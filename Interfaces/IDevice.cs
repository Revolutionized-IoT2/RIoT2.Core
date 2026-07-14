using RIoT2.Core.Models;
using System.Collections.Generic;

namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// Represents a device that can be configured, started, stopped, and can raise report updates.
    /// </summary>
    public interface IDevice
    {
        /// <summary>
        /// Gets the current configuration of the device.
        /// </summary>
        DeviceConfiguration Configuration { get; }

        /// <summary>
        /// Gets the current operational state of the device.
        /// </summary>
        DeviceState State { get; }

        /// <summary>
        /// Gets a message describing the current state, typically used for error details.
        /// </summary>
        string StateMessage { get; }

        /// <summary>
        /// Gets the unique identifier of the device.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the display name of the device.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Starts the device so it begins producing reports.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the device.
        /// </summary>
        void Stop();

        /// <summary>
        /// Occurs when the device produces an updated report.
        /// </summary>
        event ReportUpdatedHandler ReportUpdated;

        /// <summary>
        /// Gets the report templates describing the reports the device can produce.
        /// </summary>
        IEnumerable<ReportTemplate> ReportTemplates { get; }

        /// <summary>
        /// Initializes the device with the specified configuration.
        /// </summary>
        /// <param name="configuration">The configuration to apply to the device.</param>
        void Initialize(DeviceConfiguration configuration);
    }
}
