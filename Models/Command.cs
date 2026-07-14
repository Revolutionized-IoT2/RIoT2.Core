using RIoT2.Core.Interfaces;
using RIoT2.Core.Utils;

namespace RIoT2.Core.Models
{
    /// <summary>
    /// Represents a command targeted at a device, carrying a value payload and exchanged as JSON over MQTT.
    /// </summary>
    public class Command : ICommand, IMessage
    {
        /// <summary>
        /// Gets or sets the unique identifier of the command.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the value payload of the command.
        /// </summary>
        public ValueModel Value { get; set; }

        /// <summary>
        /// Creates a <see cref="Command"/> from its JSON representation.
        /// </summary>
        /// <param name="json">The JSON payload representing the command.</param>
        /// <returns>The deserialized command.</returns>
        public static Command Create(string json)
        {
            return Json.Deserialize<Command>(json);
        }
    }
}
