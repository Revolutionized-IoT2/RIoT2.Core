using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;
using RIoT2.Core.Services;
using RIoT2.Core.Utils;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace RIoT2.Core.Abstracts
{
    /// <summary>
    /// Provides a base implementation of <see cref="INodeConfigurationService"/> that handles
    /// loading device configuration and downloading and installing plugin packages.
    /// </summary>
    public abstract class NodeConfigurationServiceBase : INodeConfigurationService, INodeConfigurationStatusSource
    {
        private readonly object _pendingGate = new object();
        private string _pendingHash;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeConfigurationServiceBase"/> class with no device configuration loaded.
        /// </summary>
        public NodeConfigurationServiceBase() 
        {
            DeviceConfigurationLoaded = false;
        }

        /// <inheritdoc/>
        public virtual event DeviceConfigurationUpdatedHandler DeviceConfigurationUpdated;

        /// <inheritdoc/>
        public virtual bool DeviceConfigurationLoaded { get; private set; }

        /// <inheritdoc/>
        public virtual NodeDeviceConfiguration DeviceConfiguration { get; private set; }

        /// <inheritdoc/>
        public abstract NodeConfiguration Configuration { get; }

        /// <inheritdoc/>
        public virtual NodeOnlineMessage OnlineMessage { get; set; }

        /// <summary>The node's view of its configuration, published on its status topic (design 7.2).</summary>
        public NodeConfigurationStatus ConfigurationStatus { get; } = new NodeConfigurationStatus();

        /// <summary>How long fetching the configuration may take.</summary>
        protected virtual int ConfigurationFetchTimeoutMilliseconds => 30000;

        /// <summary>
        /// Where the last applied configuration is kept, so the node can start with it when the
        /// orchestrator is down (design 7.2, decision 4).
        /// </summary>
        protected virtual string AppliedConfigurationPath => getSafeApplicationPath(Path.Combine("Data", "configuration.applied.json"));

        /// <summary>
        /// Handles a <see cref="ConfigurationCommand"/> (design 7.2). A command whose <c>hash</c> is
        /// already applied, with every device running, or is being applied, needs no work: the status is
        /// published again and nothing is fetched. Otherwise the configuration is fetched, with
        /// <c>If-None-Match</c> for the configuration it holds, and handed to the devices. When fetching or reading
        /// it fails the node keeps its current configuration, reports <c>failed</c>, and this method throws.
        /// </summary>
        public virtual async Task LoadDeviceConfiguration(string json, string id)
        {
            var command = Json.Deserialize<ConfigurationCommand>(json);
            if (string.IsNullOrEmpty(command?.ApiBaseUrl))
                throw Fail("The configuration command has no apiBaseUrl.");

            var current = DeviceConfiguration;
            if (!string.IsNullOrEmpty(command.Hash) && current != null)
            {
                if (ConfigurationStatus.IsApplied(command.Hash))
                {
                    ConfigurationStatus.Republish();
                    return;
                }
                if (IsBeingApplied(command.Hash))
                    return;
            }

            var url = command.ApiBaseUrl + Constants.ApiConfigurationUrl.Replace("{id}", id);
            // The configuration the node holds, which a 304 confirms. While another one is being
            // applied, or after one failed, it differs from the applied hash.
            var held = current?.Hash;
            HttpResponseMessage response;
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (!string.IsNullOrEmpty(held))
                    request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"" + held + "\""));
                try
                {
                    response = await Web.SendAsync(request, ConfigurationFetchTimeoutMilliseconds).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    throw Fail($"Could not fetch the configuration from {url}: {error.GetBaseException().Message}", error);
                }
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.NotModified && current != null)
                {
                    // Unchanged: report again, or apply it again (devices not running, or not applied yet).
                    if (ConfigurationStatus.IsApplied(held))
                        ConfigurationStatus.Republish();
                    else
                        SetDeviceConfiguration(current);
                    return;
                }
                if (!response.IsSuccessStatusCode)
                    throw Fail($"Could not fetch the configuration from {url}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

                NodeDeviceConfiguration configuration;
                try
                {
                    configuration = Json.Deserialize<NodeDeviceConfiguration>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                }
                catch (Exception error)
                {
                    throw Fail($"The configuration from {url} is not valid: {error.GetBaseException().Message}", error);
                }
                if (configuration == null)
                    throw Fail($"The configuration from {url} is empty.");
                SetDeviceConfiguration(configuration);
            }
        }

        private Exception Fail(string message, Exception inner = null)
        {
            ConfigurationStatus.Failed(message);
            return new InvalidOperationException(message, inner);
        }

        private bool IsBeingApplied(string hash)
        {
            lock (_pendingGate)
                return _pendingHash == hash && ConfigurationStatus.Current.State == NodeConfigurationState.Applying;
        }

        /// <summary>
        /// Sets the current device configuration, marks it as loaded, reports <c>applying</c>, and raises
        /// <see cref="DeviceConfigurationUpdated"/>. The host reports the outcome on <see cref="ConfigurationStatus"/>.
        /// </summary>
        /// <param name="configuration">The device configuration to apply.</param>
        public void SetDeviceConfiguration(NodeDeviceConfiguration configuration) 
        {
            lock (_pendingGate)
            {
                _pendingHash = configuration?.Hash;
                ConfigurationStatus.Applying();
            }
            DeviceConfiguration = configuration;
            DeviceConfigurationLoaded = true;
            DeviceConfigurationUpdated?.Invoke();
        }

        /// <summary>
        /// Starts with the configuration saved by <see cref="SaveAppliedConfiguration"/>, if there is one:
        /// it becomes the current configuration (see <see cref="SetDeviceConfiguration"/>) and is returned.
        /// Returns <c>null</c> when nothing was saved, and throws when the saved file can't be read.
        /// </summary>
        public NodeDeviceConfiguration LoadAppliedConfiguration()
        {
            var path = AppliedConfigurationPath;
            if (!File.Exists(path))
                return null;
            var configuration = Json.Deserialize<NodeDeviceConfiguration>(File.ReadAllText(path));
            if (configuration == null)
                throw new InvalidDataException($"The saved configuration {path} is empty.");
            SetDeviceConfiguration(configuration);
            return configuration;
        }

        /// <summary>Saves <paramref name="configuration"/> for <see cref="LoadAppliedConfiguration"/>, replacing the previous file atomically.</summary>
        public void SaveAppliedConfiguration(NodeDeviceConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            var path = AppliedConfigurationPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, Json.Serialize(configuration));
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }

        /// <inheritdoc/>
        [Obsolete("Blocks the calling thread; use DownloadPluginPackageAsync.")]
        public virtual void DownloadPluginPackage(string url)
        {
            DownloadPluginPackageAsync(url).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public virtual async Task DownloadPluginPackageAsync(string url)
        {
            var file = await Utils.Web.DownloadFile(url);
            if (file != null)
            {
                saveFile(Path.Combine("Data", Path.GetFileName(file.Name)), file.Content);
            }
        }

        /// <inheritdoc/>
        public virtual void InstallPluginPackage()
        {
            try 
            {
                var packageName = getPluginPackage();
                if(string.IsNullOrEmpty(packageName))
                    return; 

                var file = loadFile("Data/" + packageName);
                if (file != null && file.Exists) 
                {
                    deleteFolderContent("Plugins");
                    extractZipFile(file, Path.Combine(Configuration.ApplicationFolder, "Plugins"));
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
            var fullPath = getSafeApplicationPath(directory);
            if (Directory.Exists(fullPath))
            {
                DirectoryInfo di = new DirectoryInfo(fullPath);
                foreach (var f in di.GetFiles())
                    f.Delete();
                foreach (var d in di.GetDirectories())
                    d.Delete(true);
            }
        }

        private void deleteFile(string filename)
        {
            var fullPath = getSafeApplicationPath(filename);
            if (File.Exists(fullPath))
            {
                FileInfo fi = new FileInfo(fullPath);
                fi.Delete();
            }
        }

        private void saveFile(string filename, byte[] content) 
        {
            var fullPath = getSafeApplicationPath(filename);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            FileInfo fi = new FileInfo(fullPath);
            if (fi.Exists)
                fi.Delete();
            
            File.WriteAllBytes(fullPath, content);
        }

        private FileInfo loadFile(string filename)
        {
            var fullPath = getSafeApplicationPath(filename);
            FileInfo f = new FileInfo(fullPath);

            if (!f.Exists)
                return null;

            return f;
        }

        private void extractZipFile(FileInfo file, string destination)
        {
            Directory.CreateDirectory(destination);
            var destinationRoot = Path.GetFullPath(destination);
            if (!destinationRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                destinationRoot += Path.DirectorySeparatorChar;

            using (var archive = ZipFile.OpenRead(file.FullName))
            {
                foreach (var entry in archive.Entries)
                {
                    var fullPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
                    if (!fullPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Plugin package contains an entry outside the plugin folder.");

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(fullPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    entry.ExtractToFile(fullPath, true);
                }
            }
        }

        private string getSafeApplicationPath(string relativePath)
        {
            var root = Path.GetFullPath(Configuration.ApplicationFolder);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root += Path.DirectorySeparatorChar;

            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path must remain inside the application folder.");

            return fullPath;
        }
    }
}