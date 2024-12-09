using RIoT2.Core.Models;
using System.Threading.Tasks;

namespace RIoT2.Core.Interfaces.Services
{
    public interface IOrchestratorMqttService
    {
        Task Start();
        Task Stop();
        void Dispose();
        Task SendCommand(string topic, Command command);
        Task SendReport(Report report);
        Task SendConfigurationCommand(string id);
        Task ProcessOutput(RuleEvaluationResult output);
    }
}