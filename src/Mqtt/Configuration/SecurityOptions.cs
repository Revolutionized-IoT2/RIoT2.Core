using System;
using System.Collections.Generic;
using System.Linq;

namespace RIoT2.Core.Configuration
{
    /// <summary>The deployment-wide security mode, <c>RIOT2_SECURITY_MODE</c> (design 7.5).</summary>
    public enum SecurityMode
    {
        /// <summary>Nothing is enforced: no login, anonymous REST, any CORS origin, plain MQTT allowed.</summary>
        Off,

        /// <summary>Credentials are checked where presented, and what <see cref="On"/> would reject is logged, never rejected.</summary>
        Audit,

        /// <summary>The features in <c>RIOT2_SECURITY_FEATURES</c> are enforced.</summary>
        On
    }

    /// <summary>The names <c>RIOT2_SECURITY_FEATURES</c> accepts (design 7.5).</summary>
    public static class SecurityFeatures
    {
        public const string Api = "api";
        public const string Realtime = "realtime";
        public const string Mqtt = "mqtt";
        public const string Endpoints = "endpoints";
        public const string Signing = "signing";
        public const string Secrets = "secrets";
        public const string Outbound = "outbound";

        /// <summary>Every feature, in the order the design lists them; <c>all</c> means these.</summary>
        public static IReadOnlyList<string> All { get; } = new[] { Api, Realtime, Mqtt, Endpoints, Signing, Secrets, Outbound };
    }

    /// <summary>
    /// The optional security mode's settings, shared by every service (ADR 0004, design 7.5). The
    /// default is <see cref="SecurityMode.Off"/>, which behaves exactly as RIoT2 did without them.
    /// </summary>
    /// <remarks>
    /// This version has the seams only (phase S0): <c>RIOT2_SECURITY_MODE</c> accepts <c>off</c>, and
    /// <c>audit</c> or <c>on</c> stop the start, so a deployment can't believe it is protected when
    /// it isn't. The other settings are read and validated, and clients already send
    /// <c>RIOT2_API_KEY</c>, so they can be set before the mode is switched.
    /// </remarks>
    public sealed class SecurityOptions
    {
        public const string ModeVariable = "RIOT2_SECURITY_MODE";
        public const string FeaturesVariable = "RIOT2_SECURITY_FEATURES";
        public const string ApiKeyVariable = "RIOT2_API_KEY";
        public const string CorsOriginsVariable = "RIOT2_CORS_ORIGINS";

        /// <summary>The modes this version supports.</summary>
        public static IReadOnlyList<SecurityMode> SupportedModes { get; } = new[] { SecurityMode.Off };

        /// <summary><c>RIOT2_SECURITY_MODE</c>: <c>off</c> (default), <c>audit</c> or <c>on</c>.</summary>
        public SecurityMode Mode { get; set; } = SecurityMode.Off;

        /// <summary>
        /// <c>RIOT2_SECURITY_FEATURES</c>: a comma-separated subset of <see cref="SecurityFeatures.All"/>, or
        /// <c>all</c> (default). Ignored while the mode is <see cref="SecurityMode.Off"/>.
        /// </summary>
        public IReadOnlyList<string> Features { get; set; } = SecurityFeatures.All;

        /// <summary>
        /// <c>RIOT2_API_KEY</c>: this service's key, sent in <c>X-RIoT2-Key</c> by its service-to-service
        /// clients when set. Never checked while the mode is <see cref="SecurityMode.Off"/>.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// <c>RIOT2_CORS_ORIGINS</c>: the browser origins allowed when the <c>api</c> feature is on, comma
        /// separated, for example <c>http://riot2.lan</c>. While the mode is <see cref="SecurityMode.Off"/>
        /// any origin is allowed.
        /// </summary>
        public IReadOnlyList<string> CorsOrigins { get; set; } = Array.Empty<string>();

