using Microsoft.Extensions.Logging;
using System;

namespace RIoT2.Core.Configuration
{
    /// <summary>The log output format (<c>RIOT2_LOG_FORMAT</c>).</summary>
    public enum LogFormat
    {
        /// <summary>One human-readable line per event (the default).</summary>
        Text,

        /// <summary>Compact JSON, one object per line, for log collectors.</summary>
        Json
    }

    /// <summary>
    /// The logging settings shared by the Orchestrator, the Node, Elsa and the connectors (design 7.4,
    /// phase 2). <c>RIoT2.Core.Hosting</c>'s <c>AddRIoT2Logging()</c> applies them.
    /// </summary>
    public sealed class LoggingOptions
    {
        public const string FormatVariable = "RIOT2_LOG_FORMAT";
        public const string LevelVariable = "RIOT2_LOG_LEVEL";

        /// <summary>Console and file format; <see cref="LogFormat.Text"/> unless <c>RIOT2_LOG_FORMAT=json</c>.</summary>
        public LogFormat Format { get; set; } = LogFormat.Text;

        /// <summary>
        /// The lowest level written; <see cref="LogLevel.Information"/> unless <c>RIOT2_LOG_LEVEL</c> is set.
        /// Framework categories (<c>Microsoft</c>, <c>System</c>) are written from <c>Warning</c> unless
        /// this is higher.
        /// </summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Reads <c>RIOT2_LOG_FORMAT</c> (<c>text</c> or <c>json</c>, any case) and <c>RIOT2_LOG_LEVEL</c>
        /// (<c>Trace</c>, <c>Debug</c>, <c>Information</c>, <c>Warning</c>, <c>Error</c> or <c>Critical</c>,
        /// any case). An invalid value is reported through <paramref name="settings"/> and the default is used.
        /// </summary>
        public static LoggingOptions Read(SettingsReader settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            var options = new LoggingOptions();

            var format = settings.Optional(FormatVariable);
            if (format != null)
            {
                if (string.Equals(format, "text", StringComparison.OrdinalIgnoreCase))
                    options.Format = LogFormat.Text;
                else if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
                    options.Format = LogFormat.Json;
                else
                    settings.AddError($"{FormatVariable} must be text or json");
            }

            var level = settings.Optional(LevelVariable);
            if (level != null)
            {
                if (Enum.TryParse(level, true, out LogLevel parsed) && parsed != LogLevel.None && Enum.IsDefined(typeof(LogLevel), parsed)
                    && !int.TryParse(level, out _))
                    options.MinimumLevel = parsed;
                else
                    settings.AddError($"{LevelVariable} must be Trace, Debug, Information, Warning, Error or Critical");
            }

            return options;
        }
    }
}
