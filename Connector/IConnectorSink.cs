namespace RIoT2.Connector.Sdk;

/// <summary>
/// Where a connector writes (design 7.3): the only part a connector implements. The SDK calls it from one worker,
/// with batches in the order received.
/// </summary>
public interface IConnectorSink
{
    /// <summary>
    /// Maps and writes a batch. Throw <see cref="TransientSinkException"/> to have the same batch retried with backoff
    /// (the target is down or busy), or <see cref="SinkRejectedException"/> when the target refuses the data itself.
    /// Any other exception counts as a rejection, so a bug can't stall the queue. Messages the sink skips (no template,
    /// nothing to write) are recorded with <see cref="ConnectorMetrics.RecordIgnored"/>.
    /// </summary>
    Task WriteAsync(IReadOnlyList<ConnectorMessage> batch, ITemplateCatalog templates, CancellationToken cancellationToken);

    /// <summary>Whether the target is reachable; true by default.</summary>
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken) => Task.FromResult(true);
}

/// <summary>The write failed for a reason that may pass (network, timeout, server error): retry the batch.</summary>
public class TransientSinkException : Exception
{
    public TransientSinkException(string message, Exception? innerException = null) : base(message, innerException) { }
}

/// <summary>The target refused the data itself (for example malformed): retrying won't help, so the batch is dropped.</summary>
public class SinkRejectedException : Exception
{
    public SinkRejectedException(string message, Exception? innerException = null) : base(message, innerException) { }
}
