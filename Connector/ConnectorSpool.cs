using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace RIoT2.Connector.Sdk;

/// <summary>
/// The disk spool (design 7.3 phase 2): JSON-lines segment files named by a sequence number, read lowest first from
/// a checkpoint (<c>checkpoint.json</c>: a segment and a byte offset in it). New messages are appended to the highest
/// segment; messages older than everything spooled (a failed batch, what waited in memory) go into a new lowest one.
/// A segment is closed at <see cref="ConnectorSpoolOptions.SegmentBytes"/> or after an hour, and the oldest is
/// dropped beyond <see cref="ConnectorSpoolOptions.MaxBytes"/>; any closed segment last written more than
/// <see cref="ConnectorSpoolOptions.MaxAge"/> ago is dropped too. Not thread-safe: the pipeline calls it under its
/// lock. The counters may be read from any thread.
/// </summary>
internal sealed class ConnectorSpool : IDisposable
{
    internal const string DeadLetterFile = "dead-letter.jsonl";
    internal const string PreviousDeadLetterFile = "dead-letter.1.jsonl";
    internal const string CheckpointFile = "checkpoint.json";
    private const string SegmentExtension = ".jsonl";
    private const string TemporaryExtension = ".tmp";
    private const long FirstSegment = 1_000_000_000;
    private static readonly TimeSpan SegmentLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan ExpiryInterval = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly ConnectorSpoolOptions _options;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly long _segmentBytes;
    private readonly SortedDictionary<long, Segment> _segments = [];
    private long _checkpointSegment;
    private long _checkpointOffset;
    // Numbers are never reused while the process runs, so a stale batch can't acknowledge a new segment.
    private long _lowest = FirstSegment;
    private long _highest = FirstSegment - 1;
    private FileStream? _writer;
    private long _writerSegment;
    private DateTimeOffset _writerOpened;
    private DateTimeOffset _lastExpiry;
    private long _messages;
    private long _bytes;

    private ConnectorSpool(ConnectorSpoolOptions options, ILogger logger, TimeProvider time)
    {
        _options = options;
        _logger = logger;
        _time = time;
        _segmentBytes = Math.Max(1, Math.Min(options.SegmentBytes, options.MaxBytes / 10));
    }

    /// <summary>Messages waiting on disk.</summary>
    public long Messages => Interlocked.Read(ref _messages);

    /// <summary>Size of the segment files.</summary>
    public long Bytes => Interlocked.Read(ref _bytes);

    public string Folder => _options.Path;

