using RIoT2.Core.Models;
using System.Collections.Generic;

namespace RIoT2.Core.Interfaces
{
    public interface IDevice
    {
        DeviceConfiguration Configuration { get; }
        DeviceState State { get; }
        string StateMessage { get; }
        string Id { get; }
        string Name { get; }
        void Start();
        void Stop();
        event ReportUpdatedHandler ReportUpdated;
        IEnumerable<ReportTemplate> ReportTemplates { get; }
        void Initialize(DeviceConfiguration configuration);
    }
}
