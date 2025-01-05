using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace RIoT2.Core.Interfaces
{
    public interface IDevicePlugin
    {
        void Initialize(IServiceCollection services);
        List<IDevice> Devices { get; }
    }
}
