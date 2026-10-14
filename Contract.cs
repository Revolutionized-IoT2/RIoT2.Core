namespace RIoT2.Core
{
    /// <summary>
    /// Version and capability names of the MQTT contract (ADR 0005, designs 7.1 and 7.2).
    /// </summary>
    public static class Contract
    {
        /// <summary>
        /// The contract version that this Core writes into the <c>contractVersion</c> field of new
        /// message types. A message without the field is version 1. The version only changes with a
        /// breaking contract change; additive fields don't change it.
        /// </summary>
        public const int Version = 1;

        /// <summary>
        /// Names a node lists in <see cref="Models.NodeOnlineMessage.Capabilities"/>. The orchestrator
        /// uses a feature only with nodes that advertise it, so older nodes keep working.
        /// </summary>
        public static class Capabilities
        {
            /// <summary>The node publishes a <see cref="Models.CommandResult"/> for every command (design 7.1).</summary>
            public const string CommandResult = "command-result/1";

            /// <summary>The node publishes a <see cref="Models.NodeStatusMessage"/> and skips unchanged configurations (design 7.2).</summary>
            public const string DesiredState = "desired-state/1";
        }
    }
}
