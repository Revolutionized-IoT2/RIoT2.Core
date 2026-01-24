using RIoT2.Core.Models;
using System.Collections.Generic;

namespace RIoT2.Core.Interfaces.Services
{
    public interface IOrchestratorConfigurationService
    {
        OrchestratorConfiguration OrchestratorConfiguration { get; }
        List<NodeDeviceConfiguration> NodeConfigurations { get; }
        DashboardConfiguration DashboardConfiguration { get; }
        string SaveDashboardConfiguration(DashboardConfiguration dashboard);
        string SaveDashboardConfiguration(string json);
        string FindNodeId(string id);
        IEnumerable<CommandTemplate> GetCommandTemplates();
        IEnumerable<ReportTemplate> GetReportTemplates();
        string SaveNodeConfiguration(NodeDeviceConfiguration configuration);
        string SaveNodeConfiguration(string json);
        void DeleteNodeConfiguration(string nodeId);
    }
}