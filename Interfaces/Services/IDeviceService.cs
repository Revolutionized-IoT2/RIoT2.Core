using RIoT2.Core.Interfaces;
using System.Collections.Generic;

namespace RIoT2.Core.Interfaces.Services
{
    /// <summary>
    /// Manages the lifecycle of devices and provides lookups by report or command identifier.
    /// </summary>
    public interface IDeviceService
    {
        /// <summary>
        /// Occurs when the set of managed devices changes.
        /// </summary>
        event DeviceServiceUpdatedHandler DevicesUpdated;

        /// <summary>
        /// Gets the currently managed devices.
        /// </summary>
        List<IDevice> Devices { get; }

        /// <summary>
        /// Gets the command-capable device that handles the specified command identifier.
        /// </summary>
        /// <param name="commandId">The command identifier to look up.</param>
        /// <returns>The matching device, or <c>null</c> if none is found.</returns>
        ICommandDevice GetDeviceByCommandId(string commandId);

        /// <summary>
        /// Gets the device that produces the specified report identifier.
        /// </summary>
        /// <param name="reportId">The report identifier to look up.</param>
        /// <returns>The matching device, or <c>null</c> if none is found.</returns>
        IDevice GetDeviceByReportId(string reportId);

        /// <summary>
        /// Configures all devices from the current configuration.
        /// </summary>
        void ConfigureDevices();

        /// <summary>
        /// Starts all managed devices.
        /// </summary>
        /// <param name="restartDevicesInErrorState">If <c>true</c>, also restarts devices currently in an error state.</param>
        void StartAllDevices(bool restartDevicesInErrorState = false);

        /// <summary>
        /// Stops all managed devices.
        /// </summary>
        void StopAllDevices();
    }
}