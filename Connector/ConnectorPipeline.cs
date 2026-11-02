using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RIoT2.Connector.Sdk;

/// <summary>
/// The connector's queue (design 7.3): a bounded in-memory queue of <see cref="ConnectorMessage"/>s, written to the
/// <see cref="IConnectorSink"/> by one worker in batches of up to <see cref="ConnectorOptions.BatchSize"/>, waiting
/// up to <see cref="ConnectorOptions.BatchInterval"/> for a batch to fill. A transient failure retries the batch with
/// backoff (<see cref="ConnectorOptions.MinRetryDelay"/> doubling to <see cref="ConnectorOptions.MaxRetryDelay"/>).
/// <para>
/// With the disk spool (<see cref="ConnectorOptions.Spool"/>, phase 2), the first transient failure moves the batch
/// and what waits in memory to disk, and new messages follow them there (as they do once the memory queue is
/// <see cref="ConnectorSpoolOptions.HighWaterMark"/> full) until the spool has been written in order; a rejected batch
/// goes to the dead-letter file; stopping finishes the write in flight and spools what is left in memory.
/// </para>
/// <para>
/// Without the spool (phase 1), new messages wait in memory during retries (refused when it is full), a rejected
/// batch is dropped, and stopping writes what is queued for up to <see cref="ConnectorOptions.DrainTimeout"/> and
/// discards the rest.
/// </para>
/// </summary>
public sealed class ConnectorPipeline : IHostedService, IAsyncDisposable
{
    private static readonly TimeSpan SpoolErrorDelay = TimeSpan.FromSeconds(5);
    private readonly IConnectorSink _sink;
    private readonly ITemplateCatalog _templates;
    private readonly ConnectorOptions _options;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly Channel<ConnectorMessage> _queue;
    private readonly int _highWater;
    // Guards the switch between memory and spool, so messages keep their order.
    private readonly Lock _gate = new();
    // Cancelled when stopping begins: no more waiting for a batch to fill or for a retry.
    private readonly CancellationTokenSource _stopping = new();
    // Cancelled when the drain time is up: the write in flight is abandoned.
    private readonly CancellationTokenSource _abort = new();
    private readonly CancellationTokenSource _stoppingOrAbort;
    private ConnectorSpool? _spool;
    // True while new messages go to the spool: from the first transient failure (or a full memory queue) until the
    // spool is written. What is in memory then is always older than what is spooled.
    private bool _spooling;
    private bool _accepting;
    // The batch the worker took from memory and hasn't settled; spooled if the connector stops.
    private List<ConnectorMessage>? _held;
    private Task _worker = Task.CompletedTask;
    private int _started;
    private int _stopped;
    private int _disposed;

