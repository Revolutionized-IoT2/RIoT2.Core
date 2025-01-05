namespace RIoT2.Core.Models
{
    public class NodeConfiguration
    {
        public string Id { get; set; }
        public string MqttServerUrl { get; set; }
        public string MqttUsername { get; set; }
        public string MqttPassword { get; set; }
        public string Url { get; set; }
        public string GetTopic(MqttTopic topic) 
        {
            return Constants.Get(Id, topic);
        }
    }
}