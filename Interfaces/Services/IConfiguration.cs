using RIoT2.Core.Models;

namespace RIoT2.Core.Interfaces.Services
{
    public interface IConfiguration
    {
        string Id { get; set; }
        string Url { get; set; }
        MqttConfiguration Mqtt { get; set; }
        string ApplicationFolder { get; }
    }
}