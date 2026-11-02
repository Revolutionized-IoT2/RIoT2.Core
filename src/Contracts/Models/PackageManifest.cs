using Newtonsoft.Json;
using Stj = System.Text.Json.Serialization;

namespace RIoT2.Core.Models
{
    /// <summary>
    /// A package's manifest: the node's <c>Data/Manifest.json</c> and the plugin package's
    /// <c>PluginManifest.json</c> (design 7.2).
    /// </summary>
    public class PackageManifest
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Date { get; set; }
        public string InstalledPackageFilename { get; set; }

        /// <summary>
        /// The <c>RIoT2.Core</c> version a plugin was built against; the node installs it only with the
        /// same major and a minor no newer than its own. Omitted when <c>null</c> (packages before Core 1.12).
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public string? CoreVersion { get; set; }

        /// <summary>
        /// Lower-case hex SHA-256 of the installed plugin zip, written by the node when it installs the
        /// package (a zip can't contain its own hash). Omitted when <c>null</c>.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public string? Sha256 { get; set; }
    }
}