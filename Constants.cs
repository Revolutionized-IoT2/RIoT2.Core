using System.Reflection;

namespace RIoT2.Core
{
    public static class Constants
    {
        //MQTT topics 
        private static readonly string nodeOnlineTopic = "riot2/node/{id}/online";
        private static readonly string orchestratorOnlineTopic = "riot2/orchestrator/online";
        private static readonly string configurationTopic = "riot2/node/{id}/configuration";
        private static readonly string commandTopic = "riot2/node/{id}/command";
        private static readonly string reportTopic = "riot2/node/{id}/report";

        //endpoints used by nodes
        public static readonly string ApiConfigurationUrl = "/api/nodes/{id}/configuration";
        public static readonly string ApiConfigurationTemplateUrl = "/api/device/configuration/templates";
        public static readonly string ApiDeviceStateUrl = "/api/device/status";
        public static readonly string ApiWorkflowTriggerUrl = "/riot/trigger/{id}";

        public static string Get(string id, MqttTopic topic)
        {
            return getTemplate(topic).Replace("{id}", id);
        }
        public static string GetTopicId(string topicString, MqttTopic topic)
        {
            string template = getTemplate(topic);
            var topicArr = topicString.Split('/');
            var templateArr = template.Split('/');

            if (topicArr.Length != templateArr.Length)
                return "";

            for (int i = 0; i < templateArr.Length; i++)
            {
                if (templateArr[i] == "{id}")
                    return topicArr[i];
            }

            return "";
        }

        private static string getTemplate(MqttTopic topic)
        {
            switch (topic)
            {
                case MqttTopic.Report:
                    return reportTopic;
                case MqttTopic.Command:
                    return commandTopic;
                case MqttTopic.Configuration:
                    return configurationTopic;
                case MqttTopic.NodeOnline:
                    return nodeOnlineTopic;
                case MqttTopic.OrchestratorOnline:
                    return orchestratorOnlineTopic;
                default:
                    return "";
            }
        }
    }
}