        /// <summary>The features enforced: none while the mode is <see cref="SecurityMode.Off"/>.</summary>
        public IReadOnlyList<string> EnforcedFeatures => Mode == SecurityMode.Off ? Array.Empty<string>() : Features;

        /// <summary>Whether <paramref name="feature"/> is enforced (or audited) in this mode.</summary>
        public bool IsEnabled(string feature) => EnforcedFeatures.Contains(feature, StringComparer.Ordinal);

        /// <summary>
        /// Reads <c>RIOT2_SECURITY_MODE</c>, <c>RIOT2_SECURITY_FEATURES</c>, <c>RIOT2_API_KEY</c> and
        /// <c>RIOT2_CORS_ORIGINS</c>. A mode this version doesn't support is an error.
        /// </summary>
        public static SecurityOptions Read(SettingsReader settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            var options = new SecurityOptions
            {
                ApiKey = settings.Optional(ApiKeyVariable)?.Trim()
            };
            if (options.ApiKey?.Length == 0)
                options.ApiKey = null;

            var mode = settings.Optional(ModeVariable)?.Trim();
            if (!string.IsNullOrEmpty(mode))
            {
                if (!TryParseMode(mode!, out var parsed))
                    settings.AddError($"{ModeVariable} must be off, audit or on");
                else if (!SupportedModes.Contains(parsed))
                    settings.AddError($"{ModeVariable}={mode!.ToLowerInvariant()} is not supported by this version, which has the security " +
                        "seams only (design 7.5 phase S0); use off");
                else
                    options.Mode = parsed;
            }

            var features = settings.Optional(FeaturesVariable)?.Trim();
            if (!string.IsNullOrEmpty(features) && !string.Equals(features, "all", StringComparison.OrdinalIgnoreCase))
            {
                var names = features!.Split(',').Select(f => f.Trim().ToLowerInvariant()).Where(f => f.Length > 0).Distinct().ToList();
                var unknown = names.Where(f => !SecurityFeatures.All.Contains(f, StringComparer.Ordinal)).ToList();
                if (unknown.Count > 0)
                    settings.AddError($"{FeaturesVariable} has unknown features ({string.Join(", ", unknown)}); use all or a comma-separated " +
                        "list of " + string.Join(", ", SecurityFeatures.All));
                else
                    options.Features = SecurityFeatures.All.Where(names.Contains).ToArray();
            }

            var origins = settings.Optional(CorsOriginsVariable);
            if (!string.IsNullOrWhiteSpace(origins))
            {
                var list = origins!.Split(',').Select(o => o.Trim().TrimEnd('/')).Where(o => o.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var invalid = list.Where(o => !IsOrigin(o)).ToList();
                if (invalid.Count > 0)
                    settings.AddError($"{CorsOriginsVariable} must be a comma-separated list of origins such as http://riot2.lan:8080 " +
                        $"(scheme, host and optional port, no path); not valid: {string.Join(", ", invalid)}");
                else
                    options.CorsOrigins = list;
            }
            return options;
        }

        /// <summary>The mode's name in settings and in <c>/api/security/info</c>: <c>off</c>, <c>audit</c> or <c>on</c>.</summary>
        public static string ToName(SecurityMode mode) => mode switch
        {
            SecurityMode.Audit => "audit",
            SecurityMode.On => "on",
            _ => "off"
        };

        private static bool TryParseMode(string value, out SecurityMode mode)
        {
            switch (value.ToLowerInvariant())
            {
                case "off": mode = SecurityMode.Off; return true;
                case "audit": mode = SecurityMode.Audit; return true;
                case "on": mode = SecurityMode.On; return true;
                default: mode = SecurityMode.Off; return false;
            }
        }

        private static bool IsOrigin(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && (uri.AbsolutePath == "/" || uri.AbsolutePath.Length == 0)
            && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
            && string.IsNullOrEmpty(uri.UserInfo);
    }
}
