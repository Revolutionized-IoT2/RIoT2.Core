using RIoT2.Core.Configuration;

namespace RIoT2.Connector.Sdk;

/// <summary>
/// What every connector reads (plan M4, design 7.3): its id, whether it also handles commands, MQTT, logging, telemetry
/// and the security mode, plus the queue's limits, which a connector may change in code.
/// </summary>
public sealed class ConnectorOptions
{
    public const string IdVariable = "RIOT2_CONNECTOR_ID";
    public const string HandleCommandsVariable = "RIOT2_HANDLE_COMMANDS";

    /// <summary>Unique id; also the MQTT client id. Required.</summary>
    public string Id { get; set; } = null!;

    /// <summary>The name in the connector's presence message.</summary>
    public string Name { get; set; } = "Connector";

    /// <summary>Also handle commands (<c>riot2/node/+/command</c>), not only reports.</summary>
    public bool HandleCommands { get; set; }

    public MqttOptions Mqtt { get; set; } = new();

    /// <summary><c>RIOT2_LOG_FORMAT</c> and <c>RIOT2_LOG_LEVEL</c> (design 7.4).</summary>
    public LoggingOptions Logging { get; set; } = new();

    /// <summary><c>OTEL_EXPORTER_OTLP_ENDPOINT</c> and <c>RIOT2_TRACING</c> (design 7.4).</summary>
    public TelemetryOptions Telemetry { get; set; } = new();

    /// <summary>The optional security mode (design 7.5); this version accepts <c>off</c> only.</summary>
    public SecurityOptions Security { get; set; } = new();

    /// <summary>Messages the queue holds; when it is full, new messages are refused and counted as failed.</summary>
    public int QueueCapacity { get; set; } = 1000;

    /// <summary>The largest batch the sink gets.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>How long a batch waits to fill once its first message is there; zero writes whatever is waiting.</summary>
    public TimeSpan BatchInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>The first wait before a batch is retried after a transient failure; it doubles up to <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan MinRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>How long a stopping connector keeps writing what is queued before it discards the rest.</summary>
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Reads the shared settings and the connector's own (<paramref name="readSettings"/>) from one reader, and throws
    /// <see cref="ConfigurationValidationException"/> naming <paramref name="component"/> and listing every problem.
    /// </summary>
    public static (ConnectorOptions Connector, TSettings Settings) Load<TSettings>(Microsoft.Extensions.Configuration.IConfiguration configuration,
        string component, Func<SettingsReader, TSettings> readSettings, Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(readSettings);
        var reader = new SettingsReader(configuration);
        var connector = Read(reader);
        var settings = readSettings(reader);
        reader.ThrowIfInvalid(component, logger);
        return (connector, settings);
    }

    /// <summary>
    /// Reads the shared settings into <paramref name="settings"/>'s collected errors; the connector reads its own with the
    /// same reader and calls <see cref="SettingsReader.ThrowIfInvalid"/> once, so every problem is listed together.
    /// </summary>
    public static ConnectorOptions Read(SettingsReader settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new ConnectorOptions
        {
            Id = settings.Required(IdVariable),
            HandleCommands = settings.Flag(HandleCommandsVariable),
            Mqtt = MqttOptions.Read(settings),
            Logging = LoggingOptions.Read(settings),
            Telemetry = TelemetryOptions.Read(settings),
            Security = SecurityOptions.Read(settings)
        };
    }

    /// <summary>Throws when a limit set in code is out of range.</summary>
    public void Validate()
    {
        if (QueueCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(QueueCapacity), QueueCapacity, "The queue needs room for one message.");
        if (BatchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(BatchSize), BatchSize, "A batch has at least one message.");
        if (BatchInterval < TimeSpan.Zero || MinRetryDelay <= TimeSpan.Zero || MaxRetryDelay < MinRetryDelay || DrainTimeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MaxRetryDelay), "The batch interval, retry delays and drain timeout must be positive, with the minimum retry delay no larger than the maximum.");
    }
}
