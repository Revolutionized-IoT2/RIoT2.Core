using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using ConfigurationRoot = Microsoft.Extensions.Configuration.IConfiguration;

namespace RIoT2.Core.Configuration
{
    /// <summary>
    /// Reads <c>RIOT2_*</c> settings from <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>
    /// and collects every problem, so a service reports all invalid settings at once.
    /// </summary>
    /// <remarks>
    /// A host's configuration includes the environment variables, so the variable names don't change;
    /// tests pass an in-memory configuration instead. Each service maps its names in one options type
    /// (plan M4), reads them through one reader, and calls <see cref="ThrowIfInvalid"/> at the end.
    /// </remarks>
    public sealed class SettingsReader
    {
        private readonly ConfigurationRoot _configuration;
        private readonly List<string> _errors = new List<string>();

        public SettingsReader(ConfigurationRoot configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>The problems found so far, one sentence each, naming the setting.</summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>Reads a value that must be present and not blank.</summary>
        public string Required(string name)
        {
            var value = _configuration[name];
            if (string.IsNullOrWhiteSpace(value))
            {
                _errors.Add($"{name} is required");
                return null;
            }
            return value;
        }

        /// <summary>Reads a value that may be missing; an empty value counts as missing.</summary>
        public string Optional(string name, string defaultValue = null)
        {
            var value = _configuration[name];
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        /// <summary>Reads a required absolute <c>http</c> or <c>https</c> URL.</summary>
        public string RequiredHttpUrl(string name)
        {
            var value = Required(name);
            if (value != null && !IsHttpUrl(value))
            {
                _errors.Add($"{name} must be an absolute http or https URL");
                return null;
            }
            return value;
        }

        /// <summary>Reads a flag: <c>true</c> (any case) is true, anything else, or nothing, is false.</summary>
        public bool Flag(string name) => bool.TryParse(_configuration[name], out var value) && value;

        /// <summary>Reads an optional whole number in a range.</summary>
        public int Integer(string name, int defaultValue, int minimum, int maximum)
        {
            var value = _configuration[name];
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;
            if (int.TryParse(value, out var number) && number >= minimum && number <= maximum)
                return number;
            _errors.Add($"{name} must be a whole number from {minimum} to {maximum}");
            return defaultValue;
        }

        /// <summary>Adds a problem that the typed readers can't express, such as a rule across settings.</summary>
        public void AddError(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                throw new ArgumentException("An error message is required.", nameof(error));
            _errors.Add(error);
        }

        /// <summary>
        /// Throws <see cref="ConfigurationValidationException"/> listing every problem, after logging it
        /// as critical, if any setting was missing or invalid.
        /// </summary>
        /// <param name="component">The service name used in the message, for example <c>orchestrator</c>.</param>
        /// <param name="logger">Optional logger for the critical message.</param>
        public void ThrowIfInvalid(string component, ILogger logger = null)
        {
            if (_errors.Count == 0)
                return;
            var error = new ConfigurationValidationException(component, _errors.ToArray());
            logger?.LogCritical("{Message}", error.Message);
            throw error;
        }

        private static bool IsHttpUrl(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
