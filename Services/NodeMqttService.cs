using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;
using RIoT2.Core.Utils;
using RIoT2.Core.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Channels;

namespace RIoT2.Core.Services
{
    /// <summary>
    /// The node's MQTT side: receives configuration and commands, announces presence and publishes
    /// the reports of its devices.
    /// </summary>
    /// <remarks>
    /// Reports go through a bounded queue to one publisher, so they are published in the order the
    /// devices raised them and a slow broker never blocks a device. When the queue is full, the
    /// oldest report is dropped, counted (<see cref="DroppedReports"/>) and logged.
    /// </remarks>
    public class NodeMqttService : INodeMqttService
    {
        /// <summary>Reports that may wait for the publisher before the oldest one is dropped.</summary>
        public const int ReportQueueCapacity = 1000;

        private static readonly TimeSpan DropWarningInterval = TimeSpan.FromMinutes(1);
        private MqttClient _client;
        private ICommandService _commandService;
        private IReportService _reportService;
        private INodeConfigurationService _configurationService;
        private string _reportTopic;
        private string _commandTopic;
        private string _configurationTopic;
        private string _orchestratorOnlineTopic;
        private string _nodeOnlineTopic;
        private string _commandResultTopic;
        //private ILogger _logger;
        private ILogger<NodeMqttService> _logger;
        private CancellationTokenSource _shutdown;
        private SemaphoreSlim _legacyCommandGate;
        private readonly Func<MqttClient> _clientFactory;
        private const int MaxPendingCommands = 64;
        private readonly object _commandGate = new object();
        private readonly List<Task> _commands = new List<Task>();
        private bool _acceptingCommands;
        private Channel<Report> _reports;
        private Task _reportPublisher = Task.CompletedTask;
        private long _droppedReports;
        private long _lastDropWarning;
        private readonly CorrelationCache _correlations = new CorrelationCache();
        // Results of commands that finish while Stop waits for them still go out on this client.
        private MqttClient _resultClient;

        /// <summary>Reports dropped because the report queue was full.</summary>
        public long DroppedReports => Interlocked.Read(ref _droppedReports);

        public NodeMqttService(INodeConfigurationService configurationService, ICommandService commandService, IReportService reportService, ILogger<NodeMqttService> logger)
            : this(configurationService, commandService, reportService, logger,
                () => new MqttClient(configurationService.Configuration.Mqtt, logger))
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
                _resultClient = _client;
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
                _commandResultTopic = _configurationService.Configuration.GetTopic(MqttTopic.CommandResult);

                var reports = Channel.CreateBounded<Report>(new BoundedChannelOptions(ReportQueueCapacity)
                {
                    SingleReader = true,
                    FullMode = BoundedChannelFullMode.DropOldest
                }, OnReportDropped);
                _reports = reports;
                var client = _client;
                _reportPublisher = Task.Run(() => PublishReportsAsync(client, reports.Reader));

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

            await _client.PublishAsync(topic, value);
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

        // A command with a correlationId gets a CommandResult (design 7.1); one without keeps the old
        // fire-and-forget behaviour. A redelivered id runs only once: while the first delivery runs, its
        // result answers the redelivery too; afterwards the redelivery gets "duplicate" (it was executed)
        // or the first delivery's failure again.
        private void DispatchCommand(string message)
        {
            var command = TryReadCommand(message);
            var correlationId = string.IsNullOrEmpty(command?.CorrelationId) ? null : command.CorrelationId;
            lock (_commandGate)
            {
                _commands.RemoveAll(task => task.IsCompleted);
                if (!_acceptingCommands || _commands.Count >= MaxPendingCommands)
                {
                    _logger.LogWarning("Node MQTT command rejected: stopping or pending command limit {Limit} reached; no retry", MaxPendingCommands);
                    if (correlationId != null)
                        _ = PublishResultAsync(command, CommandResultStatus.Rejected,
                            _acceptingCommands ? $"More than {MaxPendingCommands} commands are pending." : "The node is stopping.");
                    return;
                }
                if (correlationId != null && !_correlations.TryAdd(correlationId))
                {
                    AnswerRedelivery(command);
                    return;
                }
                // Capture the device generation now, without blocking configuration messages behind I/O.
                _commands.Add(ExecuteCommandAsync(message, correlationId == null ? null : command, _shutdown.Token));
            }
        }

