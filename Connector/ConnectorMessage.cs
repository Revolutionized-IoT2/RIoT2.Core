namespace RIoT2.Connector.Sdk;

/// <summary>What a <see cref="ConnectorMessage"/> carries.</summary>
public enum ConnectorMessageKind
{
    /// <summary>A <c>Report</c> from <c>riot2/node/{id}/report</c>.</summary>
    Report,

    /// <summary>A <c>Command</c> from <c>riot2/node/{id}/command</c> (only with <c>RIOT2_HANDLE_COMMANDS=true</c>).</summary>
    Command
}

/// <summary>
/// A message as the connector received it (design 7.3, decision 2): queued raw and mapped by the sink at write
/// time, so a retried batch keeps its original payload and receive time.
/// </summary>
/// <param name="Kind">Report or command.</param>
/// <param name="NodeId">The node id from the topic.</param>
/// <param name="Json">The payload as received.</param>
/// <param name="ReceivedUtc">When the connector received it; commands carry no time of their own and use this.</param>
public sealed record ConnectorMessage(ConnectorMessageKind Kind, string NodeId, string Json, DateTimeOffset ReceivedUtc);
