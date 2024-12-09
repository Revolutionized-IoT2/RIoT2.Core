using System.Threading.Tasks;

namespace RIoT2.Core.Interfaces.Services
{
    public interface IAzureRelayService
    {
        event WebMessageHandler MessageReceived;
        Task StartAsync();
        Task StopAsync();

        void Configure(string relayNamespace, string connectionName, string keyName, string key);
    }
}
