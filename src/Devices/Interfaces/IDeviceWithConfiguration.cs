using RIoT2.Core.Models;

namespace RIoT2.Core.Interfaces
{
    public interface IDeviceWithConfiguration : IDevice
    {
        DeviceConfiguration GetConfigurationTemplate();
    }
}