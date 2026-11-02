using Contoso.Riot2Plugin.Devices;
using RIoT2.Core;
using RIoT2.Core.Devices.Testing;
using RIoT2.Core.Models;

namespace Contoso.Riot2Plugin.Tests;

[TestClass]
public class PluginTests
{
    /// <summary>
    /// What the node relies on: every device resolves, has a template, reads only the parameters it declares, and
    /// starts and stops.
    /// </summary>
    [TestMethod]
    public void PluginConforms() => PluginConformance.Verify(typeof(Plugin));

    [TestMethod]
    public async Task CounterCountsOnRefreshAndTakesTheSetCommand()
    {
        await using var host = new PluginTestHost(typeof(Plugin));
        var counter = host.GetDevice<Counter>();
        var configuration = DeviceConfigurationBuilder.FromTemplate(counter.GetConfigurationTemplate())
            .WithParameter("step", "2.5")
            .Build();
        await PluginTestHost.InitializeAsync(counter, configuration);
        await PluginTestHost.StartAsync(counter);

        await PluginTestHost.RefreshAsync(counter);
        var set = configuration.CommandTemplates!.Single(c => c.Address == Counter.SetAddress);
        await PluginTestHost.CommandAsync(counter, set.Id, "10");
        await PluginTestHost.RefreshAsync(counter);
        await PluginTestHost.StopAsync(counter);

        CollectionAssert.AreEqual(new[] { 2.5, 10, 12.5 }, host.Reports.Select(r => ((Report)r.Report).Value.GetValue<double>()).ToArray());
        Assert.AreEqual(DeviceState.Stopped, counter.State);
    }
}
