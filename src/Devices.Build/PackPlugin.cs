using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace RIoT2.Core.Plugins.Build
{
    /// <summary>
    /// MSBuild task behind the <c>RIoT2PluginPack</c> target: packs <c>ResolvedFileToPublish</c> with
    /// <see cref="PluginPackager"/>.
    /// </summary>
    public sealed class PackPlugin : Microsoft.Build.Utilities.Task
    {
        /// <summary><c>@(ResolvedFileToPublish)</c>: <c>RelativePath</c>, <c>NuGetPackageId</c> and <c>NuGetPackageVersion</c> are read.</summary>
        [Required]
        public ITaskItem[] Files { get; set; }

        [Required]
        public string PublishDirectory { get; set; }

        [Required]
        public string OutputDirectory { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Version { get; set; }

        [Required]
        public string Package { get; set; }

        public string CoreVersion { get; set; }

        public string[] ExcludePatterns { get; set; }

        [Output]
        public string ZipFile { get; set; }

        [Output]
        public string HashFile { get; set; }

        [Output]
        public string ResolvedCoreVersion { get; set; }

        public override bool Execute()
        {
            var request = new PluginPackageRequest
            {
                Name = Name,
                Version = Version,
                Package = Package,
                CoreVersion = CoreVersion,
                PublishDirectory = PublishDirectory,
                OutputDirectory = OutputDirectory,
            };
            foreach (var item in Files ?? Array.Empty<ITaskItem>())
            {
                var relativePath = item.GetMetadata("RelativePath");
                if (string.IsNullOrEmpty(relativePath))
                    relativePath = Path.GetFileName(item.ItemSpec);
                request.Files.Add(new PluginFile(relativePath, item.GetMetadata("NuGetPackageId"), item.GetMetadata("NuGetPackageVersion")));
            }
            foreach (var pattern in (ExcludePatterns ?? Array.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)))
                request.ExcludePatterns.Add(pattern);

            PluginPackageResult result;
            try
            {
                result = PluginPackager.Pack(request);
            }
            catch (Exception x) when (x is ArgumentException || x is InvalidOperationException || x is IOException || x is UnauthorizedAccessException)
            {
                Log.LogError(null, "RIOT2PLUGIN001", null, null, 0, 0, 0, 0, "RIoT2 plugin pack failed: {0}", x.Message);
                return false;
            }

            ZipFile = result.ZipFile;
            HashFile = result.HashFile;
            ResolvedCoreVersion = result.CoreVersion;
            foreach (var entry in result.Entries)
                Log.LogMessage(MessageImportance.Normal, "  {0}", entry);
            Log.LogMessage(MessageImportance.High, "{0} -> {1} (coreVersion {2}, sha256 {3})", Package, ZipFile, result.CoreVersion, result.Sha256);
            return true;
        }
    }
}
