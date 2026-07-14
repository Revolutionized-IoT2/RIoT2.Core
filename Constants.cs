using System.Reflection;

namespace RIoT2.Core
{
    /// <summary>
    /// Defines shared MQTT topic templates and API endpoint URLs, and provides helpers to build and
    /// parse topic strings.
    /// </summary>
    public static class Constants
    {
        //MQTT topics 
        private static readonly string nodeOnlineTopic = "riot2/node/{id}/online";
        private static readonly string orchestratorOnlineTopic = "riot2/orchestrator/online";
        private static readonly string configurationTopic = "riot2/node/{id}/configuration";
        private static readonly string commandTopic = "riot2/node/{id}/command";
        private static readonly string reportTopic = "riot2/node/{id}/report";

        /// <summary>The API endpoint template for retrieving a node's configuration.</summary>
        public static readonly string ApiConfigurationUrl = "/api/nodes/{id}/configuration";

        /// <summary>The API endpoint for retrieving device configuration templates.</summary>
        public static readonly string ApiConfigurationTemplateUrl = "/api/device/configuration/templates";

        /// <summary>The API endpoint for retrieving device status.</summary>
        public static readonly string ApiDeviceStateUrl = "/api/device/status";

        /// <summary>The API endpoint template for triggering a workflow.</summary>
        public static readonly string ApiWorkflowTriggerUrl = "/riot/trigger/{id}";

        /// <summary>
        /// Builds the MQTT topic string for the specified topic and identifier.
        /// </summary>
        /// <param name="id">The identifier to substitute into the topic template.</param>
        /// <param name="topic">The topic whose template should be used.</param>
        /// <returns>The resolved topic string.</returns>
        public static string Get(string id, MqttTopic topic)
        {
            return getTemplate(topic).Replace("{id}", id);
        }

        /// <summary>
        /// Extracts the identifier segment from a topic string using the specified topic template.
        /// </summary>
        /// <param name="topicString">The concrete topic string to parse.</param>
        /// <param name="topic">The topic whose template defines the identifier position.</param>
        /// <returns>The extracted identifier, or an empty string if it cannot be resolved.</returns>
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
