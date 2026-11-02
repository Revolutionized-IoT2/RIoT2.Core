using RIoT2.Core.Interfaces;
using System;

namespace RIoT2.Core.Plugins
{
    /// <summary>
    /// Whether a plugin built against one <c>RIoT2.Core</c> version can run in a node with another (design 7.2,
    /// decided 2026-10-12): the same major, and a minor no newer than the node's. Core minor releases are
    /// additive (ADR 0005), so a plugin doesn't need a rebuild for every Core minor.
    /// </summary>
    public static class PluginCompatibility
    {
        /// <summary>The <c>RIoT2.Core</c> version this node runs (the version of <c>RIoT2.Core.Devices</c>, which plugins build on).</summary>
        public static Version HostCoreVersion { get; } = typeof(IDevice).Assembly.GetName().Version ?? new Version(0, 0);

        /// <summary>
        /// <c>true</c> when a plugin built against <paramref name="coreVersion"/> runs on <paramref name="hostCoreVersion"/>.
        /// A missing version (packages from before Core 1.12) is accepted; the caller warns about it.
        /// Otherwise <paramref name="reason"/> says why not.
        /// </summary>
        public static bool IsCompatible(string? coreVersion, Version hostCoreVersion, out string? reason)
        {
            if (hostCoreVersion == null)
                throw new ArgumentNullException(nameof(hostCoreVersion));
            reason = null;
            if (coreVersion == null || coreVersion.Trim().Length == 0)
                return true;

            var text = coreVersion.Trim();
            var suffix = text.IndexOfAny(new[] { '-', '+' });
            if (suffix >= 0)
                text = text.Substring(0, suffix);
            if (!Version.TryParse(text, out var version))
            {
                reason = $"Its coreVersion '{coreVersion}' is not a version.";
                return false;
            }

            var host = $"{hostCoreVersion.Major}.{hostCoreVersion.Minor}";
            if (version.Major != hostCoreVersion.Major)
                reason = $"It was built against RIoT2.Core {coreVersion}, and this node runs RIoT2.Core {host}: the major versions differ.";
            else if (version.Minor > hostCoreVersion.Minor)
                reason = $"It was built against RIoT2.Core {coreVersion}, newer than this node's RIoT2.Core {host}: update the node first.";
            return reason == null;
        }
    }
}
