namespace RIoT2.Core.Models
{
    public class MqttEventArgs
    {
        public string Topic { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
