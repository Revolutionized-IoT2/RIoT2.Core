using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using RIoT2.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
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

        public event MqttMessageReceivedHandler MessageReceived;
        public MqttClient(string clientId, string serverUrl, string username, string password)
        {
            _clientId = clientId;
            _serverUrl = serverUrl;
            _username = username;
            _password = password;
        }

        public async Task Start(params string[] topic)
        {
            _clientTopics = topic;
            _client = await startClient(topic);
            _client.ApplicationMessageReceivedAsync += e =>
            {
                handleMqttMessageReceived(e);
                return Task.CompletedTask;
            };
        }

        public async Task Stop()
        {
            if (_client == null)
                return;

            if (_client.IsStarted)
                await _client.StopAsync();
        }

        private void handleMqttMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            //e.clientId is the client ID of THIS client!

            MessageReceived(new MqttEventArgs()
            {
                ClientId = e.ClientId,
                Message = e.ApplicationMessage.ConvertPayloadToString(),
                Topic = e.ApplicationMessage.Topic
            });
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
                ConfigurationTemplateUrl = "",
                DeviceStateUrl = "",
                IsOnline = false
            };

            var options = new ManagedMqttClientOptionsBuilder()
                    .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                    .WithClientOptions(new MqttClientOptionsBuilder()
                        .WithClientId(_clientId)
                        .WithTcpServer(_serverUrl)
                        .WithWillTopic(Constants.Get(_clientId, MqttTopic.NodeOnline))
                        .WithWillPayload(Encoding.UTF8.GetBytes(Json.Serialize(lwMsg)))
                        .WithCredentials(_username, _password))
                    .Build();

            var mqttClient = new MqttFactory().CreateManagedMqttClient();
            var topicFilters = new List<MqttTopicFilter>();
            foreach (var t in topics)
                topicFilters.Add(new MqttTopicFilterBuilder().WithTopic(t).Build());

            await mqttClient.SubscribeAsync(topicFilters);
            await mqttClient.StartAsync(options);

            return mqttClient;
        }

        public async void Dispose()
        {
            if (_client == null)
                return;

            if (_client.IsStarted)
                await Stop();

            _client?.Dispose();
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
