using RIoT2.Core.Models;
using System.Threading.Tasks;

namespace RIoT2.Core.Interfaces.Services
{
    public interface INodeConfigurationService
    {
        event DeviceConfigurationUpdatedHandler DeviceConfigurationUpdated;
        
        bool DeviceConfigurationLoaded { get; }
        Task LoadDeviceConfiguration(string json, string id);
        NodeConfiguration Configuration { get; }
        NodeDeviceConfiguration DeviceConfiguration { get; }
        string ApplicationFolder { get; }
        NodeOnlineMessage OnlineMessage { get; set; }
    }
}