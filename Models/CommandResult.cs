using Newtonsoft.Json;
using RIoT2.Core.Utils;
using Stj = System.Text.Json.Serialization;

namespace RIoT2.Core.Models
{
    /// <summary>
    /// What a node did with one command delivery (design 7.1), published on
    /// <c>riot2/node/{id}/command/result</c> by nodes that advertise
    /// <see cref="Contract.Capabilities.CommandResult"/>.
    /// </summary>
    public class CommandResult
    {
        /// <summary>The <see cref="Command.CorrelationId"/> of the delivery this result answers.</summary>
        public string CorrelationId { get; set; }

        /// <summary>The <see cref="Command.Id"/> (command template id).</summary>
        public string CommandId { get; set; }

        /// <summary>One of <see cref="CommandResultStatus"/>.</summary>
        public string Status { get; set; }

        /// <summary>Why the command failed or was rejected. Omitted when <c>null</c>.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public string Error { get; set; }

        /// <summary>When the node decided, as Unix epoch seconds.</summary>
        public long TimeStamp { get; set; }

        /// <summary>Contract version of the payload; <see cref="Contract.Version"/> for new results.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public int? ContractVersion { get; set; } = Contract.Version;

        /// <summary>Creates a result from its JSON, or returns <c>null</c> if it can't be parsed.</summary>
        public static CommandResult Create(string json)
        {
            try
            {
                return Json.Deserialize<CommandResult>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The JSON published on MQTT, without null properties.</summary>
        public string ToJson() => Json.SerializeIgnoreNulls(this);
    }

    /// <summary>The values of <see cref="CommandResult.Status"/>, as sent on the wire.</summary>
    public static class CommandResultStatus
    {
        /// <summary>The device ran the command without an exception. This is not a physical confirmation: that comes with the device's next report.</summary>
        public const string Executed = "executed";

        /// <summary>The device threw while running the command.</summary>
        public const string Failed = "failed";

        /// <summary>The node didn't run the command, for example because its command queue is full.</summary>
        public const string Rejected = "rejected";

        /// <summary>No device on the node owns the command id.</summary>
        public const string UnknownCommand = "unknownCommand";

        /// <summary>The correlation id was seen recently, so the command was not run again.</summary>
        public const string Duplicate = "duplicate";
    }
}
