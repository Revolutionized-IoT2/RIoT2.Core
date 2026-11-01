namespace RIoT2.Core.Models
{
    public class MqttEventArgs
    {
        public string Topic { get; set; }
        public string ClientId { get; set; }
        public string Message { get; set; }
    }
}
