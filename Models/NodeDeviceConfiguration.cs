using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class NodeDeviceConfiguration
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string PluginPackageUrl { get; set; }
        public List<DeviceConfiguration> DeviceConfigurations { get; set; }

        /// <summary>
        /// Optional revision, incremented by the orchestrator on every save that changes the content
        /// (design 7.2). For people and the UI; nodes compare <see cref="Hash"/>. Omitted when <c>null</c>.
        /// </summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? Revision { get; set; }

        /// <summary>
        /// Optional lowercase hex SHA-256 of the configuration as served (design 7.2). Omitted when <c>null</c>.
        /// </summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string Hash { get; set; }
    }
}
