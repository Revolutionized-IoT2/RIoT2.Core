using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;
using RIoT2.Core.Utils;
using RIoT2.Core.Interfaces;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Threading;

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
        private CancellationTokenSource _shutdown;

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
                if (_client != null)
                {
                    _logger.LogWarning("MQTT service is already started; ignoring duplicate Start call.");
                    return;
                }

                _client = new MqttClient(_configurationService.Configuration.Mqtt.ClientId,
                    _configurationService.Configuration.Mqtt.ServerUrl,
                    _configurationService.Configuration.Mqtt.Username,
                    _configurationService.Configuration.Mqtt.Password);
                _shutdown = new CancellationTokenSource();

                _configurationTopic = _configurationService.Configuration.GetTopic(MqttTopic.Configuration);
                _commandTopic = _configurationService.Configuration.GetTopic(MqttTopic.Command);
                _reportTopic = _configurationService.Configuration.GetTopic(MqttTopic.Report);
                _orchestratorOnlineTopic = _configurationService.Configuration.GetTopic(MqttTopic.OrchestratorOnline);
                _nodeOnlineTopic = _configurationService.Configuration.GetTopic(MqttTopic.NodeOnline);

                _client.MessageReceivedAsync += _client_MessageReceived;
                _client.ConnectedAsync += AnnounceOnlineAsync;
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
            if (_client == null)
                throw new InvalidOperationException("Cannot send command before the MQTT service is started.");

            await _client.Publish(topic, value);
        }

        private async Task _client_MessageReceived(MqttEventArgs mqttEventArgs)
        {
            try
            {
                if (MqttClient.IsMatch(mqttEventArgs.Topic, _configurationTopic))
                {
#if DEBUG
                    _logger.LogWarning("Received topic {Topic}, but skipped because DEBUG", mqttEventArgs.Topic);
#else
                    await _configurationService.LoadDeviceConfiguration(mqttEventArgs.Message, _configurationService.Configuration.Id).ConfigureAwait(false);
#endif
                }
                if (MqttClient.IsMatch(mqttEventArgs.Topic, _commandTopic))
                {
                    if (_commandService is IAsyncCommandService asynchronous)
                        await asynchronous.ExecuteJsonCommandAsync(mqttEventArgs.Message, _shutdown.Token).ConfigureAwait(false);
                    else
                        _commandService.ExecuteJsonCommand(mqttEventArgs.Message);
                }
                if (MqttClient.IsMatch(mqttEventArgs.Topic, _orchestratorOnlineTopic))
                    await AnnounceOnlineAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { _logger.LogDebug("Node MQTT work was cancelled"); }
            catch (Exception error) { _logger.LogError(error, "Could not process node MQTT message on {Topic}", mqttEventArgs.Topic); }
        }

        private async Task AnnounceOnlineAsync()
        {
            try
            {
                if (_configurationService.OnlineMessage == null)
                    throw new InvalidOperationException("The node online message has not been configured.");
                await SendNodeOnlineMessage(_configurationService.OnlineMessage);
            }
            catch (Exception x)
            {
                _logger.LogError(x, "Could not announce node presence");
            }
        }

        private async void _reportService_ReportUpdated(IDevice sender, IReport report)
        {
            try
            {
                var typedReport = report as Report;
                if (typedReport == null)
                {
                    _logger.LogWarning("ReportUpdated received an unexpected report type; publish skipped.");
                    return;
                }

                if (_client == null)
                {
                    _logger.LogWarning("ReportUpdated received before the MQTT service was started; publish skipped.");
                    return;
                }

                await _client.Publish(_reportTopic, Json.SerializeIgnoreNulls(typedReport));
            }
            catch (Exception x)
            {
                _logger.LogError(x, $"Failed to publish report to topic {_reportTopic}");
            }
        }

        public async Task Stop()
        {
            if (_client == null)
                return;

            _shutdown.Cancel();
            _client.MessageReceivedAsync -= _client_MessageReceived;
            _client.ConnectedAsync -= AnnounceOnlineAsync;
            _reportService.ReportUpdated -= _reportService_ReportUpdated;

            await _client.Stop();
            _client.Dispose();
            _client = null;
            _shutdown.Dispose();
        }

        public async Task SendNodeOnlineMessage(NodeOnlineMessage msg)
        {
            await SendCommand(_nodeOnlineTopic, Json.SerializeIgnoreNulls(msg));
        }

        public bool IsConnected()
        {
            return _client != null && _client.IsConnected();
        }
    }
}