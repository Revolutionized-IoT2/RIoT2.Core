using RIoT2.Core.Models;
using System.Threading.Tasks;

namespace RIoT2.Core.Interfaces.Services
{
    /// <summary>
    /// Provides MQTT connectivity and messaging for a node.
    /// </summary>
    public interface INodeMqttService
    {
        /// <summary>
        /// Connects the node to the MQTT broker and begins processing messages.
        /// </summary>
        Task Start();

        /// <summary>
        /// Disconnects the node from the MQTT broker.
        /// </summary>
        Task Stop();

        /// <summary>
        /// Publishes a command value to the specified topic.
        /// </summary>
        /// <param name="topic">The topic to publish to.</param>
        /// <param name="value">The command value to publish.</param>
        Task SendCommand(string topic, string value);

        /// <summary>
        /// Publishes an online (presence) message for the node.
        /// </summary>
        /// <param name="msg">The online message to publish.</param>
        Task SendNodeOnlineMessage(NodeOnlineMessage msg);

        /// <summary>
        /// Gets a value indicating whether the node is currently connected to the broker.
        /// </summary>
        /// <returns><c>true</c> if connected; otherwise, <c>false</c>.</returns>
        bool IsConnected();
    }
}