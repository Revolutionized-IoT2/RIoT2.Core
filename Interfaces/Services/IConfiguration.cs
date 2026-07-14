using RIoT2.Core.Models;

namespace RIoT2.Core.Interfaces.Services
{
    /// <summary>
    /// Represents the runtime configuration shared by nodes and the orchestrator.
    /// </summary>
    public interface IConfiguration
    {
        /// <summary>
        /// Gets or sets the unique identifier of the configured component.
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// Gets or sets the base API URL used by the component.
        /// </summary>
        string Url { get; set; }

        /// <summary>
        /// Gets or sets the MQTT broker connection settings.
        /// </summary>
        MqttConfiguration Mqtt { get; set; }

        /// <summary>
        /// Gets the root folder of the running application.
        /// </summary>
        string ApplicationFolder { get; }
    }
}