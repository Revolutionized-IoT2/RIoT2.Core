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
        public NodeType NodeType { get; set; }
        public PackageManifest Manifest { get; set; } = null;
        public PackageManifest PluginManifest { get; set; } = null;
    }
}
