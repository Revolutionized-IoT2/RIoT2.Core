namespace RIoT2.Core.Models
{
    /// <summary>
    /// The runtime settings of one <see cref="Utils.MqttClient"/>.
    /// </summary>
    public class MqttConfiguration
    {
        public const int DefaultPort = 1883;
        public const int DefaultKeepAliveSeconds = 15;
        public const int DefaultMaxPendingMessages = 10000;

        public string ClientId { get; set; }
        public string ServerUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        /// <summary>Broker TCP port (<c>RIOT2_MQTT_PORT</c>).</summary>
        public int Port { get; set; } = DefaultPort;

        /// <summary>Seconds between keep-alive pings when nothing else is sent.</summary>
        public int KeepAliveSeconds { get; set; } = DefaultKeepAliveSeconds;

        /// <summary>
        /// Outgoing messages kept while the broker is unreachable. When the queue is full the oldest
        /// message is dropped, counted and logged.
        /// </summary>
        public int MaxPendingMessages { get; set; } = DefaultMaxPendingMessages;
    }
}
