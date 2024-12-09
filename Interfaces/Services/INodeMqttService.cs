using RIoT2.Core.Models;
using System.Threading.Tasks;

namespace RIoT2.Core.Interfaces.Services
{
    public interface INodeMqttService
    {
        Task Start();
        Task Stop();
        Task SendCommand(string topic, string value);
        Task SendNodeOnlineMessage(NodeOnlineMessage msg);
        bool IsConnected();
    }
}