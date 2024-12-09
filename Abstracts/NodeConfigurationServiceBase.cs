using RIoT2.Core.Models;
using RIoT2.Core.Utils;
using System.Threading.Tasks;

namespace RIoT2.Core.Abstracts
{
    public abstract class NodeConfigurationServiceBase
    {

        public NodeConfigurationServiceBase() 
        {
            DeviceConfigurationLoaded = false;
        }

        public virtual event DeviceConfigurationUpdatedHandler DeviceConfigurationUpdated;
        public virtual bool DeviceConfigurationLoaded { get; private set; }

        public virtual NodeDeviceConfiguration DeviceConfiguration { get; private set; }

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

        public NodeOnlineMessage OnlineMessage { get; set; }
    }
}
