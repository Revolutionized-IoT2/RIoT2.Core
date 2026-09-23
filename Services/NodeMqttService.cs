using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;
using RIoT2.Core.Utils;
using RIoT2.Core.Interfaces;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Collections.Generic;

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
        private SemaphoreSlim _legacyCommandGate;
        private readonly Func<MqttClient> _clientFactory;
        private const int MaxPendingCommands = 64;
        private readonly object _commandGate = new object();
        private readonly List<Task> _commands = new List<Task>();
        private bool _acceptingCommands;

        public NodeMqttService(INodeConfigurationService configurationService, ICommandService commandService, IReportService reportService, ILogger<NodeMqttService> logger)
            : this(configurationService, commandService, reportService, logger, () => new MqttClient(
                configurationService.Configuration.Mqtt.ClientId,
                configurationService.Configuration.Mqtt.ServerUrl,
                configurationService.Configuration.Mqtt.Username,
                configurationService.Configuration.Mqtt.Password))
        {
        }

        public NodeMqttService(INodeConfigurationService configurationService, ICommandService commandService,
            IReportService reportService, ILogger<NodeMqttService> logger, Func<MqttClient> clientFactory)
        {
            _logger = logger;
            _configurationService = configurationService;
            _commandService = commandService;
            _reportService = reportService;
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
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

                _client = _clientFactory();
                _shutdown = new CancellationTokenSource();
                _legacyCommandGate = new SemaphoreSlim(1, 1);
                lock (_commandGate)
                {
                    _commands.Clear();
                    _acceptingCommands = true;
                }

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
                    DispatchCommand(mqttEventArgs.Message);
                if (MqttClient.IsMatch(mqttEventArgs.Topic, _orchestratorOnlineTopic))
                    await AnnounceOnlineAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { _logger.LogDebug("Node MQTT work was cancelled"); }
            catch (Exception error) { _logger.LogError(error, "Could not process node MQTT message on {Topic}", mqttEventArgs.Topic); }
        }

        private void DispatchCommand(string message)
        {
            lock (_commandGate)
            {
                _commands.RemoveAll(task => task.IsCompleted);
                if (!_acceptingCommands || _commands.Count >= MaxPendingCommands)
                {
                    _logger.LogWarning("Node MQTT command rejected: stopping or pending command limit {Limit} reached; no retry", MaxPendingCommands);
                    return;
                }
                // Capture the device generation now, without blocking configuration messages behind I/O.
                _commands.Add(ExecuteCommandAsync(message, _shutdown.Token));
            }
        }

        private async Task ExecuteCommandAsync(string message, CancellationToken cancellationToken)
        {
            try
            {
                if (_commandService is IAsyncCommandService asynchronous)
                    await asynchronous.ExecuteJsonCommandAsync(message, cancellationToken).ConfigureAwait(false);
                else
                {
                    await _legacyCommandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        await Task.Run(() => _commandService.ExecuteJsonCommand(message), cancellationToken).ConfigureAwait(false);
                    }
                    finally { _legacyCommandGate.Release(); }
                }
            }
            catch (OperationCanceledException) { _logger.LogDebug("Node MQTT command was cancelled"); }
            catch (Exception error) { _logger.LogError(error, "Could not execute node MQTT command"); }
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

            Task[] commands;
            lock (_commandGate)
            {
                _acceptingCommands = false;
                commands = _commands.ToArray();
            }
            _shutdown.Cancel();
            _client.MessageReceivedAsync -= _client_MessageReceived;
            _client.ConnectedAsync -= AnnounceOnlineAsync;
            _reportService.ReportUpdated -= _reportService_ReportUpdated;

            try
            {
                await Task.WhenAll(commands).ConfigureAwait(false);
                await _client.Stop().ConfigureAwait(false);
            }
            finally
            {
                _client.Dispose();
                _client = null;
                _shutdown.Dispose();
                _legacyCommandGate.Dispose();
            }
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