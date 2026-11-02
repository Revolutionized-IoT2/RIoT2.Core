using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RIoT2.Core.Models;
using RIoT2.Core.Utils;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RIoT2.Core.Plugins
{
    /// <summary>
    /// Updates a node's plugin package safely (design 7.2 phase 4, backlog item 5):
    /// <list type="number">
    /// <item><see cref="UpdateAsync"/>, while the node runs: compares the package's <c>.sha256</c> sidecar with the
    /// installed manifest's <c>sha256</c>, downloads a changed package, verifies its hash, extracts it into
    /// <c>Plugins.new/</c> and checks its <c>coreVersion</c>. A refused package never touches <c>Plugins/</c>.</item>
    /// <item><see cref="PromoteStaged"/>, at the next start before plugins load: moves the contents of <c>Plugins/</c>
    /// to <c>Plugins.previous/</c> and those of <c>Plugins.new/</c> to <c>Plugins/</c>. Contents move, not the folder,
    /// because <c>Plugins/</c> is usually a bind mount. A journal in <c>Data/</c> lets an interrupted swap resume.</item>
    /// <item><see cref="ConfirmStartup"/>, once plugins have loaded: keeps the package, or, when it didn't load, puts the
    /// previous plugins back and remembers the package as refused, so the node restarts once and never loops.</item>
    /// </list>
    /// </summary>
    public sealed class PluginPackageInstaller
    {
        public const string ManifestFileName = "PluginManifest.json";
        public const string JournalFileName = "plugins.install.json";
        public const string RejectionFileName = "plugins.rejected.json";
        private const string DownloadFileName = "plugin-package.download";

        private const string PhaseStaged = "staged";
        private const string PhaseBackup = "backup";
        private const string PhasePromote = "promote";
        private const string PhaseInstalled = "installed";
        private const string PhaseRollingBack = "rollingBack";
        private const string PhaseRestoring = "restoring";

        private readonly Version _hostCoreVersion;
        private readonly ILogger _logger;
        private readonly Func<DateTime> _utcNow;

        /// <param name="pluginsFolder">The folder plugins load from (<c>/app/Plugins</c> in the image).</param>
        /// <param name="dataFolder">The node's <c>Data</c> folder, for the journal, the refused package and the download.</param>
        /// <param name="hostCoreVersion">The node's Core version; <see cref="PluginCompatibility.HostCoreVersion"/> by default.</param>
        public PluginPackageInstaller(string pluginsFolder, string dataFolder, Version? hostCoreVersion = null, ILogger? logger = null, Func<DateTime>? utcNow = null)
        {
            if (string.IsNullOrEmpty(pluginsFolder))
                throw new ArgumentException("A plugins folder is required.", nameof(pluginsFolder));
            if (string.IsNullOrEmpty(dataFolder))
                throw new ArgumentException("A data folder is required.", nameof(dataFolder));
            PluginsFolder = Path.GetFullPath(pluginsFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            StagingFolder = PluginsFolder + ".new";
            PreviousFolder = PluginsFolder + ".previous";
            DataFolder = Path.GetFullPath(dataFolder);
            _hostCoreVersion = hostCoreVersion ?? PluginCompatibility.HostCoreVersion;
            _logger = logger ?? NullLogger.Instance;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public string PluginsFolder { get; }

        /// <summary><c>Plugins.new/</c>: a verified package waiting for the next start.</summary>
        public string StagingFolder { get; }

        /// <summary><c>Plugins.previous/</c>: the plugins before the last install, for one rollback.</summary>
        public string PreviousFolder { get; }

        public string DataFolder { get; }

        /// <summary>The last package refused for good, or <c>null</c>.</summary>
        public PluginRejection? LastRejection => Read<PluginRejection>(Path.Combine(DataFolder, RejectionFileName));

        /// <summary>The sidecar's URL: <c>.sha256</c> appended to the package URL's path, keeping its query.</summary>
        public static string SidecarUrl(string packageUrl)
        {
            var builder = new UriBuilder(new Uri(packageUrl, UriKind.Absolute));
            builder.Path += ".sha256";
            return builder.Uri.AbsoluteUri;
        }

        /// <summary>The last segment of the URL's path, or <c>null</c>.</summary>
        public static string? PackageFileName(string packageUrl)
        {
            if (!Uri.TryCreate(packageUrl, UriKind.Absolute, out var uri))
                return null;
            var name = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));
            return string.IsNullOrEmpty(name) ? null : name;
        }

        /// <summary>The hash in a <c>sha256sum</c> line (<c>&lt;hash&gt;  &lt;file&gt;</c>) or a bare hash, lower case; <c>null</c> when there is none.</summary>
        public static string? ParseSidecar(string? content)
        {
            if (content == null)
                return null;
            var token = content.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (token.Length == 0)
                return null;
            var hash = token[0].TrimStart('\\').ToLowerInvariant();
            if (hash.Length != 64)
                return null;
            foreach (var c in hash)
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f'))
                    return null;
            return hash;
        }

        /// <summary>Lower-case hex SHA-256 of the file.</summary>
        public static string ComputeSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        /// <summary>
        /// Checks the package at <paramref name="packageUrl"/> against the installed one and stages it when it changed.
        /// Plugins that name no package (copied in by hand) are kept. Without a sidecar the file name is compared and
        /// the package is installed without a hash check, as before; a package refused for good is not tried again.
        /// </summary>
        /// <param name="installed">The installed <c>PluginManifest.json</c>.</param>
        /// <param name="pluginsLoaded">Whether any plugin loaded at this start; when none did, the package is installed again.</param>
        /// <param name="send">Sends a request: the node's <c>plugins</c> HTTP client, with the outbound URL policy.</param>
        public async Task<PluginUpdateResult> UpdateAsync(string packageUrl, PackageManifest? installed, bool pluginsLoaded,
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send, CancellationToken cancellationToken = default)
        {
            if (send == null)
                throw new ArgumentNullException(nameof(send));
            if (!Uri.TryCreate(packageUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return PluginUpdateResult.Rejected($"The plugin package URL '{packageUrl}' is not an absolute http or https URL.");
            var unfinished = UnfinishedInstall();
            if (unfinished != null)
                return PluginUpdateResult.Unavailable(unfinished);
            if (pluginsLoaded && IsEmpty(installed?.InstalledPackageFilename) && IsEmpty(installed?.Sha256))
            {
                _logger.LogInformation("The loaded plugins name no package (installed by hand); they are kept.");
                return PluginUpdateResult.Unchanged;
            }

            var sidecarUrl = SidecarUrl(packageUrl);
            string? expected;
            using (var request = new HttpRequestMessage(HttpMethod.Get, sidecarUrl))
            {
                HttpResponseMessage response;
                try { response = await send(request, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception error)
                {
                    return PluginUpdateResult.Unavailable($"Could not fetch {sidecarUrl}: {error.GetBaseException().Message}");
                }
                using (response)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        expected = null;
                    else if (!response.IsSuccessStatusCode)
                        return PluginUpdateResult.Unavailable($"Could not fetch {sidecarUrl}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                    else
                    {
                        expected = ParseSidecar(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                        if (expected == null)
                            return PluginUpdateResult.Rejected($"{sidecarUrl} doesn't hold a SHA-256 hash.");
                    }
                }
            }

            var rejection = LastRejection;
            string packageName;
            if (expected != null)
            {
                packageName = PackageFileName(packageUrl) ?? "plugin.zip";
                // A package installed by a node before 1.14.0 has no sha256; its file name identifies it.
                if (pluginsLoaded && installed != null &&
                    (Same(installed.Sha256, expected) || (IsEmpty(installed.Sha256) && Same(installed.InstalledPackageFilename, packageName))))
                    return PluginUpdateResult.Unchanged;
                if (rejection != null && Same(rejection.Sha256, expected))
                    return PluginUpdateResult.Rejected(rejection.Reason);
            }
            else
            {
                var head = await HeadFileNameAsync(packageUrl, send, cancellationToken).ConfigureAwait(false);
                if (head.Name == null)
                    return PluginUpdateResult.Unavailable(head.Error ?? $"Could not read {packageUrl}.");
                packageName = head.Name;
                if (pluginsLoaded && installed != null && Same(installed.InstalledPackageFilename, packageName))
                    return PluginUpdateResult.Unchanged;
                if (rejection != null && Same(rejection.PackageFilename, packageName))
                    return PluginUpdateResult.Rejected(rejection.Reason);
            }

            Directory.CreateDirectory(DataFolder);
            var download = Path.Combine(DataFolder, DownloadFileName);
            try
            {
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, packageUrl))
                    using (var response = await send(request, cancellationToken).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                            return PluginUpdateResult.Unavailable($"Could not download {packageUrl}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                        using (var file = new FileStream(download, FileMode.Create, FileAccess.Write, FileShare.None))
                            await response.Content.CopyToAsync(file).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception error) when (error is HttpRequestException || error is OperationCanceledException || error is IOException)
                {
                    return PluginUpdateResult.Unavailable($"Could not download {packageUrl}: {error.GetBaseException().Message}");
                }

                var actual = ComputeSha256(download);
                if (expected != null && !Same(actual, expected))
                    return PluginUpdateResult.Rejected($"The plugin package {packageName} doesn't match its .sha256 sidecar: expected {expected}, downloaded {actual}.");
                if (expected == null)
                    _logger.LogWarning("The plugin package {Package} has no .sha256 sidecar; it is installed without a hash check.", packageName);

                try
                {
                    return PluginUpdateResult.Staged(Stage(download, packageName, actual));
                }
                catch (PluginPackageException error)
                {
                    Reject(actual, packageName, error.Message);
                    return PluginUpdateResult.Rejected(error.Message);
                }
            }
            finally
            {
                TryDelete(download);
            }
        }

        /// <summary>
        /// Extracts the zip into <c>Plugins.new/</c>, checks its <c>coreVersion</c>, writes <c>sha256</c> (and the file
        /// name, if missing) into its manifest, and records it for <see cref="PromoteStaged"/>. Throws
        /// <see cref="PluginPackageException"/> for a package that can't be installed, leaving no <c>Plugins.new/</c>.
        /// </summary>
        public PackageManifest Stage(string zipPath, string packageFileName, string? sha256 = null)
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException("The plugin package doesn't exist.", zipPath);
            var unfinished = UnfinishedInstall();
            if (unfinished != null)
                throw new InvalidOperationException(unfinished);
            sha256 = sha256 ?? ComputeSha256(zipPath);
            DeleteDirectory(StagingFolder);
            try
            {
                Extract(zipPath, StagingFolder, packageFileName);
                var manifestPath = Path.Combine(StagingFolder, ManifestFileName);
                PackageManifest? manifest = null;
                if (File.Exists(manifestPath))
                {
                    try { manifest = Json.Deserialize<PackageManifest>(File.ReadAllText(manifestPath)); }
                    catch (Exception error)
                    {
                        throw new PluginPackageException($"The plugin package {packageFileName} has an unreadable {ManifestFileName}: {error.GetBaseException().Message}", error);
                    }
                }
                manifest = manifest ?? new PackageManifest { Name = Path.GetFileNameWithoutExtension(packageFileName) };

                if (!PluginCompatibility.IsCompatible(manifest.CoreVersion, _hostCoreVersion, out var reason))
                    throw new PluginPackageException($"The plugin package {packageFileName} can't run on this node. {reason}");
                if (IsEmpty(manifest.CoreVersion))
                    _logger.LogWarning("The plugin package {Package} doesn't say which RIoT2.Core it was built against; it is installed anyway.", packageFileName);
                if (IsEmpty(manifest.InstalledPackageFilename))
                    manifest.InstalledPackageFilename = packageFileName;
                manifest.Sha256 = sha256;
                File.WriteAllText(manifestPath, Json.SerializeIgnoreNulls(manifest) ?? "{}", new UTF8Encoding(false));

                WriteJournal(new PluginInstallJournal { Phase = PhaseStaged, Package = manifest, TimeStamp = Now() });
                _logger.LogInformation("Plugin package {Package} {Version} (coreVersion {CoreVersion}) is verified and staged; it is installed at the next start.",
                    manifest.InstalledPackageFilename, manifest.Version, manifest.CoreVersion ?? "unknown");
                return manifest;
            }
            catch
            {
                DeleteDirectory(StagingFolder);
                throw;
            }
        }

        /// <summary>
        /// At start, before plugins load: installs a staged package, or finishes an interrupted install or rollback.
        /// Returns the package installed at this start, which <see cref="ConfirmStartup"/> must confirm, or <c>null</c>.
        /// A swap that fails puts the previous plugins back and refuses the package, so it isn't staged again.
        /// </summary>
        public PackageManifest? PromoteStaged()
        {
            var journal = ReadJournal();
            if (journal == null)
            {
                if (Directory.Exists(StagingFolder))
                {
                    _logger.LogWarning("Removing {Folder}, left by an interrupted plugin download.", StagingFolder);
                    DeleteDirectory(StagingFolder);
                }
                return null;
            }

            try
            {
                switch (journal.Phase)
                {
                    case PhaseStaged:
                        if (!Directory.Exists(StagingFolder))
                        {
                            _logger.LogWarning("The staged plugin package {Package} is gone; nothing to install.", Describe(journal.Package));
                            DeleteJournal();
                            return null;
                        }
                        DeleteDirectory(PreviousFolder);
                        journal.HadPrevious = HasContents(PluginsFolder);
                        journal.Phase = PhaseBackup;
                        WriteJournal(journal);
                        goto case PhaseBackup;
                    case PhaseBackup:
                        if (!Directory.Exists(StagingFolder))
                            return Abandon(journal, clearPlugins: false);
                        MoveContents(PluginsFolder, PreviousFolder);
                        journal.Phase = PhasePromote;
                        WriteJournal(journal);
                        goto case PhasePromote;
                    case PhasePromote:
                        if (!Directory.Exists(StagingFolder))
                            return Abandon(journal, clearPlugins: true);
                        MoveContents(StagingFolder, PluginsFolder);
                        journal.Phase = PhaseInstalled;
                        WriteJournal(journal);
                        TryDeleteDirectory(StagingFolder);
                        _logger.LogInformation("Installed plugin package {Package}; the previous plugins are in {Folder} until it loads.",
                            Describe(journal.Package), PreviousFolder);
                        return journal.Package;
                    case PhaseInstalled:
                        return journal.Package;
                    case PhaseRollingBack:
                        RollBack(journal, clearPlugins: true);
                        return null;
                    case PhaseRestoring:
                        RollBack(journal, clearPlugins: false);
                        return null;
                    default:
                        _logger.LogWarning("Ignoring {Journal} with the unknown phase '{Phase}'.", JournalFileName, journal.Phase);
                        DeleteJournal();
                        return null;
                }
            }
            catch (Exception error) when (journal.Phase == PhaseBackup || journal.Phase == PhasePromote)
            {
                var reason = $"The plugin package {Describe(journal.Package)} could not be installed: {error.GetBaseException().Message}";
                _logger.LogError(error, "{Reason} Restoring the previous plugins.", reason);
                Reject(journal.Package?.Sha256, journal.Package?.InstalledPackageFilename, reason);
                RollBack(journal, clearPlugins: journal.Phase == PhasePromote);
                TryDeleteDirectory(StagingFolder);
                return null;
            }
        }

        /// <summary>
        /// After plugins loaded at start. For a package installed at this start: when <paramref name="loaded"/>, keeps it.
        /// Otherwise remembers it as refused and, if there were previous plugins, puts them back
        /// (<see cref="PluginStartupOutcome.RolledBack"/>: restart the node once to load them); without previous plugins it
        /// stays, with what of it did load (<see cref="PluginStartupOutcome.Failed"/>).
        /// </summary>
        /// <param name="error">Why loading failed, for the status and the log.</param>
        public PluginStartupResult ConfirmStartup(bool loaded, string? error = null)
        {
            var journal = ReadJournal();
            if (journal == null || journal.Phase != PhaseInstalled)
                return PluginStartupResult.None;

            var package = journal.Package;
            if (loaded)
            {
                DeleteJournal();
                return new PluginStartupResult(PluginStartupOutcome.Installed, package, null);
            }

            var reason = $"The plugin package {Describe(package)} didn't load: {(IsEmpty(error) ? "it has no plugin." : error)}";
            Reject(package?.Sha256, package?.InstalledPackageFilename, reason);
            if (journal.HadPrevious && Directory.Exists(PreviousFolder))
            {
                RollBack(journal, clearPlugins: true);
                return new PluginStartupResult(PluginStartupOutcome.RolledBack, package, reason + " The previous plugins are restored.");
            }
            TryDeleteDirectory(PreviousFolder);
            DeleteJournal();
            return new PluginStartupResult(PluginStartupOutcome.Failed, package, reason);
        }

        private PackageManifest? Abandon(PluginInstallJournal journal, bool clearPlugins)
        {
            _logger.LogError("The staged plugin package {Package} disappeared during its install; restoring the previous plugins.", Describe(journal.Package));
            RollBack(journal, clearPlugins);
            return null;
        }

        // Puts Plugins.previous/ back. Each step is recorded first, so an interrupted rollback resumes without
        // clearing what it already restored: "rollingBack" clears Plugins/ (nothing restored yet), "restoring" only
        // moves the rest of Plugins.previous/ back.
        private void RollBack(PluginInstallJournal journal, bool clearPlugins)
        {
            if (!Directory.Exists(PreviousFolder))
            {
                _logger.LogWarning("{Folder} is gone; the plugins stay as they are.", PreviousFolder);
                DeleteJournal();
                return;
            }
            if (clearPlugins)
            {
                journal.Phase = PhaseRollingBack;
                WriteJournal(journal);
                ClearContents(PluginsFolder);
            }
            journal.Phase = PhaseRestoring;
            WriteJournal(journal);
            MoveContents(PreviousFolder, PluginsFolder);
            DeleteDirectory(PreviousFolder);
            DeleteJournal();
        }

        private void Reject(string? sha256, string? packageFilename, string reason)
        {
            _logger.LogError("Refusing plugin package {Package}: {Reason}", packageFilename, reason);
            try
            {
                WriteAtomically(Path.Combine(DataFolder, RejectionFileName), Json.SerializeIgnoreNulls(new PluginRejection
                {
                    Sha256 = sha256,
                    PackageFilename = packageFilename,
                    Reason = reason,
                    TimeStamp = Now()
                }) ?? "{}");
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                _logger.LogWarning(error, "Could not record the refused plugin package; it may be tried again.");
            }
        }

        private async Task<(string? Name, string? Error)> HeadFileNameAsync(string packageUrl,
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Head, packageUrl))
                using (var response = await send(request, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        return (null, $"Could not read {packageUrl}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                    var disposition = response.Content.Headers.ContentDisposition;
                    var name = (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"');
                    name = string.IsNullOrEmpty(name) ? PackageFileName(packageUrl) : Path.GetFileName(name);
                    return (name ?? "plugin.zip", null);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                return (null, $"Could not read {packageUrl}: {error.GetBaseException().Message}");
            }
        }

        private static void Extract(string zipPath, string destination, string packageFileName)
        {
            Directory.CreateDirectory(destination);
            var root = Path.GetFullPath(destination);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root += Path.DirectorySeparatorChar;

            ZipArchive archive;
            try { archive = ZipFile.OpenRead(zipPath); }
            catch (InvalidDataException error)
            {
                throw new PluginPackageException($"The plugin package {packageFileName} is not a zip file.", error);
            }
            using (archive)
            {
                foreach (var entry in archive.Entries)
                {
                    var fullPath = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new PluginPackageException($"The plugin package {packageFileName} contains an entry outside the plugin folder: {entry.FullName}");
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(fullPath);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    entry.ExtractToFile(fullPath, true);
                }
            }
        }

        // Moves what is in source into target, merging folders. File.Move falls back to copying between file
        // systems (a bind-mounted Plugins/ and the container's /app); Directory.Move doesn't, so folders are walked.
        private static void MoveContents(string source, string target)
        {
            Directory.CreateDirectory(target);
            if (!Directory.Exists(source))
                return;
            foreach (var file in Directory.GetFiles(source))
            {
                var destination = Path.Combine(target, Path.GetFileName(file));
                if (File.Exists(destination))
                    File.Delete(destination);
                File.Move(file, destination);
            }
            foreach (var directory in Directory.GetDirectories(source))
            {
                var destination = Path.Combine(target, Path.GetFileName(directory));
                if (!Directory.Exists(destination))
                {
                    try
                    {
                        Directory.Move(directory, destination);
                        continue;
                    }
                    catch (IOException) { }
                }
                MoveContents(directory, destination);
                Directory.Delete(directory, recursive: true);
            }
        }

        private static void ClearContents(string folder)
        {
            if (!Directory.Exists(folder))
                return;
            foreach (var file in Directory.GetFiles(folder))
                File.Delete(file);
            foreach (var directory in Directory.GetDirectories(folder))
                Directory.Delete(directory, recursive: true);
        }

        private static void DeleteDirectory(string folder)
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }

        private void TryDeleteDirectory(string folder)
        {
            try { DeleteDirectory(folder); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                _logger.LogWarning(error, "Could not remove {Folder}.", folder);
            }
        }

        private static bool HasContents(string folder) =>
            Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any();

        private static void TryDelete(string file)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
        }

        // A swap or rollback that only the next start can finish; staging again would overwrite its journal.
        private string? UnfinishedInstall()
        {
            var journal = ReadJournal();
            return journal == null || journal.Phase == PhaseStaged
                ? null
                : $"The install of the plugin package {Describe(journal.Package)} is unfinished ({journal.Phase}); the node finishes it at its next start.";
        }

        private PluginInstallJournal? ReadJournal() => Read<PluginInstallJournal>(Path.Combine(DataFolder, JournalFileName));

        private void WriteJournal(PluginInstallJournal journal) =>
            WriteAtomically(Path.Combine(DataFolder, JournalFileName), Json.SerializeIgnoreNulls(journal) ?? "{}");

        private void DeleteJournal() => TryDelete(Path.Combine(DataFolder, JournalFileName));

        private T? Read<T>(string path) where T : class
        {
            if (!File.Exists(path))
                return null;
            try { return Json.Deserialize<T>(File.ReadAllText(path)); }
            catch (Exception error)
            {
                _logger.LogWarning(error, "Ignoring the unreadable {File}.", path);
                return null;
            }
        }

        private static void WriteAtomically(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }

        private long Now() => new DateTimeOffset(DateTime.SpecifyKind(_utcNow(), DateTimeKind.Utc)).ToUnixTimeSeconds();

        private static string Describe(PackageManifest? package) =>
            package == null ? "(unknown)" : $"{package.InstalledPackageFilename ?? package.Name} {package.Version}".TrimEnd();

        private static bool IsEmpty(string? value) => value == null || value.Length == 0;

        private static bool Same(string? a, string? b) => !IsEmpty(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A plugin package that can't be installed: not a zip, unsafe entries, an unreadable manifest, or an incompatible <c>coreVersion</c>.</summary>
    public sealed class PluginPackageException : Exception
    {
        public PluginPackageException(string message, Exception? inner = null) : base(message, inner) { }
    }

    internal sealed class PluginInstallJournal
    {
        public string Phase { get; set; } = string.Empty;
        public PackageManifest? Package { get; set; }

        /// <summary>Whether <c>Plugins/</c> had anything before the install, so a rollback has something to go back to.</summary>
        public bool HadPrevious { get; set; }

        public long TimeStamp { get; set; }
    }
}
