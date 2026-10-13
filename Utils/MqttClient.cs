using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Exceptions;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using RIoT2.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RIoT2.Core.Utils
{
    public class MqttClient : IDisposable
    {
        private IManagedMqttClient _client;
        private string _serverUrl;
        private string _username;
        private string _password;
        private string _clientId;
        private string[] _clientTopics;
        private readonly int _port;
        private string _willTopic;
        private string _willPayload;
        private bool _willRetain;
        private readonly SemaphoreSlim _stopGate = new SemaphoreSlim(1, 1);

        public event MqttMessageReceivedHandler MessageReceived;
        public event Func<MqttEventArgs, Task> MessageReceivedAsync;
        public event Func<Task> ConnectedAsync;
        public MqttClient(string clientId, string serverUrl, string username, string password)
            : this(clientId, serverUrl, username, password, 1883)
        {
        }

        public MqttClient(string clientId, string serverUrl, string username, string password, int port)
        {
            _clientId = clientId;
            _serverUrl = serverUrl;
            _username = username;
            _password = password;
            _port = port;
            _willTopic = Constants.Get(clientId, MqttTopic.NodeOnline);
            _willPayload = Json.Serialize(new NodeOnlineMessage() { NodeBaseUrl = "", IsOnline = false });
        }

        /// <summary>
        /// Replaces the default last will (<c>riot2/node/{clientId}/online</c> with an offline
        /// <see cref="NodeOnlineMessage"/>, not retained). The broker publishes the last will when the
        /// connection drops, and <see cref="Stop"/> publishes the same message on a graceful stop.
        /// Call it before <see cref="Start"/>.
        /// </summary>
        public void SetLastWill(string topic, string payload, bool retain)
        {
            if (string.IsNullOrEmpty(topic))
                throw new ArgumentException("The last-will topic is required.", nameof(topic));
            if (_client?.IsStarted == true)
                throw new InvalidOperationException("Set the last will before the MQTT client is started.");
            _willTopic = topic;
            _willPayload = payload ?? "";
            _willRetain = retain;
        }

        public async Task Start(params string[] topic)
        {
            _clientTopics = topic;
            if (_client?.IsStarted == true)
                throw new InvalidOperationException("The MQTT client has already been started.");
            _client?.Dispose();
            await startClient(topic);
        }

        /// <summary>
        /// Publishes the last will and stops the client. Concurrent calls are serialized: a host that
        /// is stopped twice (by its owner, and by <c>Run</c>) calls it twice at the same time.
        /// </summary>
        public async Task Stop()
        {
            if (_client == null)
                return;

            await _stopGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var client = _client;
                if (client == null || !client.IsStarted)
                    return;

                // Publish the last will ourselves: the broker sends it only when the connection drops.
                try
                {
                    if (client.IsConnected)
                    {
                        var offline = new MqttApplicationMessageBuilder()
                            .WithTopic(_willTopic)
                            .WithPayload(_willPayload)
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce);
                        if (_willRetain)
                            offline.WithRetainFlag();
                        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                            await client.InternalClient.PublishAsync(offline.Build(), timeout.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception x) when (x is MqttCommunicationException || x is OperationCanceledException)
                {
                    // The connection dropped or the broker didn't answer, so the broker publishes the last will.
                }
                finally
                {
                    await client.StopAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                _stopGate.Release();
            }
        }

        private async Task handleMqttMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            //e.clientId is the client ID of THIS client!

            var message = new MqttEventArgs()
            {
                ClientId = e.ClientId,
                Message = e.ApplicationMessage.ConvertPayloadToString(),
                Topic = e.ApplicationMessage.Topic
            };
            MessageReceived?.Invoke(message);
            var handlers = MessageReceivedAsync;
            if (handlers != null)
                foreach (Func<MqttEventArgs, Task> handler in handlers.GetInvocationList())
                    await handler(message).ConfigureAwait(false);
        }

        public async Task Publish(string topic, string payload, bool retain = false)
        {
            var msgbuilder = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce);

            if (retain)
                msgbuilder.WithRetainFlag();

            var message = msgbuilder.Build();
            await _client.EnqueueAsync(message);
        }

        private async Task<IManagedMqttClient> startClient(params string[] topics)
        {
            var options = new ManagedMqttClientOptionsBuilder()
                    .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                    .WithClientOptions(new MqttClientOptionsBuilder()
                        .WithClientId(_clientId)
                        .WithTcpServer(_serverUrl, _port)
                        .WithWillTopic(_willTopic)
                        .WithWillPayload(Encoding.UTF8.GetBytes(_willPayload))
                        .WithWillRetain(_willRetain)
                        .WithCredentials(_username, _password))
                    .Build();

            var mqttClient = new MqttFactory().CreateManagedMqttClient();
            _client = mqttClient;
            mqttClient.ApplicationMessageReceivedAsync += handleMqttMessageReceived;
            mqttClient.ConnectedAsync += async _ =>
            {
                var handlers = ConnectedAsync;
                if (handlers != null)
                    foreach (Func<Task> handler in handlers.GetInvocationList())
                        await handler();
            };

            var topicFilters = new List<MqttTopicFilter>();
            foreach (var t in topics)
                topicFilters.Add(new MqttTopicFilterBuilder().WithTopic(t).Build());

            await mqttClient.SubscribeAsync(topicFilters);
            await mqttClient.StartAsync(options);
            return mqttClient;
        }

        public void Dispose()
        {
            if (_client == null)
                return;

            try
            {
                if (_client.IsStarted)
                    Stop().GetAwaiter().GetResult();
            }
            finally
            {
                _client.Dispose();
                _client = null;
            }
        }

        public static bool IsMatch(string topic, string topicFilter)
        {
            return MqttTopicFilterComparer.Compare(topic, topicFilter) == MqttTopicFilterCompareResult.IsMatch;
        }

        public bool IsConnected()
        {
            return _client == null ? false : _client.IsConnected;
        }
    }
}
