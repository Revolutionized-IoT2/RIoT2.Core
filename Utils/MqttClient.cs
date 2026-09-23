using MQTTnet;
using MQTTnet.Client;
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
        }

        public async Task Start(params string[] topic)
        {
            _clientTopics = topic;
            if (_client?.IsStarted == true)
                throw new InvalidOperationException("The MQTT client has already been started.");
            _client?.Dispose();
            await startClient(topic);
        }

        public async Task Stop()
        {
            if (_client == null)
                return;

            if (_client.IsStarted) 
            {
                // Send Last Will Message that we are offline (Automatic lw is only sent if shutdow is ungracefull)
                var lwMsg = new NodeOnlineMessage()
                {
                    NodeBaseUrl = "",
                    IsOnline = false
                };
                try
                {
                    if (_client.IsConnected)
                    {
                        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                            await _client.InternalClient.PublishAsync(new MqttApplicationMessageBuilder()
                                .WithTopic(Constants.Get(_clientId, MqttTopic.NodeOnline))
                                .WithPayload(Json.Serialize(lwMsg))
                                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                                .Build(), timeout.Token);
                    }
                }
                finally
                {
                    await _client.StopAsync();
                }
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
            var lwMsg = new NodeOnlineMessage()
            {
                NodeBaseUrl = "",
                IsOnline = false
            };

            var options = new ManagedMqttClientOptionsBuilder()
                    .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                    .WithClientOptions(new MqttClientOptionsBuilder()
                        .WithClientId(_clientId)
                        .WithTcpServer(_serverUrl, _port)
                        .WithWillTopic(Constants.Get(_clientId, MqttTopic.NodeOnline))
                        .WithWillPayload(Encoding.UTF8.GetBytes(Json.Serialize(lwMsg)))
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
