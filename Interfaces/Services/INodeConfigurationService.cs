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
        NodeOnlineMessage OnlineMessage { get; set; }

        /// <summary>
        /// Installs plugin package for device if there is any donwloaded. Deletes the package after installation. If there is an error during installation, it will throw exception with details of the error.
        /// </summary>
        void InstallPluginPackage();

        /// <summary>
        /// Downloads a plugin package from the specified URL into Data folder. The package can be installed by calling InstallPluginPackage.
        /// </summary>
        /// <param name="url">The URL of the plugin package to download.</param>
        void DownloadPluginPackage(string url);
    }
}