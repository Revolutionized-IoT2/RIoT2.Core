using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RIoT2.Core.Configuration;
using RIoT2.Core.Diagnostics;

namespace RIoT2.Core.Hosting;

/// <summary>
/// The metrics and traces of every RIoT2 service (design 7.4, phase 4). The instruments always exist
/// (<c>System.Diagnostics.Metrics</c>, free while nothing listens); they are exported over OTLP only
/// when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set (<see cref="TelemetryOptions"/>).
/// </summary>
public static class RIoT2Telemetry
{
    /// <summary>The prefix of every RIoT2 activity source; traces include them all.</summary>
    public const string ActivitySourcePrefix = "RIoT2.";

    /// <summary>
    /// Exports the service's meters, the shared MQTT client's (<see cref="MeterNames.Mqtt"/>), ASP.NET
    /// Core's and the .NET runtime's over OTLP, plus traces with <c>RIOT2_TRACING=true</c>. Does nothing,
    /// and returns <c>false</c>, without <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>. The OTLP exporter reads the
    /// endpoint and the other <c>OTEL_EXPORTER_OTLP_*</c> settings itself. An invalid setting leaves
    /// telemetry off here; the service's options type reports it with its other settings.
    /// </summary>
    /// <param name="service">The service, e.g. <c>orchestrator</c>; the resource's <c>service.name</c> is <c>riot2-&lt;service&gt;</c>.</param>
    /// <param name="instanceId">The service's RIoT2 id (<c>service.instance.id</c>), which tells two nodes apart.</param>
    /// <param name="meters">The service's own meters, e.g. <see cref="MeterNames.Orchestrator"/>.</param>
    public static bool AddRIoT2Telemetry(this IHostApplicationBuilder builder, string service, string instanceId, params string[] meters)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        var options = TelemetryOptions.Read(new SettingsReader(builder.Configuration));
        if (!options.Enabled)
            return false;

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: ServiceName(service),
                serviceNamespace: "riot2",
                serviceInstanceId: string.IsNullOrWhiteSpace(instanceId) ? null : instanceId,
                autoGenerateServiceInstanceId: string.IsNullOrWhiteSpace(instanceId)))
            .WithMetrics(metrics => metrics
                .AddMeter(MeterNames.Mqtt)
                .AddMeter(meters ?? [])
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());

        if (options.Tracing)
        {
            telemetry.WithTracing(tracing => tracing
                .AddSource(ActivitySourcePrefix + "*")
                // Health checks run every few seconds and say nothing about the system.
                .AddAspNetCoreInstrumentation(aspNet => aspNet.Filter = context => !IsHealthCheck(context.Request))
                .AddHttpClientInstrumentation()
                .AddOtlpExporter());
        }
        return true;
    }

    /// <summary>The resource's <c>service.name</c>: <c>riot2-&lt;service&gt;</c>.</summary>
    public static string ServiceName(string service) => "riot2-" + service;

    private static bool IsHealthCheck(HttpRequest request) =>
        request.Path.StartsWithSegments("/health") || request.Path.StartsWithSegments("/healthz");
}
