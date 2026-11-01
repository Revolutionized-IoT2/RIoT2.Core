namespace RIoT2.Core.Models
{
    /// <summary>MQTT quality of service, without exposing MQTTnet types.</summary>
    public enum MqttQos
    {
        /// <summary>QoS 0: delivered at most once; lost if the connection drops.</summary>
        AtMostOnce = 0,

        /// <summary>QoS 1, the RIoT2 default: delivered at least once; a receiver can see a duplicate.</summary>
        AtLeastOnce = 1,

        /// <summary>QoS 2: delivered exactly once, at the cost of a four-way handshake.</summary>
        ExactlyOnce = 2
    }
}
