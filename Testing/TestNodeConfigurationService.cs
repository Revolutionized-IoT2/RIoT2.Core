using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;

namespace RIoT2.Core.Devices.Testing;

/// <summary>
/// The node configuration service a plugin sees in <see cref="PluginTestHost"/>. It holds a node id and URL and
/// never downloads or installs anything.
/// </summary>
public sealed class TestNodeConfigurationService : INodeConfigurationService
{
    public TestNodeConfigurationService(string url = "http://localhost:5000")
    {
        Configuration = new NodeConfiguration { Id = "test-node", Url = url };
    }

    public event DeviceConfigurationUpdatedHandler DeviceConfigurationUpdated;

    public bool DeviceConfigurationLoaded => DeviceConfiguration != null;
    public NodeConfiguration Configuration { get; }
    public NodeDeviceConfiguration DeviceConfiguration { get; private set; }
    public NodeOnlineMessage OnlineMessage { get; set; }

    public Task LoadDeviceConfiguration(string json, string id)
    {
        DeviceConfiguration = Utils.Json.Deserialize<NodeDeviceConfiguration>(json);
        DeviceConfigurationUpdated?.Invoke();
        return Task.CompletedTask;
    }

    public void InstallPluginPackage() { }

    public void DownloadPluginPackage(string url) =>
        throw new NotSupportedException("The test host doesn't download plugin packages.");

    public Task DownloadPluginPackageAsync(string url) =>
        throw new NotSupportedException("The test host doesn't download plugin packages.");
}
