namespace RIoT2.Connector.Sdk;

/// <summary>
/// The connector's <c>/health</c> answer (design 7.3): <c>unhealthy</c> (HTTP 503) while MQTT is disconnected,
/// <c>degraded</c> while the sink is unreachable or messages wait in the spool, otherwise <c>ok</c>.
/// </summary>
public sealed record ConnectorHealthReport(string Status, bool Mqtt, bool Sink, int Queue, ConnectorSpoolHealth Spool)
{
    public const string Ok = "ok";
    public const string Degraded = "degraded";
    public const string Unhealthy = "unhealthy";

    /// <summary>How long the sink's check may take before it counts as unreachable.</summary>
    public static readonly TimeSpan SinkTimeout = TimeSpan.FromSeconds(5);

    [System.Text.Json.Serialization.JsonIgnore]
    public int HttpStatus => Status == Unhealthy ? 503 : 200;

    /// <summary>Checks the sink (within <see cref="SinkTimeout"/>) and reads the queue and spool.</summary>
    public static async Task<ConnectorHealthReport> CheckAsync(bool mqttConnected, IConnectorSink sink, ConnectorPipeline pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(pipeline);
        bool sinkHealthy;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(SinkTimeout);
            try
            {
                sinkHealthy = await sink.CheckHealthAsync(timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                sinkHealthy = false;
            }
        }
        var spool = new ConnectorSpoolHealth(pipeline.SpoolEnabled, pipeline.SpoolDepth, pipeline.SpoolBytes, pipeline.SpoolEnabled ? pipeline.SpoolMaxBytes : 0);
        var status = !mqttConnected ? Unhealthy : !sinkHealthy || spool.Messages > 0 ? Degraded : Ok;
        return new ConnectorHealthReport(status, mqttConnected, sinkHealthy, pipeline.QueueDepth, spool);
    }
}

/// <summary>The spool part of <see cref="ConnectorHealthReport"/>: messages and bytes on disk, and the size limit.</summary>
public sealed record ConnectorSpoolHealth(bool Enabled, long Messages, long Bytes, long MaxBytes);