        private void AnswerRedelivery(Command command)
        {
            if (!_correlations.TryGet(command.CorrelationId, out var status, out var error) || status == null)
            {
                _logger.LogInformation("Command {CommandId} ({CorrelationId}) is still running; its result answers this delivery too",
                    command.Id, command.CorrelationId);
                return;
            }
            _logger.LogInformation("Command {CommandId} ({CorrelationId}) was received before; not run again", command.Id, command.CorrelationId);
            if (status == CommandResultStatus.Executed)
                _ = PublishResultAsync(command, CommandResultStatus.Duplicate);
            else
                _ = PublishResultAsync(command, status, error);
        }

        private static Command TryReadCommand(string message)
        {
            try
            {
                return Command.Create(message);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task ExecuteCommandAsync(string message, Command correlated, CancellationToken cancellationToken)
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
                if (correlated != null)
                    await CompleteAsync(correlated, CommandResultStatus.Executed).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The node is stopping. No result: the orchestrator delivers it again after the restart,
                // and the restarted node doesn't know the id, so it runs it then.
                if (correlated != null)
                    _correlations.Remove(correlated.CorrelationId);
                _logger.LogDebug("Node MQTT command was cancelled");
            }
            catch (OperationCanceledException error)
            {
                // The device was stopped or reconfigured while the command waited or ran, or the device
                // gave up. It must not run against a new configuration, so this is final.
                _logger.LogWarning("Node MQTT command was cancelled while the node kept running: {Reason}", error.Message);
                if (correlated != null)
                    await CompleteAsync(correlated, CommandResultStatus.Failed,
                        "The command was cancelled, because its device was stopped or reconfigured or timed out; it may have run.").ConfigureAwait(false);
            }
            catch (DeviceNotReadyException error) when (correlated != null)
            {
                NotReady(correlated, error.Message);
            }
            catch (UnknownCommandException error) when (correlated != null && AwaitsConfiguration(error.CommandId))
            {
                NotReady(correlated, "The node is still applying its configuration.");
            }
            catch (UnknownCommandException error)
            {
                _logger.LogWarning("Node MQTT command {CommandId} has no device on this node", error.CommandId);
                if (correlated != null)
                    await CompleteAsync(correlated, CommandResultStatus.UnknownCommand, error.Message).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                _logger.LogError(error, "Could not execute node MQTT command");
                if (correlated != null)
                    await CompleteAsync(correlated, CommandResultStatus.Failed, error.GetBaseException().Message).ConfigureAwait(false);
            }
        }

        // Remembers the outcome for redeliveries, then answers.
        private Task CompleteAsync(Command command, string status, string error = null)
        {
            _correlations.Complete(command.CorrelationId, status, error);
            return PublishResultAsync(command, status, error);
        }

        // The command didn't run: answer "rejected" and forget its id, so the orchestrator's next delivery runs it.
        private void NotReady(Command command, string reason)
        {
            _correlations.Remove(command.CorrelationId);
            _logger.LogInformation("Command {CommandId} ({CorrelationId}) rejected until the node is ready: {Reason}",
                command.Id, command.CorrelationId, reason);
            _ = PublishResultAsync(command, CommandResultStatus.Rejected, reason);
        }

        // Before the first configuration, and while a configuration that has the command is being
        // applied, an unknown command is a matter of time, not a mistake.
        private bool AwaitsConfiguration(string commandId)
        {
            var configuration = _configurationService.DeviceConfiguration;
            return configuration == null || configuration.DeviceConfigurations?.Any(device =>
                device.CommandTemplates?.Any(template => template.Id == commandId) == true) == true;
        }

