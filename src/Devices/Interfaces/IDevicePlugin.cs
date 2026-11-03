using Microsoft.Extensions.DependencyInjection;

namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// Entry point of a device plugin. The node creates one instance, calls <see cref="Initialize"/> and then
    /// resolves every registered <see cref="IDevice"/> from DI. Derive from <see cref="Abstracts.PluginBase"/>.
    /// </summary>
    public interface IDevicePlugin
    {
        void Initialize(IServiceCollection services);
    }
}
