using Contoso.Riot2Plugin.Devices;
using RIoT2.Core.Abstracts;

namespace Contoso.Riot2Plugin
{
    /// <summary>
    /// The plugin: the node creates it, calls <c>Initialize</c> and resolves every registered device from DI. Register
    /// each device, and any service the devices share, here.
    /// </summary>
    public class Plugin : PluginBase
    {
        protected override void Register()
        {
            AddDevice<Counter>();
        }
    }
}
