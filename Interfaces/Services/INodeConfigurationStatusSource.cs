using RIoT2.Core.Services;

namespace RIoT2.Core.Interfaces.Services
{
    /// <summary>
    /// Implemented by a node configuration service that applies desired-state configuration (design
    /// 7.2). <see cref="NodeMqttService"/> then advertises <c>desired-state/1</c> and publishes
    /// <see cref="ConfigurationStatus"/> on the node's status topic. <see cref="Abstracts.NodeConfigurationServiceBase"/>
    /// implements it.
    /// </summary>
    public interface INodeConfigurationStatusSource
    {
        NodeConfigurationStatus ConfigurationStatus { get; }
    }
}
