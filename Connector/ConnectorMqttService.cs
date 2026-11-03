using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RIoT2.Core;
using RIoT2.Core.Models;
using RIoT2.Core.Utils;

namespace RIoT2.Connector.Sdk;

/// <summary>
/// The connector's MQTT side (design 7.3): announces it on <c>riot2/node/{id}/online</c> and again when the
/// orchestrator comes online, loads the templates when the orchestrator sends the configuration command, and queues
/// every readable report (and, with <see cref="ConnectorOptions.HandleCommands"/>, command) with its receive time.
/// </summary>
public sealed class ConnectorMqttService : IHostedService, IAsyncDisposable
{
    private readonly ConnectorOptions _options;
    private readonly ConnectorPipeline _pipeline;
    private readonly ITemplateCatalog _templates;
    private readonly OrchestratorEndpoint _endpoint;
    private readonly IReadOnlyList<IConnectorTemplateObserver> _observers;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly MqttClient _client;
    private readonly string _reportTopic = Constants.Get("+", MqttTopic.Report);
    private readonly string _commandTopic = Constants.Get("+", MqttTopic.Command);
    private readonly string _orchestratorOnlineTopic = Constants.Get("+", MqttTopic.OrchestratorOnline);
    private readonly string _onlineTopic;
    private readonly string _configurationTopic;

    public ConnectorMqttService(ConnectorOptions options, ConnectorPipeline pipeline, ITemplateCatalog templates,
        ILogger<ConnectorMqttService> logger, TimeProvider? time = null)
        : this(options, pipeline, templates, logger, time, client: null) { }

    /// <summary>
    /// With the endpoint the configuration command sets (<see cref="IRiot2CommandClient"/> reads it) and the observers
    /// told about every new set of templates (Core 2.1.0).
    /// </summary>
    public ConnectorMqttService(ConnectorOptions options, ConnectorPipeline pipeline, ITemplateCatalog templates,
        ILogger<ConnectorMqttService> logger, OrchestratorEndpoint endpoint, IEnumerable<IConnectorTemplateObserver> observers,
        TimeProvider? time = null)
        : this(options, pipeline, templates, logger, time, client: null, endpoint, observers) { }

