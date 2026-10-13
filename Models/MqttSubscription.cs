using System;

namespace RIoT2.Core.Models
{
    /// <summary>A topic filter and the quality of service to receive it with.</summary>
    public sealed class MqttSubscription
    {
        public MqttSubscription(string topic, MqttQos qos = MqttQos.AtLeastOnce)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("A topic filter is required.", nameof(topic));
            Topic = topic;
            Qos = qos;
        }

        public string Topic { get; }

        public MqttQos Qos { get; }
    }
}
