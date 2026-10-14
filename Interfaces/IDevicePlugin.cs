using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// Entry point of a device plugin. The node creates one instance, calls <see cref="Initialize"/> and then
    /// resolves every registered <see cref="IDevice"/> from DI. Derive from <see cref="Abstracts.PluginBase"/>.
    /// </summary>
    public interface IDevicePlugin
    {
        void Initialize(IServiceCollection services);

        [Obsolete("The node resolves devices from DI and never reads this list. Derive from PluginBase; the member is removed in the next major release.")]
        List<IDevice> Devices { get; }
    }
}
