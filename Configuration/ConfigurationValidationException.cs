using System;
using System.Collections.Generic;

namespace RIoT2.Core.Configuration
{
    /// <summary>
    /// A service's settings are missing or invalid. The message has the same format in every service:
    /// <c>Invalid {component} configuration: {problem}; {problem}.</c>
    /// </summary>
    public sealed class ConfigurationValidationException : InvalidOperationException
    {
        public ConfigurationValidationException(string component, IReadOnlyList<string> errors)
            : base($"Invalid {component} configuration: {string.Join("; ", errors ?? Array.Empty<string>())}.")
        {
            Component = component;
            Errors = errors ?? Array.Empty<string>();
        }

        /// <summary>The service whose settings are invalid.</summary>
        public string Component { get; }

        /// <summary>Every problem, one sentence each, naming the setting.</summary>
        public IReadOnlyList<string> Errors { get; }
    }
}
