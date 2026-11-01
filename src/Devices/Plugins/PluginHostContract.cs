using System;
using System.Collections.Generic;
using System.Linq;

namespace RIoT2.Core.Plugins
{
    /// <summary>
    /// What the node shares with the plugins it loads. Each plugin gets its own load context; assemblies
    /// matching <see cref="SharedAssemblyPrefixes"/> always come from the node, so a plugin's devices
    /// implement the node's <c>IDevice</c> and use its DI and logging abstractions.
    /// </summary>
    public static class PluginHostContract
    {
        /// <summary>
        /// Assembly name prefixes the node shares. A name matches when it equals a prefix or starts with the
        /// prefix followed by a dot.
        /// </summary>
        public static IReadOnlyList<string> SharedAssemblyPrefixes { get; } = Array.AsReadOnly(new[]
        {
            "RIoT2.Core",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging.Abstractions",
        });

        /// <summary>Whether the node supplies the assembly instead of the plugin.</summary>
        public static bool IsShared(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            return SharedAssemblyPrefixes.Any(prefix =>
                assemblyName.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                assemblyName.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase));
        }
    }
}
