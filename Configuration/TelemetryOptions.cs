using System;

namespace RIoT2.Core.Configuration
{
    /// <summary>
    /// Metrics and traces (design 7.4, phase 4). Nothing is exported unless
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set; <c>RIoT2.Core.Hosting</c>'s <c>AddRIoT2Telemetry()</c>
    /// applies these settings. The OpenTelemetry SDK reads the other standard <c>OTEL_*</c> variables
    /// itself, for example <c>OTEL_EXPORTER_OTLP_PROTOCOL</c> and <c>OTEL_METRIC_EXPORT_INTERVAL</c>.
    /// </summary>
    public sealed class TelemetryOptions
    {
        public const string EndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";
        public const string TracingVariable = "RIOT2_TRACING";

        /// <summary>The OTLP collector, or <c>null</c>, which turns metrics and traces off.</summary>
        public Uri OtlpEndpoint { get; set; }

        /// <summary>Also export traces (<c>RIOT2_TRACING=true</c>); needs <see cref="OtlpEndpoint"/>.</summary>
        public bool Tracing { get; set; }

        /// <summary>Whether anything is exported.</summary>
        public bool Enabled => OtlpEndpoint != null;

        /// <summary>
        /// Reads <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (an absolute <c>http</c> or <c>https</c> URL) and
        /// <c>RIOT2_TRACING</c> (<c>true</c> or <c>false</c>). An invalid value is reported through
        /// <paramref name="settings"/>, and telemetry stays off.
        /// </summary>
        public static TelemetryOptions Read(SettingsReader settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            var options = new TelemetryOptions();

            var endpoint = settings.Optional(EndpointVariable);
            if (endpoint != null)
            {
                if (Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    options.OtlpEndpoint = uri;
                else
                    settings.AddError($"{EndpointVariable} must be an absolute http or https URL");
            }

            var tracing = settings.Optional(TracingVariable);
            if (tracing != null)
            {
                if (!bool.TryParse(tracing.Trim(), out var enabled))
                    settings.AddError($"{TracingVariable} must be true or false");
                else if (enabled && endpoint == null)
                    settings.AddError($"{TracingVariable}=true needs {EndpointVariable}");
                else
                    options.Tracing = enabled && options.OtlpEndpoint != null;
            }

            return options;
        }
    }
}
