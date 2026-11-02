using System;

namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// A device whose parameters bind to a typed options class (<see cref="Abstracts.DeviceBase{TOptions}"/>,
    /// <see cref="Abstracts.AsyncDeviceBase{TOptions}"/>). The node describes the options in the template's
    /// <c>parameterDefinitions</c>.
    /// </summary>
    public interface IDeviceWithOptions
    {
        /// <summary>The options class, whose properties carry <see cref="Plugins.DeviceParameterAttribute"/>.</summary>
        Type OptionsType { get; }
    }
}
