using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class NodeDeviceConfiguration
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public List<DeviceConfiguration> DeviceConfigurations { get; set; }
    }
}
