using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;
using RIoT2.Core.Utils;
using RIoT2.Core.Interfaces;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RIoT2.Core.Services
{
    public class NodeMqttService : INodeMqttService
    {
        private MqttClient _client;
        private ICommandService _commandService;
        private IReportService _reportService;
        private INodeConfigurationService _configurationService;
        private string _reportTopic;
        private string _commandTopic;
        private string _configurationTopic;
        private string _orchestratorOnlineTopic;
        private string _nodeOnlineTopic;
        //private ILogger _logger;
        private ILogger<NodeMqttService> _logger;

        public NodeMqttService(INodeConfigurationService configurationService, ICommandService commandService, IReportService reportService, ILogger<NodeMqttService> logger) 
        {
            _logger = logger;
            _configurationService = configurationService;
            _commandService = commandService;
            _reportService = reportService;
        }

        public async Task Start() 
        {
            try
            {
                _client = new MqttClient(_configurationService.Configuration.Mqtt.ClientId,
                    _configurationService.Configuration.Mqtt.ServerUrl,
                    _configurationService.Configuration.Mqtt.Username,
                    _configurationService.Configuration.Mqtt.Password);

                _configurationTopic = _configurationService.Configuration.GetTopic(MqttTopic.Configuration);
                _commandTopic = _configurationService.Configuration.GetTopic(MqttTopic.Command);
                _reportTopic = _configurationService.Configuration.GetTopic(MqttTopic.Report);
                _orchestratorOnlineTopic = _configurationService.Configuration.GetTopic(MqttTopic.OrchestratorOnline);
                _nodeOnlineTopic = _configurationService.Configuration.GetTopic(MqttTopic.NodeOnline);

                _client.MessageReceived += _client_MessageReceived;
                _reportService.ReportUpdated += _reportService_ReportUpdated;

                await _client.Start(new string[] { _commandTopic, _configurationTopic, _orchestratorOnlineTopic });
            }
            catch (Exception x) 
            {
                throw new Exception("Could not start MQTT Broker", x);
            }
        }

        public async Task SendCommand(string topic, string value)
        {
            await _client.Publish(topic, value);
        }

        private void _client_MessageReceived(MqttEventArgs mqttEventArgs)
        {
            //Do not listen configuration when in debug -> configuration is loaded from local file in DEBUG! 

            if (MqttClient.IsMatch(mqttEventArgs.Topic, _configurationTopic)) 
            {
                #if DEBUG
                _logger.LogWarning($"Received topic {mqttEventArgs.Topic}, but skipped because DEBUG");
                #else
                _configurationService.LoadDeviceConfiguration(mqttEventArgs.Message, _configurationService.Configuration.Id);
                #endif
            }

            if (MqttClient.IsMatch(mqttEventArgs.Topic, _commandTopic))
                _commandService.ExecuteJsonCommand(mqttEventArgs.Message);

            if (MqttClient.IsMatch(mqttEventArgs.Topic, _orchestratorOnlineTopic))
                _ = SendNodeOnlineMessage(_configurationService.OnlineMessage);
        }

        private async void _reportService_ReportUpdated(IDevice sender, IReport report)
        {
            await _client.Publish(_reportTopic, Json.SerializeIgnoreNulls(report as Report));
        }

        public async Task Stop()
        {
            await _client.Stop();
        }

        public async Task SendNodeOnlineMessage(NodeOnlineMessage msg)
        {
            await SendCommand(_nodeOnlineTopic, Json.SerializeIgnoreNulls(msg));
        }

        public bool IsConnected()
        {
            return _client.IsConnected();
        }
    }
}