    public ConnectorPipeline(IConnectorSink sink, ITemplateCatalog templates, ConnectorOptions options, ILogger<ConnectorPipeline> logger, TimeProvider? time = null)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _time = time ?? TimeProvider.System;
        _queue = Channel.CreateBounded<ConnectorMessage>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        _highWater = Math.Max(1, (int)(_options.QueueCapacity * _options.Spool.HighWaterMark));
        _stoppingOrAbort = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token, _abort.Token);
        ConnectorMetrics.Track(this);
    }

    /// <summary>Messages waiting in memory (<c>riot2.connector.queue_depth</c>).</summary>
    public int QueueDepth => _queue.Reader.Count;

    /// <summary>Whether the disk spool is on (<c>RIOT2_CONNECTOR_SPOOL</c>).</summary>
    public bool SpoolEnabled => _options.Spool.Enabled;

    /// <summary>Messages waiting on disk (<c>riot2.connector.spool_depth</c>).</summary>
    public long SpoolDepth => _spool?.Messages ?? 0;

    /// <summary>Size of the spool's segment files (<c>riot2.connector.spool_bytes</c>).</summary>
    public long SpoolBytes => _spool?.Bytes ?? 0;

    /// <summary>The spool's size limit (<c>RIOT2_CONNECTOR_SPOOL_MAX_MB</c>).</summary>
    public long SpoolMaxBytes => _options.Spool.MaxBytes;

    /// <summary>
    /// Queues a message; false when the pipeline isn't running, or the memory queue is full without the spool, or the
    /// spool can't be written.
    /// </summary>
    public bool TryEnqueue(ConnectorMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (Volatile.Read(ref _disposed) != 0)
            return false;
        lock (_gate)
        {
            if (!_accepting)
                return false;
            if (_spool == null || (!_spooling && _queue.Reader.Count < _highWater))
                return _queue.Writer.TryWrite(message);
            try
            {
                _spool.Append([message]);
                _spooling = true;
                return true;
            }
            catch (Exception error) when (IsDiskError(error))
            {
                _logger.LogError(error, "Could not write a message to the spool in {Folder}; it is lost", _spool.Folder);
                return false;
            }
        }
    }

    /// <summary>Opens the spool (when it is on) and starts the worker; spooled messages are written first.</summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("The connector pipeline has already been started.");
        if (_options.Spool.Enabled)
        {
            try
            {
                _spool = ConnectorSpool.Open(_options.Spool, _logger, _time);
            }
            catch (Exception error) when (IsDiskError(error))
            {
                throw new InvalidOperationException($"Could not open the connector spool in {_options.Spool.Path}: {error.Message}", error);
            }
            _spooling = _spool.Messages > 0;
            if (_spooling)
                _logger.LogInformation("The spool holds {Count} messages; they are written first", _spool.Messages);
        }
        lock (_gate)
            _accepting = true;
        _worker = RunAsync();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops taking messages and waits for the worker, for up to <see cref="ConnectorOptions.DrainTimeout"/> or until
    /// <paramref name="cancellationToken"/> is cancelled (then this throws). With the spool, the write in flight is
    /// finished and what is left in memory is spooled; without it, what is queued is written and the rest discarded.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _started) == 0)
            return;
        if (Interlocked.Exchange(ref _stopped, 1) == 0)
        {
            lock (_gate)
                _accepting = false;
            // Without the spool the worker drains the queue until it is complete. With it, stopping ends the worker
            // and the queue stays open, so a batch the spool couldn't take can be put back.
            if (_spool == null)
                _queue.Writer.TryComplete();
            await _stopping.CancelAsync().ConfigureAwait(false);
        }
        using var drain = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drain.CancelAfter(_options.DrainTimeout);
        try
        {
            await _worker.WaitAsync(drain.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (drain.IsCancellationRequested)
        {
            await _abort.CancelAsync().ConfigureAwait(false);
            await _worker.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogWarning("The connector stopped after {Timeout} without finishing the write in flight", _options.DrainTimeout);
        }
        finally
        {
            if (_worker.IsCompleted)
            {
                lock (_gate)
                    _spool?.Dispose();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _stoppingOrAbort.Dispose();
            _stopping.Dispose();
            _abort.Dispose();
        }
    }

    private async Task RunAsync()
    {
        var reader = _queue.Reader;
        try
        {
            while (true)
            {
                if (_spool != null && _stopping.IsCancellationRequested)
                    break;
                if (_spool != null && reader.Count == 0)
                {
                    SpoolBatch? spooled;
                    try
                    {
                        spooled = TakeFromSpool();
                    }
                    catch (Exception error) when (IsDiskError(error))
                    {
                        _logger.LogError(error, "Could not read the spool in {Folder}; trying again in {DelaySeconds} s", _spool.Folder, SpoolErrorDelay.TotalSeconds);
                        await DelayAsync(SpoolErrorDelay).ConfigureAwait(false);
                        continue;
                    }
                    if (spooled != null)
                    {
                        await DeliverAsync(spooled.Messages, spooled).ConfigureAwait(false);
                        continue;
                    }
                }

                try
                {
                    if (!await reader.WaitToReadAsync(_spool == null ? _abort.Token : _stoppingOrAbort.Token).ConfigureAwait(false))
                        break;
                }
                catch (OperationCanceledException) when (!_abort.IsCancellationRequested)
                {
                    break;  // Stopping with the spool: what waits in memory is spooled below.
                }
                var batch = new List<ConnectorMessage>(_options.BatchSize);
                Fill(batch);
                if (batch.Count < _options.BatchSize && _options.BatchInterval > TimeSpan.Zero && !_stopping.IsCancellationRequested)
                    await FillForAsync(batch).ConfigureAwait(false);
                if (batch.Count == 0)
                    continue;
                _held = batch;
                await DeliverAsync(batch, null).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_abort.IsCancellationRequested)
        {
            // The drain time is up.
        }
        finally
        {
            SaveLeftovers();
        }
    }

    // The next spooled batch, or null when the spool has been written (then new messages go to memory again).
    private SpoolBatch? TakeFromSpool()
    {
        lock (_gate)
        {
            if (!_spooling || _spool == null)
                return null;
            while (true)
            {
                var batch = _spool.Read(_options.BatchSize);
                if (batch == null)
                {
                    _spool.Clear();
                    _spooling = false;
                    _logger.LogInformation("The spool has been written; new messages are queued in memory again");
                    return null;
                }
                if (batch.Messages.Count > 0)
                    return batch;
                _spool.Acknowledge(batch);
            }
        }
    }

    private void Fill(List<ConnectorMessage> batch)
    {
        while (batch.Count < _options.BatchSize && _queue.Reader.TryRead(out var message))
            batch.Add(message);
    }

    // Waits up to the batch interval for the batch to fill; stopping ends the wait at once.
    private async Task FillForAsync(List<ConnectorMessage> batch)
    {
        using var interval = new CancellationTokenSource(_options.BatchInterval, _time);
        using var window = CancellationTokenSource.CreateLinkedTokenSource(interval.Token, _stopping.Token, _abort.Token);
        try
        {
            while (batch.Count < _options.BatchSize && await _queue.Reader.WaitToReadAsync(window.Token).ConfigureAwait(false))
                Fill(batch);
        }
        catch (OperationCanceledException) when (!_abort.IsCancellationRequested)
        {
            // The interval passed or stopping began: write what there is.
        }
    }

    // Writes a batch from memory (spooled is null) or from the spool, retrying transient failures.
    private async Task DeliverAsync(List<ConnectorMessage> batch, SpoolBatch? spooled)
    {
        var delay = _options.MinRetryDelay;
        while (true)
        {
            try
            {
                await _sink.WriteAsync(batch, _templates, _abort.Token).ConfigureAwait(false);
                ConnectorMetrics.Write(ConnectorMetrics.Written, batch.Count);
                Settle(spooled);
                return;
            }
            catch (OperationCanceledException) when (_abort.IsCancellationRequested)
            {
                if (_spool == null)
                {
                    _held = null;
                    ConnectorMetrics.Write(ConnectorMetrics.Discarded, batch.Count);
                    _logger.LogWarning("Stopping interrupted a batch of {Count} messages; whether it was written is unknown, and it is not written again", batch.Count);
                }
                else
                    _logger.LogWarning("Stopping interrupted a batch of {Count} messages; whether it was written is unknown, so it stays spooled and is written again", batch.Count);
                throw;
            }
            catch (TransientSinkException error)
            {
                ConnectorMetrics.Write(ConnectorMetrics.Retried, batch.Count);
                if (spooled == null && _spool != null && MoveToSpool(batch, out var waiting))
                {
                    _logger.LogWarning("Writing {Count} messages failed; they and the {Waiting} waiting in memory are spooled, and new messages follow them until they are written. Retrying in {DelaySeconds} s: {Reason}",
                        batch.Count, waiting, delay.TotalSeconds, error.Message);
                    _logger.LogDebug(error, "Transient sink failure");
                    await DelayAsync(delay).ConfigureAwait(false);
                    return;
                }
                if (_stopping.IsCancellationRequested)
                {
                    if (_spool == null)
                    {
                        _held = null;
                        ConnectorMetrics.Write(ConnectorMetrics.Discarded, batch.Count);
                        _logger.LogWarning("Writing {Count} messages failed while the connector stopped; they are discarded: {Reason}", batch.Count, error.Message);
                    }
                    // With the spool the batch is on disk already, or is spooled with what is left in memory.
                    return;
                }
                // Expected while the target is down: one line per attempt, the stack only at debug level.
                _logger.LogWarning("Writing {Count} messages failed; retrying in {DelaySeconds} s: {Reason}", batch.Count, delay.TotalSeconds, error.Message);
                _logger.LogDebug(error, "Transient sink failure");
                await DelayAsync(delay).ConfigureAwait(false);
                if (_spool != null && _stopping.IsCancellationRequested)
                    return;
                // Without the spool, stopping ends the wait: one more attempt, then the batch is discarded.
                delay = delay * 2 < _options.MaxRetryDelay ? delay * 2 : _options.MaxRetryDelay;
            }
            catch (Exception error)
            {
                ConnectorMetrics.Write(ConnectorMetrics.Rejected, batch.Count);
                DeadLetter(batch, error);
                Settle(spooled);
                return;
            }
        }
    }

    // The batch is written or dead-lettered: forget it, or move the spool's checkpoint past it.
    private void Settle(SpoolBatch? spooled)
    {
        if (spooled == null)
        {
            _held = null;
            return;
        }
        lock (_gate)
        {
            try
            {
                _spool!.Acknowledge(spooled);
            }
            catch (Exception error) when (IsDiskError(error))
            {
                // The batch stays unacknowledged and is written again: twice rather than lost.
                _logger.LogError(error, "Could not move the spool in {Folder} past a written batch; it may be written again", _spool!.Folder);
            }
        }
    }

    // Puts the failed batch and what waits in memory ahead of everything spooled, and sends new messages to the spool.
    private bool MoveToSpool(List<ConnectorMessage> batch, out int waiting)
    {
        lock (_gate)
        {
            var messages = new List<ConnectorMessage>(batch);
            while (_queue.Reader.TryRead(out var message))
                messages.Add(message);
            waiting = messages.Count - batch.Count;
            try
            {
                _spool!.Prepend(messages);
                _spooling = true;
                _held = null;
                return true;
            }
            catch (Exception error) when (IsDiskError(error))
            {
                _logger.LogError(error, "Could not spool {Count} messages in {Folder}; the batch is retried from memory", messages.Count, _spool!.Folder);
                // The queue is still open and empty: putting them back keeps their order.
                foreach (var message in messages.Skip(batch.Count))
                    _queue.Writer.TryWrite(message);
                waiting = 0;
                return false;
            }
        }
    }

    private void DeadLetter(List<ConnectorMessage> batch, Exception error)
    {
        if (_spool == null)
        {
            _logger.LogError(error, "The sink rejected a batch of {Count} messages; they are dropped", batch.Count);
            return;
        }
        lock (_gate)
        {
            try
            {
                _spool.DeadLetter(batch, error.Message);
                _logger.LogError(error, "The sink rejected a batch of {Count} messages; they are in {File}", batch.Count, Path.Combine(_spool.Folder, ConnectorSpool.DeadLetterFile));
            }
            catch (Exception failure) when (IsDiskError(failure))
            {
                _logger.LogError(error, "The sink rejected a batch of {Count} messages, and the dead-letter file can't be written ({Failure}); they are dropped", batch.Count, failure.Message);
            }
        }
    }

    // When the worker ends: the batch it held and what waits in memory are spooled, or discarded without the spool.
    private void SaveLeftovers()
    {
        lock (_gate)
        {
            var left = new List<ConnectorMessage>(_held ?? []);
            _held = null;
            while (_queue.Reader.TryRead(out var message))
                left.Add(message);
            if (left.Count == 0)
                return;
            if (_spool != null)
            {
                try
                {
                    _spool.Prepend(left);
                    _logger.LogInformation("Spooled {Count} messages from memory as the connector stopped; they are written at the next start", left.Count);
                    return;
                }
                catch (Exception error) when (IsDiskError(error))
                {
                    _logger.LogError(error, "Could not spool {Count} messages as the connector stopped", left.Count);
                }
            }
            ConnectorMetrics.Write(ConnectorMetrics.Discarded, left.Count);
            _logger.LogWarning("Discarded {Count} queued messages when the connector stopped", left.Count);
        }
    }

    private async Task DelayAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, _time, _stopping.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            // Stopping ends the wait.
        }
    }

    private static bool IsDiskError(Exception error) => error is IOException or UnauthorizedAccessException;
}
