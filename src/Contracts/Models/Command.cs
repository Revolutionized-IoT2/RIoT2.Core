using Newtonsoft.Json;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Utils;
using Stj = System.Text.Json.Serialization;

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
        /// Optional id of this delivery, set by the orchestrator's command outbox (design 7.1). A node
        /// that advertises <see cref="Contract.Capabilities.CommandResult"/> echoes it in its
        /// <see cref="CommandResult"/> and runs a repeated id only once. Omitted when <c>null</c>.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public string CorrelationId { get; set; }

        /// <summary>
        /// Optional contract version of the payload (<see cref="Contract.Version"/>); absent means 1.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public int? ContractVersion { get; set; }

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
