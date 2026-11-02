using System;
using Microsoft.Extensions.Logging;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Plugins;

namespace RIoT2.Core.Abstracts
{
    /// <summary>
    /// A device whose parameters bind to <typeparamref name="TOptions"/> (plan M12 step 4). Before
    /// <see cref="DeviceBase.ConfigureDevice"/> runs, the base class binds <c>deviceParameters</c> to
    /// <see cref="Options"/>; a missing required or invalid value puts the device into
    /// <see cref="DeviceState.Error"/> with one message listing every problem. The node describes the options in
    /// the template's <c>parameterDefinitions</c> and adds their keys to its <c>deviceParameters</c>.
    /// </summary>
    public abstract class DeviceBase<TOptions> : DeviceBase, IDeviceWithOptions where TOptions : class, new()
    {
        protected DeviceBase(ILogger logger) : base(logger) { }

        /// <summary>The bound parameters; set before <see cref="DeviceBase.ConfigureDevice"/> runs.</summary>
        public TOptions Options { get; private set; }

        public Type OptionsType => typeof(TOptions);

        internal override void BindOptions() =>
            Options = (TOptions)DeviceOptions.Bind(typeof(TOptions), Configuration?.DeviceParameters, ParameterRead);
    }

    /// <summary>
    /// <see cref="AsyncDeviceBase"/> with parameters bound to <typeparamref name="TOptions"/>, as in
    /// <see cref="DeviceBase{TOptions}"/>.
    /// </summary>
    public abstract class AsyncDeviceBase<TOptions> : AsyncDeviceBase, IDeviceWithOptions where TOptions : class, new()
    {
        protected AsyncDeviceBase(ILogger logger) : base(logger) { }

        /// <summary>The bound parameters; set before <see cref="DeviceBase.ConfigureDevice"/> runs.</summary>
        public TOptions Options { get; private set; }

        public Type OptionsType => typeof(TOptions);

        internal override void BindOptions() =>
            Options = (TOptions)DeviceOptions.Bind(typeof(TOptions), Configuration?.DeviceParameters, ParameterRead);
    }
}
