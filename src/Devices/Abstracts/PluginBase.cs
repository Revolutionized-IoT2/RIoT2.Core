using Microsoft.Extensions.DependencyInjection;
using RIoT2.Core.Interfaces;
using System;
using System.Collections.Generic;

namespace RIoT2.Core.Abstracts
{
    /// <summary>
    /// Base class for device plugins. Override <see cref="Register"/> and call <see cref="AddService{TService, TImplementation}"/>
    /// and <see cref="AddDevice{TDevice}"/>. Every registration is a host-owned singleton. The node adds the plugin's
    /// controllers itself, so a plugin never calls <c>AddControllers</c>.
    /// </summary>
    public abstract class PluginBase : IDevicePlugin
    {
        private IServiceCollection _services;

        /// <summary>Registers the plugin's services and devices.</summary>
        protected abstract void Register();

        public void Initialize(IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (_services != null)
                throw new InvalidOperationException("The plugin is already being initialized.");

            _services = services;
            try { Register(); }
            finally { _services = null; }
        }

        /// <summary>The service collection, available only while <see cref="Register"/> runs.</summary>
        protected IServiceCollection Services =>
            _services ?? throw new InvalidOperationException("Services are available only inside Register().");

        /// <summary>Registers a device. The node resolves every registered device and configures it.</summary>
        protected void AddDevice<TDevice>() where TDevice : class, IDevice =>
            Services.AddSingleton<IDevice, TDevice>();

        /// <summary>Registers a device created by a factory, for example to pass constructor arguments DI can't supply.</summary>
        protected void AddDevice<TDevice>(Func<IServiceProvider, TDevice> factory) where TDevice : class, IDevice
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            Services.AddSingleton<IDevice>(factory);
        }

        /// <summary>Registers a singleton service shared by the plugin's devices and controllers.</summary>
        protected void AddService<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService =>
            Services.AddSingleton<TService, TImplementation>();

        /// <summary>Registers a singleton service created by a factory.</summary>
        protected void AddService<TService>(Func<IServiceProvider, TService> factory) where TService : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            Services.AddSingleton(factory);
        }

        [Obsolete("The node resolves devices from DI and never reads this list. The member is removed in the next major release.")]
        public List<IDevice> Devices { get; } = new List<IDevice>();
    }
}
