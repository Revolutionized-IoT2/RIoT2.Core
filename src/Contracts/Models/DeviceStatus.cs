namespace RIoT2.Core.Models
{
    public class DeviceStatus
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Message { get; set; }
        public DeviceState State { get; set; }

        /// <summary>
        /// Whether the device is linked to its account, for devices that sign in to one (Core 2.4.0). Omitted when
        /// <c>null</c>.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public DeviceAuthorizationStatus? Authorization { get; set; }
    }
}