using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RIoT2.Core.Configuration;
using RIoT2.Core.Utils;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Display;
using System.Globalization;
using System.Text;

namespace RIoT2.Core.Hosting;

/// <summary>
/// The logging every RIoT2 service uses (design 7.4, phase 2): Serilog to the console and to
/// <c>Logs/RIoT2.log</c>, human-readable or compact JSON (<see cref="LoggingOptions"/>), with the
/// <see cref="LogScope"/> ids on every event.
/// </summary>
public static class RIoT2Logging
{
    /// <summary>The text format: time, level, category, message, then the scope ids, if any.</summary>
    public const string TextTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext:l}: {Message:lj}{Ids:l}{NewLine}{Exception}";

    /// <summary>The log file, relative to the working directory (<c>/app/Logs</c> in the images).</summary>
    public const string DefaultLogFile = "Logs/RIoT2.log";

    /// <summary>A file rolls over at this size, and at midnight.</summary>
    public const long FileSizeLimitBytes = 50L * 1024 * 1024;

    /// <summary>The number of log files kept, the current one included.</summary>
    public const int RetainedFileCount = 14;

    /// <summary>
    /// Replaces the host's logging providers with RIoT2's Serilog pipeline, configured from
    /// <c>RIOT2_LOG_FORMAT</c> and <c>RIOT2_LOG_LEVEL</c>, and returns a logger factory for the code that
    /// runs before the host is built. Its loggers write through the same pipeline at once, and through
    /// the host's logger factory as soon as that exists, so they share its providers (a test's too) and
    /// log scopes with every <c>ILogger&lt;T&gt;</c>. An invalid setting falls back to its default here; the
    /// service's options type reports it together with its other settings.
    /// </summary>
    /// <param name="service">The service name added to every event as <c>Service</c>, e.g. <c>orchestrator</c>.</param>
    public static ILoggerFactory AddRIoT2Logging(this IHostApplicationBuilder builder, string service)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = LoggingOptions.Read(new SettingsReader(builder.Configuration));
        var serilog = CreateConfiguration(options, service).CreateLogger();
        builder.Logging.ClearProviders();
        // Serilog filters categories; the host's filter only must not drop what Serilog would write.
        builder.Logging.SetMinimumLevel(options.MinimumLevel);
        // The host disposes the provider, which flushes the file.
        builder.Logging.AddSerilog(serilog, dispose: true);

        var startup = new StartupLoggerFactory(LoggerFactory.Create(logging =>
            logging.SetMinimumLevel(options.MinimumLevel).AddSerilog(serilog, dispose: false)));
        // The host's factory as the host would create it, handed to the start-up factory when it is.
        builder.Services.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(services =>
        {
            var host = new LoggerFactory(services.GetServices<ILoggerProvider>(),
                services.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>(),
                services.GetService<IOptions<LoggerFactoryOptions>>());
            startup.Attach(host);
            return host;
        }));
        return startup;
    }

    /// <summary>A logger factory that writes through <see cref="CreateConfiguration"/>.</summary>
    public static ILoggerFactory CreateLoggerFactory(LoggingOptions options, string service,
        string? logFile = DefaultLogFile, TextWriter? output = null)
    {
        var serilog = CreateConfiguration(options, service, logFile, output).CreateLogger();
        // Serilog filters by level; the factory passes everything on.
        return LoggerFactory.Create(logging => logging.SetMinimumLevel(LogLevel.Trace).AddSerilog(serilog, dispose: true));
    }

    /// <summary>
    /// The Serilog configuration: levels, the <c>Service</c> property and the scope ids, and the console
    /// (or <paramref name="output"/>) and file sinks in the chosen format. <paramref name="logFile"/>
    /// <c>null</c> writes no file.
    /// </summary>
    public static LoggerConfiguration CreateConfiguration(LoggingOptions options, string service,
        string? logFile = DefaultLogFile, TextWriter? output = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var minimum = ToSerilog(options.MinimumLevel);
        var framework = Max(minimum, LogEventLevel.Warning);
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(minimum)
            .MinimumLevel.Override("Microsoft", framework)
            .MinimumLevel.Override("System", framework)
            // "Now listening on …" and "Application started" stay visible.
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Max(minimum, LogEventLevel.Information))
            .Enrich.FromLogContext();
        if (!string.IsNullOrEmpty(service))
            configuration.Enrich.WithProperty("Service", service);

        // JSON carries the ids as properties; text shows them through {Ids}.
        ITextFormatter formatter;
        if (options.Format == LogFormat.Json)
            formatter = new RenderedCompactJsonFormatter();
        else
        {
            configuration.Enrich.With<IdsEnricher>();
            formatter = new MessageTemplateTextFormatter(TextTemplate, CultureInfo.InvariantCulture);
        }

        if (output != null)
            configuration.WriteTo.Sink(new WriterSink(formatter, output));
        else
            configuration.WriteTo.Console(formatter);
        if (logFile != null)
            configuration.WriteTo.File(formatter, logFile, rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: FileSizeLimitBytes, rollOnFileSizeLimit: true,
                retainedFileCountLimit: RetainedFileCount, shared: true);
        return configuration;
    }

    private static LogEventLevel ToSerilog(LogLevel level) => level switch
    {
        LogLevel.Trace => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        _ => LogEventLevel.Fatal
    };

    private static LogEventLevel Max(LogEventLevel a, LogEventLevel b) => a > b ? a : b;

    // For tests and tools that capture the output instead of the console.
    private sealed class WriterSink(ITextFormatter formatter, TextWriter writer) : ILogEventSink
    {
        private readonly Lock _gate = new();

        public void Emit(LogEvent logEvent)
        {
            lock (_gate)
            {
                formatter.Format(logEvent, writer);
                writer.Flush();
            }
        }
    }

    // " [CorrelationId=…]" when the event has a LogScope id, otherwise empty.
    private sealed class IdsEnricher : ILogEventEnricher
    {
        private static readonly string[] Names = [LogScope.CorrelationId, LogScope.MessageId];

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            StringBuilder? ids = null;
            foreach (var name in Names)
            {
                if (logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue { Value: string id } && id.Length > 0)
                    (ids ??= new StringBuilder(" [")).Append(ids.Length > 2 ? " " : "").Append(name).Append('=').Append(id);
            }
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Ids", ids == null ? "" : ids.Append(']').ToString()));
        }
    }
}
