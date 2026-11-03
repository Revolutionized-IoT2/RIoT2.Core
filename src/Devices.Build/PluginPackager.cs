using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RIoT2.Core.Plugins.Build
{
    /// <summary>A file from the publish output, as MSBuild's <c>ResolvedFileToPublish</c> item describes it.</summary>
    public sealed class PluginFile
    {
        public PluginFile(string relativePath, string? packageId = null, string? packageVersion = null)
        {
            RelativePath = relativePath;
            PackageId = packageId;
            PackageVersion = packageVersion;
        }

        /// <summary>Path relative to the publish directory.</summary>
        public string RelativePath { get; }

        /// <summary>The NuGet package the file comes from, if any.</summary>
        public string? PackageId { get; }

        public string? PackageVersion { get; }
    }

    /// <summary>What goes into a plugin zip and where it is written.</summary>
    public sealed class PluginPackageRequest
    {
        /// <summary>Display name in <c>PluginManifest.json</c>.</summary>
        public string Name { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        /// <summary>Zip file name prefix: the zip is <c>&lt;Package&gt;_&lt;Version&gt;.zip</c>.</summary>
        public string Package { get; set; } = string.Empty;

        /// <summary>
        /// The plugin SDK version for <c>PluginManifest.json</c>. When empty, the version of the
        /// <c>RIoT2.Core.Devices</c> package in <see cref="Files"/> is used, or of <c>RIoT2.Core</c> for a
        /// plugin built on the facade.
        /// </summary>
        public string? CoreVersion { get; set; }

        /// <summary>The publish directory that <see cref="PluginFile.RelativePath"/> is relative to.</summary>
        public string PublishDirectory { get; set; } = string.Empty;

        public string OutputDirectory { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.UtcNow;

        public IList<PluginFile> Files { get; } = new List<PluginFile>();

        /// <summary>
        /// File name patterns (<c>*</c> and <c>?</c>) of assemblies the node provides. Assemblies in
        /// <see cref="PluginHostContract.SharedAssemblyPrefixes"/> are always left out.
        /// </summary>
        public IList<string> ExcludePatterns { get; } = new List<string>();
    }

    public sealed class PluginPackageResult
    {
        internal PluginPackageResult(string zipFile, string hashFile, string sha256, string coreVersion, IReadOnlyList<string> entries)
        {
            ZipFile = zipFile;
            HashFile = hashFile;
            Sha256 = sha256;
            CoreVersion = coreVersion;
            Entries = entries;
        }

        public string ZipFile { get; }

        /// <summary>The <c>.sha256</c> sidecar next to the zip, in <c>sha256sum</c> format.</summary>
        public string HashFile { get; }

        /// <summary>Lower-case hex SHA-256 of the zip.</summary>
        public string Sha256 { get; }

        public string CoreVersion { get; }

        /// <summary>Entry names in the zip, <c>PluginManifest.json</c> last.</summary>
        public IReadOnlyList<string> Entries { get; }
    }

    /// <summary>
    /// Builds the plugin zip the node installs (plan M12 step 3): the plugin's own top-level assemblies and
    /// native libraries from the publish output, without what the node shares or provides, plus
    /// <c>PluginManifest.json</c>, and a <c>.sha256</c> sidecar next to the zip.
    /// </summary>
    public static class PluginPackager
    {
        public const string ManifestFileName = "PluginManifest.json";

        private static readonly string[] CorePackages = { "RIoT2.Core.Devices" };

        public static string ZipFileName(string package, string version) => package + "_" + version + ".zip";

        /// <summary>Whether a top-level publish file belongs in the zip.</summary>
        public static bool Includes(string fileName, IEnumerable<string>? excludePatterns)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            var extension = Path.GetExtension(fileName);
            if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                if (PluginHostContract.IsShared(Path.GetFileNameWithoutExtension(fileName)))
                    return false;
            }
            else if (!extension.Equals(".so", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !(excludePatterns ?? Enumerable.Empty<string>()).Any(pattern => Matches(fileName, pattern));
        }

        /// <summary>
        /// The version of <c>RIoT2.Core.Devices</c> the plugin is built on:
        /// the package version, or the assembly version of a project reference. Null when neither is found.
        /// </summary>
        public static string? ResolveCoreVersion(IEnumerable<PluginFile> files, string? publishDirectory)
        {
            var list = files.ToList();
            foreach (var package in CorePackages)
            {
                var fromPackage = list.FirstOrDefault(f =>
                    string.Equals(f.PackageId, package, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(f.PackageVersion));
                if (fromPackage != null)
                    return fromPackage.PackageVersion;

                var assembly = list.FirstOrDefault(f => string.Equals(f.RelativePath, package + ".dll", StringComparison.OrdinalIgnoreCase));
                var path = assembly == null ? null : Path.Combine(publishDirectory ?? string.Empty, assembly.RelativePath);
                if (path != null && File.Exists(path))
                {
                    var version = AssemblyName.GetAssemblyName(path).Version!;
                    return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", version.Major, version.Minor, Math.Max(version.Build, 0));
                }
            }
            return null;
        }

        public static string CreateManifest(string name, string version, string coreVersion, string zipFileName, DateTime date)
        {
            var builder = new StringBuilder();
            builder.Append("{\n");
            AppendProperty(builder, "name", name, last: false);
            AppendProperty(builder, "date", date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture), last: false);
            AppendProperty(builder, "version", version, last: false);
            AppendProperty(builder, "installedPackageFilename", zipFileName, last: false);
            AppendProperty(builder, "coreVersion", coreVersion, last: true);
            builder.Append("}\n");
            return builder.ToString();
        }

        public static PluginPackageResult Pack(PluginPackageRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            Require(request.Name, nameof(request.Name));
            Require(request.Version, nameof(request.Version));
            Require(request.Package, nameof(request.Package));
            Require(request.PublishDirectory, nameof(request.PublishDirectory));
            Require(request.OutputDirectory, nameof(request.OutputDirectory));

            var zipName = ZipFileName(request.Package, request.Version);
            if (zipName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException($"'{zipName}' is not a valid file name: check the plugin package name and version.", nameof(request));

            var coreVersion = string.IsNullOrWhiteSpace(request.CoreVersion)
                ? ResolveCoreVersion(request.Files, request.PublishDirectory)
                : request.CoreVersion;
            if (coreVersion == null || coreVersion.Trim().Length == 0)
                throw new InvalidOperationException("The plugin references neither RIoT2.Core.Devices nor RIoT2.Core, so coreVersion is unknown. Set RIoT2PluginCoreVersion.");

            var selected = request.Files
                .Where(f => !string.IsNullOrEmpty(f.RelativePath) && f.RelativePath.IndexOfAny(new[] { '/', '\\' }) < 0)
                .Where(f => Includes(f.RelativePath, request.ExcludePatterns))
                .GroupBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First().RelativePath)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            if (!selected.Any(n => n.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("The publish output has no plugin assembly to pack.");

            Directory.CreateDirectory(request.OutputDirectory);
            var zipPath = Path.Combine(request.OutputDirectory, zipName);
            var manifest = CreateManifest(request.Name, request.Version, coreVersion, zipName, request.Date);
            // Zip entry times can't be before 1980.
            var entryTime = new DateTimeOffset(request.Date.Year < 1980 ? new DateTime(1980, 1, 1) : request.Date);

            var entries = new List<string>();
            using (var stream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var name in selected)
                {
                    var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                    entry.LastWriteTime = entryTime;
                    using (var source = File.OpenRead(Path.Combine(request.PublishDirectory, name)))
                    using (var target = entry.Open())
                        source.CopyTo(target);
                    entries.Add(name);
                }

                var manifestEntry = zip.CreateEntry(ManifestFileName, CompressionLevel.Optimal);
                manifestEntry.LastWriteTime = entryTime;
                using (var target = manifestEntry.Open())
                {
                    var bytes = new UTF8Encoding(false).GetBytes(manifest);
                    target.Write(bytes, 0, bytes.Length);
                }
                entries.Add(ManifestFileName);
            }

            var sha256 = ComputeSha256(zipPath);
            var hashPath = zipPath + ".sha256";
            // sha256sum format, so "sha256sum -c" verifies it.
            File.WriteAllText(hashPath, sha256 + "  " + zipName + "\n", new UTF8Encoding(false));
            return new PluginPackageResult(zipPath, hashPath, sha256, coreVersion, entries);
        }

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

        internal static bool Matches(string fileName, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return false;
            var regex = "^" + Regex.Escape(pattern.Trim()).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            return Regex.IsMatch(fileName, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static void Require(string? value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{name} is required.", name);
        }

        private static void AppendProperty(StringBuilder builder, string name, string value, bool last)
        {
            builder.Append("  \"").Append(name).Append("\": \"");
            foreach (var c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(c);
                        break;
                }
            }
            builder.Append('"').Append(last ? "\n" : ",\n");
        }
    }
}
