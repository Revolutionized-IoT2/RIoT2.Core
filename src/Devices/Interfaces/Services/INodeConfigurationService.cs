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
        NodeDeviceConfiguration? DeviceConfiguration { get; }
        NodeOnlineMessage? OnlineMessage { get; set; }

        /// <summary>
        /// At start, before plugins load: installs a package staged by <see cref="DownloadPluginPackageAsync"/>, keeping the
        /// previous plugins for one rollback (design 7.2). Throws when the install fails.
        /// </summary>
        void InstallPluginPackage();

        /// <summary>
        /// Downloads the plugin package at <paramref name="url"/>, verifies it (its <c>.sha256</c> sidecar and
        /// <c>coreVersion</c>) and stages it; <see cref="InstallPluginPackage"/> installs it at the next start.
        /// </summary>
        /// <param name="url">The URL of the plugin package to download.</param>
        Task DownloadPluginPackageAsync(string url);
    }
}