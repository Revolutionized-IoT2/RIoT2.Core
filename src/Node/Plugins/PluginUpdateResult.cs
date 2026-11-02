using RIoT2.Core.Models;

namespace RIoT2.Core.Plugins
{
    /// <summary>What <see cref="PluginPackageInstaller.UpdateAsync"/> did.</summary>
    public enum PluginUpdateOutcome
    {
        /// <summary>The installed package is the one configured, or there is nothing to check.</summary>
        Unchanged,

        /// <summary>A verified, compatible package is in <c>Plugins.new/</c>; the node restarts to install it.</summary>
        Staged,

        /// <summary>The package was refused (hash mismatch, incompatible <c>coreVersion</c>, not loadable); <c>Plugins/</c> is untouched.</summary>
        Rejected,

        /// <summary>The package couldn't be checked or downloaded right now; the node keeps its plugins.</summary>
        Unavailable
    }

    /// <summary>The result of a plugin update check (design 7.2).</summary>
    public sealed class PluginUpdateResult
    {
        private PluginUpdateResult(PluginUpdateOutcome outcome, string? message, PackageManifest? package)
        {
            Outcome = outcome;
            Message = message;
            Package = package;
        }

        public PluginUpdateOutcome Outcome { get; }

        /// <summary>Why the package was rejected or is unavailable.</summary>
        public string? Message { get; }

        /// <summary>The staged package's manifest.</summary>
        public PackageManifest? Package { get; }

        public static PluginUpdateResult Unchanged { get; } = new PluginUpdateResult(PluginUpdateOutcome.Unchanged, null, null);

        public static PluginUpdateResult Staged(PackageManifest package) => new PluginUpdateResult(PluginUpdateOutcome.Staged, null, package);

        public static PluginUpdateResult Rejected(string message) => new PluginUpdateResult(PluginUpdateOutcome.Rejected, message, null);

        public static PluginUpdateResult Unavailable(string message) => new PluginUpdateResult(PluginUpdateOutcome.Unavailable, message, null);
    }

    /// <summary>What <see cref="PluginPackageInstaller.ConfirmStartup"/> found.</summary>
    public enum PluginStartupOutcome
    {
        /// <summary>No package was installed at this start.</summary>
        None,

        /// <summary>The package installed at this start loaded.</summary>
        Installed,

        /// <summary>The package installed at this start didn't load, and the previous plugins are back: restart the node once.</summary>
        RolledBack,

        /// <summary>The package installed at this start didn't load, and there were no previous plugins to go back to.</summary>
        Failed
    }

    /// <summary>The result of <see cref="PluginPackageInstaller.ConfirmStartup"/>.</summary>
    public sealed class PluginStartupResult
    {
        internal PluginStartupResult(PluginStartupOutcome outcome, PackageManifest? package, string? message)
        {
            Outcome = outcome;
            Package = package;
            Message = message;
        }

        public PluginStartupOutcome Outcome { get; }

        /// <summary>The package installed at this start.</summary>
        public PackageManifest? Package { get; }

        /// <summary>Why the package was rolled back or failed.</summary>
        public string? Message { get; }

        public static PluginStartupResult None { get; } = new PluginStartupResult(PluginStartupOutcome.None, null, null);
    }

    /// <summary>
    /// The last package the node refused for good, kept in <c>Data/plugins.rejected.json</c> so that the same
    /// package isn't downloaded or installed again. A package with another hash (or file name, without a
    /// sidecar) is checked as usual; delete the file to retry the same one.
    /// </summary>
    public sealed class PluginRejection
    {
        /// <summary>Hash of the refused zip, when it is known.</summary>
        public string? Sha256 { get; set; }

        public string? PackageFilename { get; set; }

        public string Reason { get; set; } = string.Empty;

        /// <summary>When it was refused, in Unix seconds.</summary>
        public long TimeStamp { get; set; }
    }
}
