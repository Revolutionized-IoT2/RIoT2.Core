using System.Collections.Generic;

namespace RIoT2.Core.Interfaces
{
    public interface IDevicePlugin
    {
        List<IDevice> Devices { get; }
    }
}
