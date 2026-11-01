namespace RIoT2.Core.Diagnostics
{
    /// <summary>
    /// The meters of the RIoT2 services (design 7.4, phase 4), one per service plus the shared MQTT
    /// client's. Instrument names start with <c>riot2.</c>; every one is listed in the platform's
    /// <c>docs/contracts/metrics.md</c>, which the Grafana dashboard relies on.
    /// </summary>
    public static class MeterNames
    {
        /// <summary>The MQTT client of every service: <c>riot2.mqtt.*</c>.</summary>
        public const string Mqtt = "RIoT2.Mqtt";

        /// <summary>The .NET node: <c>riot2.node.*</c>, recorded by Core's node services.</summary>
        public const string Node = "RIoT2.Node";

        public const string Orchestrator = "RIoT2.Orchestrator";
        public const string Elsa = "RIoT2.Elsa";
        public const string Connector = "RIoT2.Connector";
    }
}
