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
    public const string NameVariable = "RIOT2_CONNECTOR_NAME";
    public const string UrlVariable = "RIOT2_CONNECTOR_URL";
    public const string SpoolVariable = "RIOT2_CONNECTOR_SPOOL";
    public const string SpoolMaxMegabytesVariable = "RIOT2_CONNECTOR_SPOOL_MAX_MB";
    public const string SpoolMaxAgeDaysVariable = "RIOT2_CONNECTOR_SPOOL_MAX_AGE_DAYS";

    /// <summary>Unique id; also the MQTT client id. Required.</summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// The name in the connector's presence message and the UI (<c>RIOT2_CONNECTOR_NAME</c>); when that is unset, the
    /// connector's own default, set in code.
    /// </summary>
    public string Name { get; set; } = "Connector";

    /// <summary>
    /// Where the orchestrator reaches the connector (<c>RIOT2_CONNECTOR_URL</c>, optional): announced as
    /// <c>nodeBaseUrl</c>, so the UI can show the connector's <c>/health</c>.
    /// </summary>
    public string? Url { get; set; }

    // RIOT2_CONNECTOR_NAME, which wins over the default set in code (AddRIoT2Connector applies it after configure).
    internal string? ConfiguredName { get; set; }

    /// <summary>Also handle commands (<c>riot2/node/+/command</c>), not only reports.</summary>
    public bool HandleCommands { get; set; }

    public MqttOptions Mqtt { get; set; } = new();

    /// <summary><c>RIOT2_LOG_FORMAT</c> and <c>RIOT2_LOG_LEVEL</c> (design 7.4).</summary>
    public LoggingOptions Logging { get; set; } = new();

    /// <summary><c>OTEL_EXPORTER_OTLP_ENDPOINT</c> and <c>RIOT2_TRACING</c> (design 7.4).</summary>
    public TelemetryOptions Telemetry { get; set; } = new();

    /// <summary>The optional security mode (design 7.5); this version accepts <c>off</c> only.</summary>
    public SecurityOptions Security { get; set; } = new();

    /// <summary>
    /// The disk spool (design 7.3 phase 2). <see cref="Read"/> turns it on unless <c>RIOT2_CONNECTOR_SPOOL=false</c>;
    /// options built in code have it off.
    /// </summary>
    public ConnectorSpoolOptions Spool { get; set; } = new();

    /// <summary>
    /// Messages the in-memory queue holds. Without the spool, new messages are refused (counted as failed) when it is
    /// full; with the spool, they go to disk once it is <see cref="ConnectorSpoolOptions.HighWaterMark"/> full.
    /// </summary>
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
            ConfiguredName = settings.Optional(NameVariable),
            Url = settings.Optional(UrlVariable) is { Length: > 0 } ? settings.RequiredHttpUrl(UrlVariable) : null,
            HandleCommands = settings.Flag(HandleCommandsVariable),
            Mqtt = MqttOptions.Read(settings),
            Logging = LoggingOptions.Read(settings),
            Telemetry = TelemetryOptions.Read(settings),
            Security = SecurityOptions.Read(settings),
            Spool = ConnectorSpoolOptions.Read(settings)
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
        Spool.Validate();
    }
}

/// <summary>
/// The connector's disk spool (design 7.3 phase 2): where messages wait while the sink is down, and where rejected
/// batches are kept (<c>dead-letter.jsonl</c>).
/// </summary>
public sealed class ConnectorSpoolOptions
{
    public bool Enabled { get; set; }

    /// <summary>The spool folder; <c>Data/spool</c> next to the application (<c>/app/Data/spool</c> in the image).</summary>
    public string Path { get; set; } = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "spool");

    /// <summary>The most the segment files may hold (<c>RIOT2_CONNECTOR_SPOOL_MAX_MB</c>, 100 MB); beyond it the oldest segment is dropped.</summary>
    public long MaxBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>How long a segment is kept (<c>RIOT2_CONNECTOR_SPOOL_MAX_AGE_DAYS</c>, 7 days) after its last write.</summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(7);

    /// <summary>A segment file is closed at this size, or at a tenth of <see cref="MaxBytes"/> if that is smaller.</summary>
    public long SegmentBytes { get; set; } = 4L * 1024 * 1024;

    /// <summary>The dead-letter file is moved to <c>dead-letter.1.jsonl</c> (replacing it) at this size.</summary>
    public long DeadLetterMaxBytes { get; set; } = 10L * 1024 * 1024;

    /// <summary>How full the in-memory queue may get before new messages go to disk.</summary>
    public double HighWaterMark { get; set; } = 0.8;

    /// <summary>Reads <c>RIOT2_CONNECTOR_SPOOL</c> (default true), <c>RIOT2_CONNECTOR_SPOOL_MAX_MB</c> and <c>RIOT2_CONNECTOR_SPOOL_MAX_AGE_DAYS</c>.</summary>
    public static ConnectorSpoolOptions Read(SettingsReader settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var enabled = true;
        var value = settings.Optional(ConnectorOptions.SpoolVariable);
        if (!string.IsNullOrWhiteSpace(value) && !bool.TryParse(value, out enabled))
            settings.AddError($"{ConnectorOptions.SpoolVariable} must be true or false");
        return new ConnectorSpoolOptions
        {
            Enabled = enabled,
            MaxBytes = settings.Integer(ConnectorOptions.SpoolMaxMegabytesVariable, 100, 1, 100_000) * 1024L * 1024,
            MaxAge = TimeSpan.FromDays(settings.Integer(ConnectorOptions.SpoolMaxAgeDaysVariable, 7, 1, 365))
        };
    }

    /// <summary>Throws when a limit is out of range.</summary>
    public void Validate()
    {
        if (!Enabled)
            return;
        if (string.IsNullOrWhiteSpace(Path))
            throw new ArgumentException("The spool needs a folder.", nameof(Path));
        if (MaxBytes < 1024 || SegmentBytes < 1 || DeadLetterMaxBytes < 1 || MaxAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MaxBytes), "The spool needs at least 1 KB, positive segment and dead-letter sizes, and a positive age limit.");
        if (HighWaterMark <= 0 || HighWaterMark > 1)
            throw new ArgumentOutOfRangeException(nameof(HighWaterMark), HighWaterMark, "The high-water mark is a fraction of the queue, above 0 and at most 1.");
    }
}
