namespace RIoT2.Core.Models
{
    public class OrchestratorConfiguration
    {
        public string Id { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public bool UseExtWorkflowEngine { get; set; } = false;
        public MqttConfiguration Mqtt { get; set; }
        public QnapConfiguration Qnap{ get; set; }
        public PackageManifest OrchestratorManifest { get; set; }
    }

    public class MqttConfiguration 
    {
        public string ClientId { get; set; }
        public string ServerUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class QnapConfiguration
    {
        public string ServerUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}