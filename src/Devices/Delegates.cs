using RIoT2.Core.Interfaces;

namespace RIoT2.Core
{
    public delegate void DeviceConfigurationUpdatedHandler();
    public delegate void ReportUpdatedHandler(IDevice sender, IReport report);
}