    // Tests pass their own client.
    internal ConnectorMqttService(ConnectorOptions options, ConnectorPipeline pipeline, ITemplateCatalog templates,
        ILogger<ConnectorMqttService> logger, TimeProvider? time, MqttClient? client,
        OrchestratorEndpoint? endpoint = null, IEnumerable<IConnectorTemplateObserver>? observers = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
        _endpoint = endpoint ?? new OrchestratorEndpoint();
        _observers = observers?.ToList() ?? [];
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _time = time ?? TimeProvider.System;
        _onlineTopic = Constants.Get(options.Id, MqttTopic.NodeOnline);
        _configurationTopic = Constants.Get(options.Id, MqttTopic.Configuration);
        _client = client ?? new MqttClient(options.Mqtt.ToConfiguration(options.Id), logger);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.MessageReceivedAsync += HandleMessageAsync;
        _client.ConnectedAsync += AnnounceAsync;
        var topics = new List<string> { _reportTopic, _configurationTopic, _orchestratorOnlineTopic };
        if (_options.HandleCommands)
            topics.Add(_commandTopic);
        try
        {
            await _client.Start(topics.ToArray()).ConfigureAwait(false);
            _logger.LogInformation("Connected to MQTT; listening for {What}", _options.HandleCommands ? "reports and commands" : "reports");
        }
        catch (Exception error)
        {
            _logger.LogError(error, "Could not connect to the MQTT broker");
            throw new InvalidOperationException("Could not connect to the MQTT broker.", error);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _client.MessageReceivedAsync -= HandleMessageAsync;
        _client.ConnectedAsync -= AnnounceAsync;
        await _client.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => _client.DisposeAsync();

    /// <summary>Whether the MQTT connection is up (<c>/health</c>).</summary>
    public bool IsConnected => _client.IsConnected();

    private async Task AnnounceAsync()
    {
        await _client.PublishAsync(_onlineTopic, Json.SerializeIgnoreNulls(Presence(_options))).ConfigureAwait(false);
        _logger.LogInformation("Presence sent");
    }

    /// <summary>
    /// The connector's presence (design 7.3 phase 3): <see cref="NodeType.Connector"/>, its name, <c>nodeBaseUrl</c>
    /// from <c>RIOT2_CONNECTOR_URL</c>, and a manifest with the connector's name and version and the SDK's version.
    /// </summary>
    internal static NodeOnlineMessage Presence(ConnectorOptions options) => new()
    {
        IsOnline = true,
        Name = options.Name,
        NodeType = NodeType.Connector,
        NodeBaseUrl = options.Url ?? "",
        Manifest = Manifest
    };

    private static readonly PackageManifest Manifest = CreateManifest();

    private static PackageManifest CreateManifest()
    {
        var connector = System.Reflection.Assembly.GetEntryAssembly()?.GetName();
        return new PackageManifest
        {
            Name = connector?.Name ?? "",
            Version = Version(System.Reflection.Assembly.GetEntryAssembly()),
            Date = "",
            InstalledPackageFilename = "",
            CoreVersion = Version(typeof(ConnectorMqttService).Assembly)
        };
    }

    // The informational version without the source revision after '+'.
    private static string Version(System.Reflection.Assembly? assembly)
    {
        var version = assembly?.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion
            ?? assembly?.GetName().Version?.ToString() ?? "";
        var plus = version.IndexOf('+');
        return plus < 0 ? version : version[..plus];
    }

    // Internal for tests: what the connector does with each MQTT message.
    internal async Task HandleMessageAsync(MqttEventArgs message)
    {
        try
        {
            if (MqttClient.IsMatch(message.Topic, _orchestratorOnlineTopic))
                await AnnounceAsync().ConfigureAwait(false);
            if (MqttClient.IsMatch(message.Topic, _configurationTopic))
            {
                try
                {
                    var command = Json.Deserialize<ConfigurationCommand>(message.Message);
                    if (string.IsNullOrWhiteSpace(command?.ApiBaseUrl))
                        throw new InvalidDataException("The configuration command has no apiBaseUrl.");
                    _endpoint.BaseUrl = command.ApiBaseUrl;
                    await _templates.LoadAsync(command.ApiBaseUrl).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    _logger.LogError(error, "Could not load the templates");
                    return;
                }
                await NotifyObserversAsync().ConfigureAwait(false);
                return;
            }
            if (MqttClient.IsMatch(message.Topic, _reportTopic))
                Queue(ConnectorMessageKind.Report, message, Report.Create(message.Message) != null, MqttTopic.Report);
            else if (_options.HandleCommands && MqttClient.IsMatch(message.Topic, _commandTopic))
                Queue(ConnectorMessageKind.Command, message, Command.Create(message.Message) != null, MqttTopic.Command);
        }
        catch (Exception error)
        {
            _logger.LogError(error, "Could not process the MQTT message on {Topic}", message.Topic);
        }
    }

    private async Task NotifyObserversAsync()
    {
        var snapshot = _templates.Snapshot;
        if (snapshot == null)
            return;
        foreach (var observer in _observers)
        {
            try
            {
                await observer.TemplatesLoadedAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                _logger.LogError(error, "{Observer} could not process the new templates", observer.GetType().Name);
            }
        }
    }

    private void Queue(ConnectorMessageKind kind, MqttEventArgs message, bool readable, MqttTopic topic)
    {
        var type = ConnectorMetrics.TypeOf(kind);
        if (!readable)
        {
            ConnectorMetrics.Message(type, ConnectorMetrics.Invalid);
            _logger.LogWarning("Ignored an unreadable {Type}: {Message}", type, message.Message);
            return;
        }
        var queued = _pipeline.TryEnqueue(new ConnectorMessage(kind, Constants.GetTopicId(message.Topic, topic), message.Message, _time.GetUtcNow()));
        ConnectorMetrics.Message(type, queued ? ConnectorMetrics.Queued : ConnectorMetrics.Failed);
        if (!queued)
            _logger.LogWarning("The queue is full or stopping; a {Type} was not accepted", type);
    }
}
