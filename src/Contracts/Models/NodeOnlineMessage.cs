using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class NodeOnlineMessage
    {
        public NodeOnlineMessage() 
        {
            IsOnline = true;
            NodeType = NodeType.Unknown;
        }
        public string Name { get; set; } = "";
        public bool IsOnline { get; set; }
        public string NodeBaseUrl { get; set; } = "";
        public string GrpcBaseUrl { get; set; } = "";
        public NodeType NodeType { get; set; }
        public PackageManifest Manifest { get; set; } = null;
        public PackageManifest PluginManifest { get; set; } = null;

        /// <summary>
        /// Optional features the node supports, from <see cref="Contract.Capabilities"/>, for example
        /// <c>["command-result/1","desired-state/1"]</c>. <c>null</c> (omitted) for older nodes, which
        /// get today's behaviour.
        /// </summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Capabilities { get; set; } = null;

        /// <summary>
        /// Optional contract version of the payload (<see cref="Contract.Version"/>); absent means 1.
        /// </summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? ContractVersion { get; set; }

        /// <summary>True when <see cref="Capabilities"/> lists <paramref name="capability"/>.</summary>
        public bool HasCapability(string capability) => Capabilities?.Contains(capability) == true;
    }
}
