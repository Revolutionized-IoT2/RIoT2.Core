using RIoT2.Core.Models;

namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// Represents a message (report or command) exchanged over MQTT, carrying an identifier and a value payload.
    /// </summary>
    public interface IMessage
    {
        /// <summary>
        /// Gets or sets the unique identifier of the message.
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// Gets the value payload carried by the message.
        /// </summary>
        ValueModel Value { get; }
    }
}
