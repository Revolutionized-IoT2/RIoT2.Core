using Newtonsoft.Json;
using RIoT2.Core.Utils;
using System.Collections.Generic;
using Stj = System.Text.Json.Serialization;

namespace RIoT2.Core.Models
{
    /// <summary>
    /// A node's view of its configuration (design 7.2), published retained on
    /// <c>riot2/node/{id}/status</c> by nodes that advertise <see cref="Contract.Capabilities.DesiredState"/>.
    /// Separate from the online message, which stays a presence signal.
    /// </summary>
    public class NodeStatusMessage
    {
        /// <summary>Revision of the configuration the node runs, if the orchestrator sent one. Omitted when <c>null</c>.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public int? AppliedRevision { get; set; }

        /// <summary>Hash of the configuration the node runs (<see cref="NodeDeviceConfiguration.Hash"/>). Omitted when <c>null</c>.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public string AppliedHash { get; set; }

        /// <summary>One of <see cref="NodeConfigurationState"/>.</summary>
        public string State { get; set; }

        /// <summary>Why the last fetch or apply failed. Omitted when <c>null</c>.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public string Error { get; set; }

        /// <summary>Devices that failed to start although the configuration was applied. Omitted when <c>null</c>.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public List<FailedDevice> FailedDevices { get; set; }

        /// <summary>The installed plugin package (.NET nodes). Omitted when <c>null</c>.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public PackageManifest PluginManifest { get; set; }

        /// <summary>When the state was reached, as Unix epoch seconds.</summary>
        public long TimeStamp { get; set; }

        /// <summary>Contract version of the payload; <see cref="Contract.Version"/> for new messages.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public int? ContractVersion { get; set; } = Contract.Version;

        /// <summary>Creates a status message from its JSON, or returns <c>null</c> if it can't be parsed.</summary>
        public static NodeStatusMessage Create(string json)
        {
            try
            {
                return Json.Deserialize<NodeStatusMessage>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The JSON published on MQTT, without null properties.</summary>
        public string ToJson() => Json.SerializeIgnoreNulls(this);
    }

    /// <summary>A device that failed to start, in <see cref="NodeStatusMessage.FailedDevices"/>.</summary>
    public class FailedDevice
    {
        /// <summary>The device configuration id.</summary>
        public string Id { get; set; }

        /// <summary>What went wrong.</summary>
        public string Message { get; set; }
    }

    /// <summary>The values of <see cref="NodeStatusMessage.State"/>, as sent on the wire.</summary>
    public static class NodeConfigurationState
    {
        /// <summary>The configuration is applied. Devices that failed to start are listed separately.</summary>
        public const string Applied = "applied";

        /// <summary>The node is fetching or applying a configuration.</summary>
        public const string Applying = "applying";

        /// <summary>Fetching or parsing failed; the node keeps running the previous configuration.</summary>
        public const string Failed = "failed";

        /// <summary>A new plugin package is installed and the node is about to restart.</summary>
        public const string PendingRestart = "pendingRestart";
    }
}
