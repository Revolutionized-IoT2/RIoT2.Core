using System.Diagnostics.Metrics;
using RIoT2.Core.Diagnostics;

namespace RIoT2.Connector.Sdk;

/// <summary>
/// The connector instruments (meter <see cref="MeterNames.Connector"/>, design 7.4): messages, writes, the in-memory
/// queue and the disk spool. Nothing is measured while no listener is attached.
/// </summary>
public static class ConnectorMetrics
{
    public const string MessagesName = "riot2.connector.messages";
    public const string WritesName = "riot2.connector.writes";
    public const string QueueDepthName = "riot2.connector.queue_depth";
    public const string SpoolDepthName = "riot2.connector.spool_depth";
    public const string SpoolBytesName = "riot2.connector.spool_bytes";
    public const string SpoolDroppedName = "riot2.connector.spool_dropped";

    /// <summary><see cref="SpoolDropped"/> reasons.</summary>
    public const string Size = "size";
    public const string Age = "age";
    public const string Unreadable = "unreadable";

    /// <summary><see cref="Messages"/> results.</summary>
    public const string Queued = "queued";
    public const string Ignored = "ignored";
    public const string Invalid = "invalid";
    public const string Failed = "failed";

    /// <summary><see cref="Writes"/> outcomes.</summary>
    public const string Written = "written";
    public const string Retried = "retried";
    public const string Rejected = "rejected";
    public const string Discarded = "discarded";

    private static readonly Meter Meter = new(MeterNames.Connector);
    private static readonly List<WeakReference<ConnectorPipeline>> Pipelines = [];

    /// <summary>
    /// Messages by <c>type</c> (<c>report</c>, <c>command</c>) and <c>result</c>: <c>queued</c>, <c>invalid</c>
    /// (unreadable) or <c>failed</c> (the queue was full or stopping) on receipt, and <c>ignored</c> when the sink
    /// skipped a queued message (no template, nothing to write).
    /// </summary>
    public static readonly Counter<long> Messages = Meter.CreateCounter<long>(MessagesName, "{message}",
        "Messages received, by type and result (queued, invalid or failed on receipt; ignored by the sink).");

    /// <summary>
    /// Messages handed to the sink, by <c>outcome</c>: <c>written</c>, <c>retried</c> (a transient failure; the batch
    /// is tried again), <c>rejected</c> (dead-lettered with the spool, otherwise dropped) or <c>discarded</c> (left at
    /// shutdown without the spool).
    /// </summary>
    public static readonly Counter<long> Writes = Meter.CreateCounter<long>(WritesName, "{message}",
        "Messages handed to the sink, by outcome: written, retried, rejected, or discarded at shutdown.");

    /// <summary>Spooled messages lost, by <c>reason</c>: <c>size</c> or <c>age</c> (the oldest segment was dropped) or <c>unreadable</c>.</summary>
    public static readonly Counter<long> SpoolDropped = Meter.CreateCounter<long>(SpoolDroppedName, "{message}",
        "Spooled messages dropped, by reason: size or age limit, or unreadable.");

    static ConnectorMetrics()
    {
        Meter.CreateObservableGauge(QueueDepthName, ObserveQueue, "{message}", "Messages waiting to be written.");
        Meter.CreateObservableGauge(SpoolDepthName, () => Observe(pipeline => pipeline.SpoolDepth), "{message}", "Messages waiting in the disk spool.");
        Meter.CreateObservableGauge(SpoolBytesName, () => Observe(pipeline => pipeline.SpoolBytes), "By", "Size of the disk spool's segment files.");
    }

    internal static void Dropped(string reason, long messages)
    {
        if (messages > 0)
            SpoolDropped.Add(messages, new KeyValuePair<string, object?>("reason", reason));
    }

    public static void Message(string type, string result) =>
        Messages.Add(1, new KeyValuePair<string, object?>("type", type), new KeyValuePair<string, object?>("result", result));

    /// <summary>A queued message the sink skipped.</summary>
    public static void RecordIgnored(ConnectorMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Message(TypeOf(message.Kind), Ignored);
    }

    public static void Write(string outcome, int messages)
    {
        if (messages > 0)
            Writes.Add(messages, new KeyValuePair<string, object?>("outcome", outcome));
    }

    internal static string TypeOf(ConnectorMessageKind kind) => kind == ConnectorMessageKind.Command ? "command" : "report";

    /// <summary>Measures <paramref name="pipeline"/>'s queue in <c>riot2.connector.queue_depth</c> while it lives.</summary>
    internal static void Track(ConnectorPipeline pipeline)
    {
        lock (Pipelines)
        {
            Pipelines.RemoveAll(reference => !reference.TryGetTarget(out _));
            Pipelines.Add(new WeakReference<ConnectorPipeline>(pipeline));
        }
    }

    private static IEnumerable<Measurement<int>> ObserveQueue()
    {
        lock (Pipelines)
        {
            var depth = 0;
            foreach (var reference in Pipelines)
            {
                if (reference.TryGetTarget(out var pipeline))
                    depth += pipeline.QueueDepth;
            }
            return [new Measurement<int>(depth)];
        }
    }

    private static IEnumerable<Measurement<long>> Observe(Func<ConnectorPipeline, long> value)
    {
        lock (Pipelines)
        {
            long total = 0;
            foreach (var reference in Pipelines)
            {
                if (reference.TryGetTarget(out var pipeline))
                    total += value(pipeline);
            }
            return [new Measurement<long>(total)];
        }
    }
}
