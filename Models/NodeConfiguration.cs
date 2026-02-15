using RIoT2.Core.Abstracts;
using RIoT2.Core.Interfaces.Services;
using System.IO;
using System.Reflection;

namespace RIoT2.Core.Models
{
    public class NodeConfiguration : ConfigurationBase, IConfiguration
    {
        private readonly DirectoryInfo _configurationFolder;
        public NodeConfiguration() 
        {
            _configurationFolder = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
            PluginManifest = LoadManifest("Plugins/PluginManifest.json");
            Manifest = LoadManifest("Data/Manifest.json");
        }

        public PackageManifest PluginManifest { get; private set; }
        public PackageManifest Manifest { get; private set; }

        public override string ApplicationFolder { get => _configurationFolder.FullName; }

        public string GetTopic(MqttTopic topic) 
        {
            return Constants.Get(Id, topic);
        }
    }
}