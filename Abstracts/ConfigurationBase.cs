using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;
using RIoT2.Core.Utils;
using System;
using System.IO;

namespace RIoT2.Core.Abstracts
{
    /// <summary>
    /// Provides a base implementation of <see cref="IConfiguration"/> shared by node and
    /// orchestrator configurations, including manifest loading from the application folder.
    /// </summary>
    public abstract class ConfigurationBase : IConfiguration
    {
        /// <inheritdoc/>
        public virtual string Id { get; set; }

        /// <inheritdoc/>
        public virtual string Url { get; set; }

        /// <inheritdoc/>
        public virtual MqttConfiguration Mqtt { get; set; }

        /// <inheritdoc/>
        public abstract string ApplicationFolder { get; }

        internal PackageManifest LoadManifest(string manifestFilename)
        {
            var manifest = loadConfigurationFile(manifestFilename);
            if (manifest != null)
            {
                try
                {
                    byte[] result;
                    using (FileStream SourceStream = System.IO.File.Open(manifest.FullName, FileMode.Open))
                    {
                        result = new byte[SourceStream.Length];
                        SourceStream.ReadAsync(result, 0, (int)SourceStream.Length).Wait();
                    }
                    return Json.DeserializeAutoTypeNameHandling<PackageManifest>(System.Text.Encoding.UTF8.GetString(result));
                }
                catch (Exception e)
                {
                    throw new Exception($"Error loading manifest: {e.Message}");
                }
            }
            return null;
        }

        private FileInfo loadConfigurationFile(string filename)
        {
            var fullPath = Path.Combine(ApplicationFolder, filename);
            FileInfo f = new FileInfo(fullPath);

            if (!f.Exists)
                return null;

            return f;
        }
    }
}