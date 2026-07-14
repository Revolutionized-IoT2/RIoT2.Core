using RIoT2.Core.Interfaces;
using RIoT2.Core.Utils;

namespace RIoT2.Core.Models
{
    /// <summary>
    /// Represents a report produced by a device, carrying a value and metadata, and exchanged as JSON over MQTT.
    /// </summary>
    public class Report : IReport, IMessage
    {
        /// <summary>
        /// Gets or sets the unique identifier of the report.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the creation time of the report, expressed as Unix epoch seconds.
        /// </summary>
        public long TimeStamp { get; set; }

        /// <summary>
        /// Gets or sets the filter that categorizes the report.
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        /// Gets or sets the value payload of the report.
        /// </summary>
        public ValueModel Value 
        {
            get; set;
        }

        /// <summary>
        /// Creates a <see cref="Report"/> from its JSON representation.
        /// </summary>
        /// <param name="json">The JSON payload representing the report.</param>
        /// <returns>The deserialized report, or <c>null</c> if the JSON could not be parsed.</returns>
        public static Report Create(string json) 
        {
            try
            {
                return Json.Deserialize<Report>(json);
            }
            catch   
            {
                return null;
            }
        }

        /// <summary>
        /// Serializes the report to its JSON representation, ignoring null properties.
        /// </summary>
        /// <returns>The JSON representation of the report.</returns>
        public string ToJson()
        {
            return Json.SerializeIgnoreNulls(this);
        }
    }
}
