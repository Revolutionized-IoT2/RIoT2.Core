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
        public const string TlsVariable = "RIOT2_MQTT_TLS";
        public const string CaFileVariable = "RIOT2_MQTT_CA_FILE";

        /// <summary>Broker host name or IP address, without scheme or port.</summary>
        public string Host { get; set; } = null!;

        /// <summary>Broker user; <c>null</c> connects anonymously.</summary>
        public string? Username { get; set; }

        /// <summary>Broker password; <c>null</c> when not set.</summary>
        public string? Password { get; set; }

        /// <summary>Broker TCP port; 1883 (8883 with TLS) unless <c>RIOT2_MQTT_PORT</c> is set.</summary>
        public int Port { get; set; } = MqttConfiguration.DefaultPort;

        /// <summary>
        /// <c>RIOT2_MQTT_TLS</c>: connect over TLS. Independent of the security mode; plain TCP unless
        /// set (design 7.5).
        /// </summary>
        public bool UseTls { get; set; }

        /// <summary><c>RIOT2_MQTT_CA_FILE</c>: the CA certificate the broker's certificate must chain to; needs TLS.</summary>
        public string? CaFile { get; set; }

        /// <summary>
        /// Reads <c>RIOT2_MQTT_IP</c> (required), <c>RIOT2_MQTT_PORT</c>, <c>RIOT2_MQTT_USERNAME</c>,
        /// <c>RIOT2_MQTT_PASSWORD</c>, <c>RIOT2_MQTT_TLS</c> and <c>RIOT2_MQTT_CA_FILE</c>.
        /// </summary>
        public static MqttOptions Read(SettingsReader settings)
        {
            var useTls = settings.Flag(TlsVariable);
            var options = new MqttOptions
            {
                Host = settings.Required(HostVariable),
                Port = settings.Integer(PortVariable, useTls ? MqttConfiguration.DefaultTlsPort : MqttConfiguration.DefaultPort, 1, 65535),
                Username = settings.Optional(UsernameVariable),
                Password = settings.Optional(PasswordVariable),
                UseTls = useTls,
                CaFile = settings.Optional(CaFileVariable)?.Trim()
            };
            if (string.IsNullOrEmpty(options.CaFile))
                options.CaFile = null;
            else if (!useTls)
                settings.AddError($"{CaFileVariable} needs {TlsVariable}=true");
            else if (!System.IO.File.Exists(options.CaFile))
                settings.AddError($"{CaFileVariable} names a file that doesn't exist: {options.CaFile}");
            return options;
        }

        /// <summary>The runtime MQTT settings for a client that uses <paramref name="clientId"/>.</summary>
        public MqttConfiguration ToConfiguration(string clientId) => new MqttConfiguration
        {
            ClientId = clientId,
            ServerUrl = Host,
            Port = Port,
            Username = Username,
            Password = Password,
            UseTls = UseTls,
            CaFile = CaFile
        };
    }
}
