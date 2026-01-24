using RIoT2.Core.Interfaces;
namespace RIoT2.Core.Models
{
    public class NodeCommandTemplate : CommandTemplate, INodeTemplate
    {
        public string NodeId { get; set; }
        public string Node { get; set; }
        public string DeviceId { get; set; }
        public string Device { get; set; }
    }
}