using RIoT2.Core.Models;

namespace RIoT2.Core.Configuration
{
    /// <summary>
    /// The MQTT broker settings shared by the Orchestrator, the Node, Elsa and the connectors.
    /// </summary>
    public sealed class MqttOptions
    {
        public const string HostVariable = "RIOT2_MQTT_IP";
        public const string UsernameVariable = "RIOT2_MQTT_USERNAME";
        public const string PasswordVariable = "RIOT2_MQTT_PASSWORD";
        public const string PortVariable = "RIOT2_MQTT_PORT";

        /// <summary>Broker host name or IP address, without scheme or port.</summary>
        public string Host { get; set; }

        /// <summary>Broker user; <c>null</c> connects anonymously.</summary>
        public string Username { get; set; }

        /// <summary>Broker password; <c>null</c> when not set.</summary>
        public string Password { get; set; }

        /// <summary>Broker TCP port; 1883 unless <c>RIOT2_MQTT_PORT</c> is set.</summary>
        public int Port { get; set; } = MqttConfiguration.DefaultPort;

        /// <summary>
        /// Reads <c>RIOT2_MQTT_IP</c> (required), <c>RIOT2_MQTT_PORT</c>, <c>RIOT2_MQTT_USERNAME</c> and
        /// <c>RIOT2_MQTT_PASSWORD</c>.
        /// </summary>
        public static MqttOptions Read(SettingsReader settings) => new MqttOptions
        {
            Host = settings.Required(HostVariable),
            Port = settings.Integer(PortVariable, MqttConfiguration.DefaultPort, 1, 65535),
            Username = settings.Optional(UsernameVariable),
            Password = settings.Optional(PasswordVariable)
        };

        /// <summary>The runtime MQTT settings for a client that uses <paramref name="clientId"/>.</summary>
        public MqttConfiguration ToConfiguration(string clientId) => new MqttConfiguration
        {
            ClientId = clientId,
            ServerUrl = Host,
            Port = Port,
            Username = Username,
            Password = Password
        };
    }
}
