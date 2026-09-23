using RIoT2.Core.Models;
using RIoT2.Core.Models.Matter;
using System.Collections.Generic;

namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// Implemented by a device that wants to be exposed through the RIoT Control Bridge as one or more
    /// Matter endpoints, so it can be seen and controlled from a Matter ecosystem such as Google Home.
    /// </summary>
    /// <remarks>
    /// The declaration is data, not behaviour: the device runs on a RIoT node while the Matter bridge runs
    /// in the orchestrator, so the returned templates travel with the device configuration over the
    /// existing configuration-template path and are translated into Matter clusters by the orchestrator.
    /// A device keeps handling its normal <see cref="Models.Command"/> and <see cref="Models.Report"/>
    /// traffic; the bindings simply describe which of those map to which cluster attributes.
    /// </remarks>
    public interface IMatterDevice : IDevice
    {
        /// <summary>
        /// Gets the Matter endpoints this device should be exposed as.
        /// </summary>
        /// <param name="configuration">
        /// The configuration the declaration is built against - the same instance the caller obtained from
        /// <see cref="IDeviceWithConfiguration.GetConfigurationTemplate"/>. Devices that mint template ids
        /// on each call must read the report and command template ids from this instance, so that
        /// <see cref="MatterAttributeBinding.ReportTemplateId"/> and
        /// <see cref="MatterCommandBinding.CommandTemplateId"/> resolve against the configuration that is
        /// actually persisted.
        /// </param>
        /// <returns>
        /// One template per Matter endpoint, or an empty sequence when the device currently has nothing to
        /// expose. Never <c>null</c>.
        /// </returns>
        IEnumerable<MatterEndpointTemplate> GetMatterEndpoints(DeviceConfiguration configuration);
    }
}
