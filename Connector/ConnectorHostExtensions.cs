using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RIoT2.Core.Configuration;
using RIoT2.Core.Diagnostics;
using RIoT2.Core.Hosting;
using RIoT2.Core.Security;

namespace RIoT2.Connector.Sdk;

/// <summary>Hosts a connector: <c>builder.AddRIoT2Connector&lt;TSink, TSettings&gt;(...)</c>, then <c>app.MapRIoT2Connector()</c>.</summary>
public static class ConnectorHostExtensions
{
    /// <summary>The <c>IHttpClientFactory</c> client for the orchestrator's template routes.</summary>
    public const string TemplatesHttpClientName = "riot2-templates";

    /// <summary>
    /// Sets up a connector with sink <typeparamref name="TSink"/>:
    /// <list type="bullet">
    /// <item>logging (<c>AddRIoT2Logging</c>), then the settings: <see cref="ConnectorOptions.Read"/> and
    /// <paramref name="readSettings"/> from one <see cref="SettingsReader"/>, so an invalid start lists every problem
    /// under <paramref name="component"/>;</item>
    /// <item>the security seams, telemetry (meter <c>RIoT2.Connector</c>), the template catalog (with
    /// <c>X-RIoT2-Key</c> and the outbound URL policy), the sink, the <see cref="ConnectorPipeline"/> and the
    /// <see cref="ConnectorMqttService"/>.</item>
    /// </list>
    /// The connector's settings are registered as a singleton <typeparamref name="TSettings"/> and returned.
    /// </summary>
    /// <param name="component">The name in the configuration error, for example <c>InfluxDB connector</c>.</param>
    /// <param name="configure">Sets <see cref="ConnectorOptions.Name"/> and the queue limits.</param>
    public static TSettings AddRIoT2Connector<TSink, TSettings>(this WebApplicationBuilder builder, string component,
        Func<SettingsReader, TSettings> readSettings, Action<ConnectorOptions>? configure = null)
        where TSink : class, IConnectorSink
        where TSettings : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(component);
        ArgumentNullException.ThrowIfNull(readSettings);

        var loggerFactory = builder.AddRIoT2Logging("connector");
        var (options, settings) = ConnectorOptions.Load(builder.Configuration, component, readSettings, loggerFactory.CreateLogger("RIoT2.Connector"));
        configure?.Invoke(options);
        options.Validate();

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(settings);
        builder.Services.AddRIoT2Security(options.Security);
        builder.AddRIoT2Telemetry("connector", options.Id, MeterNames.Connector);

        builder.Services.AddHttpClient(TemplatesHttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .AddHttpMessageHandler(() => new ApiKeyHandler(options.Security.ApiKey))
            .AddHttpMessageHandler(services => new OutboundUrlPolicyHandler(services.GetRequiredService<IOutboundUrlPolicy>(), OutboundPurposes.Orchestrator));
        builder.Services.AddSingleton<ITemplateCatalog>(services =>
            new TemplateCatalog(services.GetRequiredService<IHttpClientFactory>().CreateClient(TemplatesHttpClientName)));
        builder.Services.AddSingleton<TSink>();
        builder.Services.AddSingleton<IConnectorSink>(services => services.GetRequiredService<TSink>());
        builder.Services.AddSingleton(services => new ConnectorPipeline(services.GetRequiredService<IConnectorSink>(),
            services.GetRequiredService<ITemplateCatalog>(), options, services.GetRequiredService<ILogger<ConnectorPipeline>>()));
        builder.Services.AddSingleton(services => new ConnectorMqttService(options, services.GetRequiredService<ConnectorPipeline>(),
            services.GetRequiredService<ITemplateCatalog>(), services.GetRequiredService<ILogger<ConnectorMqttService>>()));
        // Started in this order and stopped in reverse: MQTT stops taking messages before the queue drains.
        builder.Services.AddHostedService(services => services.GetRequiredService<ConnectorPipeline>());
        builder.Services.AddHostedService(services => services.GetRequiredService<ConnectorMqttService>());
        return settings;
    }

    /// <summary>
    /// The security middleware, <c>/health</c> (<see cref="ConnectorHealthReport"/>: MQTT, the sink and the spool; 503
    /// while MQTT is disconnected) and <c>/healthz</c> (the process is running), both anonymous.
    /// </summary>
    public static WebApplication MapRIoT2Connector(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseRIoT2Security();
        app.MapGet("/health", async (ConnectorMqttService mqtt, IConnectorSink sink, ConnectorPipeline pipeline, CancellationToken cancellationToken) =>
        {
            var report = await ConnectorHealthReport.CheckAsync(mqtt.IsConnected, sink, pipeline, cancellationToken).ConfigureAwait(false);
            return Results.Json(report, statusCode: report.HttpStatus);
        }).AllowAnonymous();
        app.MapGet("/healthz", () => Results.Ok(new { status = ConnectorHealthReport.Ok })).AllowAnonymous();
        return app;
    }
}
