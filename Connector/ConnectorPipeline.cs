using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RIoT2.Connector.Sdk;

/// <summary>
/// The connector's queue (design 7.3 phase 1): a bounded in-memory queue of <see cref="ConnectorMessage"/>s, written
/// to the <see cref="IConnectorSink"/> by one worker in batches of up to <see cref="ConnectorOptions.BatchSize"/>,
/// waiting up to <see cref="ConnectorOptions.BatchInterval"/> for a batch to fill. A transient failure retries the
/// same batch with backoff (<see cref="ConnectorOptions.MinRetryDelay"/> doubling to
/// <see cref="ConnectorOptions.MaxRetryDelay"/>) while new messages wait in the queue; a rejection drops the batch.
/// Stopping writes what is queued for up to <see cref="ConnectorOptions.DrainTimeout"/> and discards the rest.
/// </summary>
public sealed class ConnectorPipeline : IHostedService, IAsyncDisposable
{
    private readonly IConnectorSink _sink;
    private readonly ITemplateCatalog _templates;
    private readonly ConnectorOptions _options;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly Channel<ConnectorMessage> _queue;
    // Cancelled when stopping begins: no more waiting for a batch to fill or for a retry.
    private readonly CancellationTokenSource _stopping = new();
    // Cancelled when the drain time is up: the write in flight is abandoned.
    private readonly CancellationTokenSource _abort = new();
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
        ConnectorMetrics.Track(this);
    }

    /// <summary>Messages waiting to be written (<c>riot2.connector.queue_depth</c>).</summary>
    public int QueueDepth => _queue.Reader.Count;

    /// <summary>Queues a message; false when the pipeline isn't running or the queue is full.</summary>
    public bool TryEnqueue(ConnectorMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Volatile.Read(ref _started) != 0 && Volatile.Read(ref _disposed) == 0 && _queue.Writer.TryWrite(message);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("The connector pipeline has already been started.");
        _worker = RunAsync();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops taking messages and writes what is queued, for up to <see cref="ConnectorOptions.DrainTimeout"/> or until
    /// <paramref name="cancellationToken"/> is cancelled (then this throws); what is left is discarded.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _started) == 0)
            return;
        if (Interlocked.Exchange(ref _stopped, 1) == 0)
        {
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
            _logger.LogWarning("The connector stopped after {Timeout} without writing everything that was queued", _options.DrainTimeout);
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
            _stopping.Dispose();
            _abort.Dispose();
        }
    }

    private async Task RunAsync()
    {
        var reader = _queue.Reader;
        try
        {
            while (await reader.WaitToReadAsync(_abort.Token).ConfigureAwait(false))
            {
                var batch = new List<ConnectorMessage>(_options.BatchSize);
                Fill(batch);
                if (batch.Count < _options.BatchSize && _options.BatchInterval > TimeSpan.Zero && !_stopping.IsCancellationRequested)
                    await FillForAsync(batch).ConfigureAwait(false);
                await DeliverAsync(batch).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_abort.IsCancellationRequested)
        {
            // The drain time is up.
        }
        finally
        {
            var left = 0;
            while (reader.TryRead(out _))
                left++;
            ConnectorMetrics.Write(ConnectorMetrics.Discarded, left);
            if (left > 0)
                _logger.LogWarning("Discarded {Count} queued messages when the connector stopped", left);
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

    private async Task DeliverAsync(List<ConnectorMessage> batch)
    {
        var delay = _options.MinRetryDelay;
        while (true)
        {
            try
            {
                await _sink.WriteAsync(batch, _templates, _abort.Token).ConfigureAwait(false);
                ConnectorMetrics.Write(ConnectorMetrics.Written, batch.Count);
                return;
            }
            catch (OperationCanceledException) when (_abort.IsCancellationRequested)
            {
                ConnectorMetrics.Write(ConnectorMetrics.Discarded, batch.Count);
                _logger.LogWarning("Stopping interrupted a batch of {Count} messages; whether it was written is unknown, and it is not written again", batch.Count);
                throw;
            }
            catch (TransientSinkException error)
            {
                ConnectorMetrics.Write(ConnectorMetrics.Retried, batch.Count);
                if (_stopping.IsCancellationRequested)
                {
                    ConnectorMetrics.Write(ConnectorMetrics.Discarded, batch.Count);
                    _logger.LogWarning("Writing {Count} messages failed while the connector stopped; they are discarded: {Reason}", batch.Count, error.Message);
                    return;
                }
                // Expected while the target is down: one line per attempt, the stack only at debug level.
                _logger.LogWarning("Writing {Count} messages failed; retrying in {DelaySeconds} s: {Reason}", batch.Count, delay.TotalSeconds, error.Message);
                _logger.LogDebug(error, "Transient sink failure");
                try
                {
                    await Task.Delay(delay, _time, _stopping.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
                {
                    // Stopping: one more attempt, then the batch is discarded if it fails again.
                }
                delay = delay * 2 < _options.MaxRetryDelay ? delay * 2 : _options.MaxRetryDelay;
            }
            catch (Exception error)
            {
                ConnectorMetrics.Write(ConnectorMetrics.Rejected, batch.Count);
                _logger.LogError(error, "The sink rejected a batch of {Count} messages; they are dropped", batch.Count);
                return;
            }
        }
    }
}
