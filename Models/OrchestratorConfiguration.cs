using RIoT2.Core.Abstracts;
using RIoT2.Core.Interfaces.Services;
using System.IO;
using System.Reflection;

namespace RIoT2.Core.Models
{
    public class OrchestratorConfiguration : ConfigurationBase, IConfiguration
    {
        private readonly DirectoryInfo _configurationFolder;
        public OrchestratorConfiguration()
        {
            _configurationFolder = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
            Manifest = LoadManifest("Data/Manifest.json");
        }
        public bool UseExtWorkflowEngine { get; set; } = false;
        public PackageManifest Manifest { get; set; }

        public override string ApplicationFolder { get => _configurationFolder.FullName; }
    }
}