        private async Task PublishResultAsync(Command command, string status, string error = null)
        {
            var client = _resultClient;
            if (client == null)
                return;
            var result = new CommandResult
            {
                CorrelationId = command.CorrelationId,
                CommandId = command.Id,
                Status = status,
                Error = error,
                TimeStamp = DateTime.UtcNow.ToEpoch()
            };
            try
            {
                await client.PublishAsync(_commandResultTopic, result.ToJson()).ConfigureAwait(false);
            }
            catch (Exception x)
            {
                // The orchestrator delivers the command again when no result arrives; the node then answers from its cache.
                _logger.LogWarning(x, "Could not publish the result {Status} of command {CommandId} ({CorrelationId})",
                    status, command.Id, command.CorrelationId);
            }
        }

        private async Task AnnounceOnlineAsync()
        {
            try
            {
                var message = _configurationService.OnlineMessage;
                if (message == null)
                    throw new InvalidOperationException("The node online message has not been configured.");
                // This service answers commands that carry a correlationId with a CommandResult.
                message.Capabilities = message.Capabilities ?? new List<string>();
                if (!message.Capabilities.Contains(Contract.Capabilities.CommandResult))
                    message.Capabilities.Add(Contract.Capabilities.CommandResult);
                await SendNodeOnlineMessage(message);
            }
            catch (Exception x)
            {
                _logger.LogError(x, "Could not announce node presence");
            }
        }

        private void _reportService_ReportUpdated(IDevice sender, IReport report)
        {
            if (!(report is Report typedReport))
            {
                _logger.LogWarning("ReportUpdated received an unexpected report type; publish skipped.");
                return;
            }

            var reports = _reports;
            if (reports == null || !reports.Writer.TryWrite(typedReport))
                _logger.LogWarning("ReportUpdated received while the MQTT service is not running; publish skipped.");
        }

        private async Task PublishReportsAsync(MqttClient client, ChannelReader<Report> reader)
        {
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (reader.TryRead(out var report))
                {
                    try
                    {
                        await client.PublishAsync(_reportTopic, Json.SerializeIgnoreNulls(report)).ConfigureAwait(false);
                    }
                    catch (Exception x)
                    {
                        _logger.LogError(x, "Failed to publish report {ReportId} to topic {Topic}; the next reports are still published",
                            report.Id, _reportTopic);
                    }
                }
            }
        }

        private void OnReportDropped(Report report)
        {
            var dropped = Interlocked.Increment(ref _droppedReports);
            var now = DateTime.UtcNow.Ticks;
            var last = Interlocked.Read(ref _lastDropWarning);
            if (now - last >= DropWarningInterval.Ticks && Interlocked.CompareExchange(ref _lastDropWarning, now, last) == last)
                _logger.LogWarning("Report queue full ({Capacity}): dropped the oldest report {ReportId}; {Dropped} reports dropped so far",
                    ReportQueueCapacity, report.Id, dropped);
        }

        public async Task Stop()
        {
            // Only the first of concurrent calls stops: a host can be stopped twice (by its owner, and
            // by Run when the application stops), and both calls reach the hosted service.
            var client = Interlocked.Exchange(ref _client, null);
            if (client == null)
                return;

            var shutdown = _shutdown;
            var legacyCommandGate = _legacyCommandGate;
            var reports = _reports;
            var reportPublisher = _reportPublisher;
            Task[] commands;
            lock (_commandGate)
            {
                _acceptingCommands = false;
                commands = _commands.ToArray();
            }
            shutdown.Cancel();
            client.MessageReceivedAsync -= _client_MessageReceived;
            client.ConnectedAsync -= AnnounceOnlineAsync;
            _reportService.ReportUpdated -= _reportService_ReportUpdated;

            try
            {
                await Task.WhenAll(commands).ConfigureAwait(false);
                // Publish the reports already queued before the client goes offline.
                reports.Writer.TryComplete();
                await reportPublisher.ConfigureAwait(false);
                await client.Stop().ConfigureAwait(false);
            }
            finally
            {
                Interlocked.CompareExchange(ref _resultClient, null, client);
                client.Dispose();
                shutdown.Dispose();
                legacyCommandGate.Dispose();
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