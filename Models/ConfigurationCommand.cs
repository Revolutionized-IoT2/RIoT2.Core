namespace RIoT2.Core.Models
{
    /// <summary>
    /// Sent by the orchestrator on <c>riot2/node/{id}/configuration</c>: where to fetch the node's
    /// configuration, and since Core 1.6.0 which version it should have (design 7.2).
    /// </summary>
    public class ConfigurationCommand
    {
        public string ApiBaseUrl { get; set; }

        /// <summary>
        /// Optional revision of the desired configuration, for people and the UI. Omitted when <c>null</c>.
        /// </summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? Revision { get; set; }

        /// <summary>
        /// Optional hash of the desired configuration (<see cref="NodeDeviceConfiguration.Hash"/>). A
        /// node whose applied hash is equal skips the fetch. Omitted when <c>null</c>; a command without
        /// it always means "fetch".
        /// </summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string Hash { get; set; }

        /// <summary>
        /// Optional contract version of the payload (<see cref="Contract.Version"/>); absent means 1.
        /// </summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? ContractVersion { get; set; }
    }
}
