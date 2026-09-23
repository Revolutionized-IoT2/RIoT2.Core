using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RIoT2.Core.Models;

namespace RIoT2.Core.Interfaces.Services
{
    public interface IAsyncDeviceService : IDeviceService
    {
        Task ReconfigureDevicesAsync(IEnumerable<DeviceConfiguration> configuration, CancellationToken cancellationToken);
        Task StartAllDevicesAsync(bool restartDevicesInErrorState, CancellationToken cancellationToken);
        Task StopAllDevicesAsync(CancellationToken cancellationToken);
        Task ExecuteCommandAsync(string commandId, string value, CancellationToken cancellationToken);
        Task RefreshReportAsync(IDevice device, string group, string name, CancellationToken cancellationToken);
        bool IsActive(IDevice device);
    }
}
