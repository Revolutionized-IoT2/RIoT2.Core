using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;
using RIoT2.Core.Utils;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace RIoT2.Core.Abstracts
{
    public abstract class NodeConfigurationServiceBase : INodeConfigurationService
    {

        public NodeConfigurationServiceBase() 
        {
            DeviceConfigurationLoaded = false;
        }

        public virtual event DeviceConfigurationUpdatedHandler DeviceConfigurationUpdated;
        public virtual bool DeviceConfigurationLoaded { get; private set; }

        public virtual NodeDeviceConfiguration DeviceConfiguration { get; private set; }

        public abstract NodeConfiguration Configuration { get; }

        public virtual NodeOnlineMessage OnlineMessage { get; set; }

        public virtual async Task LoadDeviceConfiguration(string json, string id)
        {
            DeviceConfigurationLoaded = false;
            var cmd = Json.Deserialize<ConfigurationCommand>(json);

            var response = await Web.GetAsync(cmd.ApiBaseUrl + Constants.ApiConfigurationUrl.Replace("{id}", id));
            if (response.IsSuccessStatusCode)
            {
                var deviceJson = await response.Content.ReadAsStringAsync();
                SetDeviceConfiguration(Json.Deserialize<NodeDeviceConfiguration>(deviceJson));
            }
        }

        public void SetDeviceConfiguration(NodeDeviceConfiguration configuration) 
        {
            DeviceConfiguration = configuration;
            DeviceConfigurationLoaded = true;
            DeviceConfigurationUpdated?.Invoke();
        }

        public virtual void DownloadPluginPackage(string url)
        {
            var file = Utils.Web.DownloadFile(url).Result;
            if (file != null)
            {
                saveFile("Data/" + file.Name, file.Content);
            }
        }

        public virtual void InstallPluginPackage()
        {
            try 
            {
                var packageName = getPluginPackage();
                if(string.IsNullOrEmpty(packageName))
                    return; 

                var file = loadFile("Data/" + packageName);
                if (file.Exists) 
                {
                    deleteFolderContent("Plugins");
                    ZipFile.ExtractToDirectory(file.FullName, Path.Combine(Configuration.ApplicationFolder, "Plugins"));
                    deleteFile("Data/" + packageName); //delete package after intallation
                }
            } 
            catch(Exception x) 
            {
                throw new Exception("Error while installing plugin package", x);
            }
        }

        private string getPluginPackage()
        {
            var dataFolder = Path.Combine(Configuration.ApplicationFolder, "Data");
            if (Directory.Exists(dataFolder))
            {
                DirectoryInfo di = new DirectoryInfo(dataFolder);
                var file = di.GetFiles("*.zip").FirstOrDefault();
                if (file != null)
                {
                    return file.Name; 
                }
            }
            return null;
        }

        private void deleteFolderContent(string directory)
        {
            var fullPath = Path.Combine(Configuration.ApplicationFolder, directory);
            if (Directory.Exists(fullPath))
            {
                DirectoryInfo di = new DirectoryInfo(fullPath);
                foreach (var f in di.GetFiles())
                {
                    f.Delete();
                }
            }
        }

        private void deleteFile(string filename)
        {
            var fullPath = Path.Combine(Configuration.ApplicationFolder, filename);
            if (File.Exists(fullPath))
            {
                FileInfo fi = new FileInfo(fullPath);
                fi.Delete();
            }
        }

        private void saveFile(string filename, byte[] content) 
        {
            var fullPath = Path.Combine(Configuration.ApplicationFolder, filename);
            FileInfo fi = new FileInfo(fullPath);
            if (fi.Exists)
                fi.Delete();
            
            File.WriteAllBytes(fullPath, content);
        }

        private FileInfo loadFile(string filename)
        {
            var fullPath = Path.Combine(Configuration.ApplicationFolder, filename);
            FileInfo f = new FileInfo(fullPath);

            if (!f.Exists)
                return null;

            return f;
        }
    }
}