    /// <summary>Opens the folder (creating it), finds the segments and the checkpoint, and drops expired segments.</summary>
    public static ConnectorSpool Open(ConnectorSpoolOptions options, ILogger logger, TimeProvider time)
    {
        var spool = new ConnectorSpool(options, logger, time);
        Directory.CreateDirectory(options.Path);
        // Left by a Prepend the process didn't finish; never registered, so not part of the spool.
        foreach (var path in Directory.EnumerateFiles(options.Path, "*" + SegmentExtension + TemporaryExtension))
            File.Delete(path);
        foreach (var path in Directory.EnumerateFiles(options.Path, "*" + SegmentExtension))
        {
            if (long.TryParse(System.IO.Path.GetFileNameWithoutExtension(path), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                spool._segments[number] = new Segment(path) { Length = new FileInfo(path).Length };
        }
        if (spool._segments.Count > 0)
        {
            spool._lowest = spool._segments.Keys.First();
            spool._highest = spool._segments.Keys.Last();
        }
        spool.ReadCheckpoint();
        foreach (var (number, segment) in spool._segments)
        {
            (segment.Lines, segment.Torn) = CountLines(segment.Path, number == spool._checkpointSegment ? spool._checkpointOffset : 0);
            spool._messages += segment.Lines;
            spool._bytes += segment.Length;
        }
        spool.Housekeep(() => spool.Expire(force: true));
        return spool;
    }

    /// <summary>
    /// Appends <paramref name="messages"/> after everything spooled. When this throws, the segment is closed (a line
    /// it cut short is counted as unreadable later), and the messages may or may not be on disk.
    /// </summary>
    public void Append(IReadOnlyList<ConnectorMessage> messages)
    {
        if (messages.Count == 0)
            return;
        Housekeep(() => Expire(force: false));
        var writer = EnsureWriter();
        var segment = _segments[_writerSegment];
        try
        {
            foreach (var message in messages)
            {
                var line = Line(message);
                writer.Write(line);
                segment.Length += line.Length;
                segment.Lines++;
                Interlocked.Add(ref _bytes, line.Length);
                Interlocked.Increment(ref _messages);
            }
            writer.Flush();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            CloseWriterAfterFailure(segment);
            throw;
        }
        if (segment.Length >= _segmentBytes || _time.GetUtcNow() - _writerOpened >= SegmentLifetime)
            CloseWriter();
        Housekeep(EnforceSize);
    }

    /// <summary>
    /// Puts <paramref name="messages"/> ahead of everything spooled, in new lowest segments. All or nothing: when this
    /// throws, nothing was added.
    /// </summary>
    public void Prepend(IReadOnlyList<ConnectorMessage> messages)
    {
        if (messages.Count == 0)
            return;
        // Segment-sized files, so the size limit drops the oldest part rather than all of them.
        var chunks = new List<List<byte[]>> { new() };
        long size = 0;
        foreach (var message in messages)
        {
            var line = Line(message);
            if (size >= _segmentBytes)
            {
                chunks.Add([]);
                size = 0;
            }
            chunks[^1].Add(line);
            size += line.Length;
        }
        var first = _lowest - chunks.Count;
        var segments = new List<(long Number, Segment Segment)>(chunks.Count);
        var created = new List<string>();
        try
        {
            // Written under temporary names, which Open ignores, and renamed once all are on disk.
            for (var i = 0; i < chunks.Count; i++)
            {
                var number = first + i;
                var segment = new Segment(SegmentPath(number));
                var temporary = segment.Path + TemporaryExtension;
                created.Add(temporary);
                using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    foreach (var line in chunks[i])
                    {
                        file.Write(line);
                        segment.Length += line.Length;
                        segment.Lines++;
                    }
                    file.Flush(flushToDisk: true);
                }
                segments.Add((number, segment));
            }
            for (var i = 0; i < segments.Count; i++)
            {
                File.Move(created[i], segments[i].Segment.Path);
                created[i] = segments[i].Segment.Path;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            foreach (var path in created)
                TryDeleteFile(path);
            throw;
        }
        _lowest = first;
        foreach (var (number, segment) in segments)
        {
            _segments[number] = segment;
            Interlocked.Add(ref _bytes, segment.Length);
            Interlocked.Add(ref _messages, segment.Lines);
        }
        Housekeep(EnforceSize);
    }

    /// <summary>
    /// The next messages from the checkpoint on, at most <paramref name="max"/> lines; null when everything is read.
    /// Unreadable lines are counted in the batch and skipped when it is acknowledged.
    /// </summary>
    public SpoolBatch? Read(int max)
    {
        Housekeep(() => Expire(force: false));
        while (_segments.Count > 0)
        {
            var (number, segment) = _segments.First();
            var start = number == _checkpointSegment ? _checkpointOffset : 0;
            if (start >= segment.Length)
            {
                if (number == _writerSegment)
                    return null;
                Delete(number);
                continue;
            }
            SpoolBatch batch;
            try
            {
                batch = ReadLines(number, segment.Path, start, max);
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                _logger.LogWarning("Spool segment {Path} disappeared; its {Count} messages are lost", segment.Path, segment.Lines);
                ConnectorMetrics.Dropped(ConnectorMetrics.Unreadable, segment.Lines);
                Delete(number);
                continue;
            }
            if (batch.Lines > 0)
                return batch;
            // Only a torn last line (the process died while writing it) is left.
            segment.Torn = true;
            if (number == _writerSegment)
                CloseWriter();
            Delete(number);
        }
        return null;
    }

    /// <summary>Moves the checkpoint past <paramref name="batch"/>, deleting a fully read segment.</summary>
    public void Acknowledge(SpoolBatch batch)
    {
        if (!_segments.TryGetValue(batch.Segment, out var segment))
            return;  // Dropped by a limit while the batch was being written.
        if (batch.Segment == _checkpointSegment && batch.End <= _checkpointOffset)
            return;
        if (batch.End >= segment.Length && batch.Segment != _writerSegment)
        {
            // Fully read: the file goes first, so a failed delete changes nothing and the batch stays unacknowledged.
            Delete(batch.Segment);
        }
        else
        {
            segment.Lines -= batch.Lines;
            Interlocked.Add(ref _messages, -batch.Lines);
            _checkpointSegment = batch.Segment;
            _checkpointOffset = batch.End;
        }
        if (batch.Unreadable > 0)
        {
            _logger.LogWarning("Skipped {Count} unreadable lines in spool segment {Path}", batch.Unreadable, segment.Path);
            ConnectorMetrics.Dropped(ConnectorMetrics.Unreadable, batch.Unreadable);
        }
        if (Messages <= 0)
            Clear();
        else
            SaveCheckpoint();
    }

    /// <summary>Deletes every segment and the checkpoint.</summary>
    public void Clear()
    {
        CloseWriter();
        foreach (var number in _segments.Keys.ToList())
            Delete(number);
        _checkpointSegment = 0;
        _checkpointOffset = 0;
        SaveCheckpoint();
        Interlocked.Exchange(ref _messages, 0);
        Interlocked.Exchange(ref _bytes, 0);
    }

    /// <summary>Appends a rejected batch to <c>dead-letter.jsonl</c> with the error, rotating the file at its size limit.</summary>
    public void DeadLetter(IReadOnlyList<ConnectorMessage> batch, string error)
    {
        var path = System.IO.Path.Combine(_options.Path, DeadLetterFile);
        var file = new FileInfo(path);
        if (file.Exists && file.Length >= _options.DeadLetterMaxBytes)
            File.Move(path, System.IO.Path.Combine(_options.Path, PreviousDeadLetterFile), overwrite: true);
        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        var rejected = _time.GetUtcNow();
        foreach (var message in batch)
        {
            stream.Write(JsonSerializer.SerializeToUtf8Bytes(new DeadLetterRecord(rejected, error, message), Json));
            stream.WriteByte((byte)'\n');
        }
        stream.Flush(flushToDisk: true);
    }

    public void Dispose() => CloseWriter();

    private FileStream EnsureWriter()
    {
        if (_writer != null)
            return _writer;
        var number = ++_highest;
        var path = SegmentPath(number);
        _writer = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete);
        _segments[number] = new Segment(path);
        _writerSegment = number;
        _writerOpened = _time.GetUtcNow();
        return _writer;
    }

    private void CloseWriter()
    {
        _writer?.Dispose();
        _writer = null;
        _writerSegment = 0;
    }

    // After a failed append: closes the segment (the next append starts a new one) and takes its length from the
    // file, marking a line cut short as torn.
    private void CloseWriterAfterFailure(Segment segment)
    {
        try
        {
            _writer?.Dispose();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(error, "Closing spool segment {Path} after a failed write", segment.Path);
        }
        _writer = null;
        _writerSegment = 0;
        try
        {
            var length = new FileInfo(segment.Path).Length;
            if (length != segment.Length)
            {
                Interlocked.Add(ref _bytes, length - segment.Length);
                segment.Length = length;
                segment.Torn = true;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(error, "Reading the length of spool segment {Path}", segment.Path);
        }
    }

    // The limits are housekeeping: a disk error there is logged and tried again later, and doesn't fail the write.
    private void Housekeep(Action action)
    {
        try
        {
            action();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(error, "Spool housekeeping in {Folder} failed; it is tried again with the next write", _options.Path);
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(error, "Could not delete {Path}", path);
        }
    }

    private void EnforceSize()
    {
        while (Bytes > _options.MaxBytes && _segments.Count > 0)
        {
            var (number, segment) = _segments.First();
            if (number == _writerSegment)
                CloseWriter();
            _logger.LogWarning("The spool is over {MaxBytes} bytes; dropped its oldest segment with {Count} messages", _options.MaxBytes, segment.Lines);
            ConnectorMetrics.Dropped(ConnectorMetrics.Size, segment.Lines);
            Delete(number);
        }
    }

    private void Expire(bool force)
    {
        var now = _time.GetUtcNow();
        if (!force && now - _lastExpiry < ExpiryInterval)
            return;
        _lastExpiry = now;
        if (_writer != null && now - _writerOpened >= SegmentLifetime)
            CloseWriter();
        var cutoff = now - _options.MaxAge;
        foreach (var (number, segment) in _segments.ToList())
        {
            if (number == _writerSegment || File.GetLastWriteTimeUtc(segment.Path) >= cutoff.UtcDateTime)
                continue;
            _logger.LogWarning("Spool segment {Path} is older than {MaxAge}; dropped its {Count} messages", segment.Path, _options.MaxAge, segment.Lines);
            ConnectorMetrics.Dropped(ConnectorMetrics.Age, segment.Lines);
            Delete(number);
        }
        if (_segments.Count == 0 && Messages != 0)
            Clear();
    }

    // Deletes the file first: if that fails nothing changes, so the segment is still tracked and tried again.
    private void Delete(long number)
    {
        if (!_segments.TryGetValue(number, out var segment))
            return;
        if (number == _writerSegment)
            CloseWriter();
        File.Delete(segment.Path);
        _segments.Remove(number);
        if (segment.Torn)
        {
            _logger.LogWarning("Spool segment {Path} ended with an incomplete line (the process stopped while writing it); the line is lost", segment.Path);
            ConnectorMetrics.Dropped(ConnectorMetrics.Unreadable, 1);
        }
        Interlocked.Add(ref _bytes, -segment.Length);
        Interlocked.Add(ref _messages, -segment.Lines);
        if (number == _checkpointSegment)
        {
            _checkpointSegment = 0;
            _checkpointOffset = 0;
            SaveCheckpoint();
        }
    }

    private SpoolBatch ReadLines(long number, string path, long start, int max)
    {
        var messages = new List<ConnectorMessage>(max);
        var lines = 0;
        var unreadable = 0;
        var end = start;
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        file.Seek(start, SeekOrigin.Begin);
        var buffer = new byte[64 * 1024];
        using var pending = new MemoryStream();
        while (lines < max)
        {
            var read = file.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;
            var position = 0;
            while (position < read && lines < max)
            {
                var newline = Array.IndexOf(buffer, (byte)'\n', position, read - position);
                if (newline < 0)
                {
                    pending.Write(buffer, position, read - position);
                    break;
                }
                pending.Write(buffer, position, newline - position);
                end += pending.Length + 1;
                var message = Parse(pending.GetBuffer().AsSpan(0, (int)pending.Length));
                if (message == null)
                    unreadable++;
                else
                    messages.Add(message);
                lines++;
                pending.SetLength(0);
                position = newline + 1;
            }
        }
        return new SpoolBatch(number, end, messages, lines, unreadable);
    }

    private static ConnectorMessage? Parse(ReadOnlySpan<byte> line)
    {
        try
        {
            var message = JsonSerializer.Deserialize<ConnectorMessage>(line, Json);
            return message is { Json: not null, NodeId: not null } ? message : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static byte[] Line(ConnectorMessage message)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        var line = new byte[json.Length + 1];
        json.CopyTo(line, 0);
        line[^1] = (byte)'\n';
        return line;
    }

    // Complete lines from start on, and whether the file ends in an incomplete one.
    private static (long Lines, bool Torn) CountLines(string path, long start)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        file.Seek(start, SeekOrigin.Begin);
        var buffer = new byte[64 * 1024];
        long lines = 0;
        var last = (byte)'\n';
        int read;
        while ((read = file.Read(buffer, 0, buffer.Length)) > 0)
        {
            lines += buffer.AsSpan(0, read).Count((byte)'\n');
            last = buffer[read - 1];
        }
        return (lines, last != (byte)'\n');
    }

    private void ReadCheckpoint()
    {
        var path = System.IO.Path.Combine(_options.Path, CheckpointFile);
        if (!File.Exists(path))
            return;
        try
        {
            var checkpoint = JsonSerializer.Deserialize<Checkpoint>(File.ReadAllBytes(path), Json);
            if (checkpoint != null && _segments.TryGetValue(checkpoint.Segment, out var segment) && checkpoint.Offset >= 0 && checkpoint.Offset <= segment.Length)
            {
                _checkpointSegment = checkpoint.Segment;
                _checkpointOffset = checkpoint.Offset;
            }
        }
        catch (JsonException error)
        {
            _logger.LogWarning(error, "The spool checkpoint {Path} is unreadable; the spool is replayed from the start", path);
        }
    }

    private void SaveCheckpoint()
    {
        var path = System.IO.Path.Combine(_options.Path, CheckpointFile);
        if (_checkpointSegment == 0)
        {
            File.Delete(path);
            return;
        }
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(new Checkpoint(_checkpointSegment, _checkpointOffset), Json));
        File.Move(temporary, path, overwrite: true);
    }

    private string SegmentPath(long number) =>
        System.IO.Path.Combine(_options.Path, number.ToString("D19", CultureInfo.InvariantCulture) + SegmentExtension);

    private sealed class Segment(string path)
    {
        public string Path { get; } = path;
        public long Length { get; set; }
        /// <summary>Complete lines not yet acknowledged.</summary>
        public long Lines { get; set; }
        /// <summary>The file ends with an incomplete line, counted as unreadable when the segment is deleted.</summary>
        public bool Torn { get; set; }
    }

    private sealed record Checkpoint(long Segment, long Offset);

    private sealed record DeadLetterRecord(DateTimeOffset RejectedUtc, string Error, ConnectorMessage Message);
}

/// <summary>Lines read from one spool segment: <see cref="Messages"/> plus <see cref="Unreadable"/> lines, ending at byte <see cref="End"/>.</summary>
internal sealed record SpoolBatch(long Segment, long End, List<ConnectorMessage> Messages, int Lines, int Unreadable);
