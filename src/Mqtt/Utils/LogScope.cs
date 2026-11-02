using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace RIoT2.Core.Utils
{
    /// <summary>
    /// Log scope properties that follow one message through the system (design 7.4, phase 2): every
    /// event logged inside the scope carries the id, also from device code and other services' loggers
    /// on the same async flow. <c>RIoT2.Core.Hosting</c> shows them at the end of each text line and as
    /// JSON properties.
    /// </summary>
    public static class LogScope
    {
        /// <summary>A command's <c>correlationId</c> (design 7.1), from the orchestrator's outbox to the device.</summary>
        public const string CorrelationId = "CorrelationId";

        /// <summary>A workflow trigger's <c>message_id</c> (design 7.1), from the orchestrator's outbox to Elsa.</summary>
        public const string MessageId = "MessageId";

        /// <summary>
        /// Begins a scope that adds <paramref name="property"/> = <paramref name="id"/> to every event, or
        /// returns <c>null</c> (fine in a <c>using</c>) when there is no id or logger.
        /// </summary>
        public static IDisposable? BeginIdScope(this ILogger? logger, string property, string? id)
        {
            if (logger == null || string.IsNullOrEmpty(property) || string.IsNullOrEmpty(id))
                return null;
            return logger.BeginScope(new Dictionary<string, object> { [property] = id! });
        }
    }
}
