namespace RIoT2.Core.Models
{
    /// <summary>
    /// One parameter in <see cref="DeviceConfiguration.ParameterDefinitions"/> (Core 1.16.0): what the device reads
    /// from <see cref="DeviceConfiguration.DeviceParameters"/> under <see cref="Key"/>.
    /// </summary>
    public class DeviceParameterDefinition
    {
        /// <summary>The key in <c>deviceParameters</c>.</summary>
        public string Key { get; set; }

        public DeviceParameterType Type { get; set; }

        /// <summary>The device doesn't start without a value.</summary>
        public bool Required { get; set; }

        /// <summary>A password, key or token: the UI masks it. It is still stored as plain text.</summary>
        public bool Secret { get; set; }

        /// <summary>Used when the parameter is missing or empty. Omitted when <c>null</c>.</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? Default { get; set; }

        /// <summary>Help text for the UI. Omitted when <c>null</c>.</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }
    }
}
