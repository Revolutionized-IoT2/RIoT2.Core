using RIoT2.Core;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Interfaces.Services;

namespace RIoT2.Common.Services
{
    public class ReportService : IReportService
    {
        public event ReportUpdatedHandler ReportUpdated;

        IDeviceService _deviceService;
        public ReportService(IDeviceService deviceService)
        {
            _deviceService = deviceService;
            subscribeToDevicesReportEvent();
        }

        private void subscribeToDevicesReportEvent() 
        {
            foreach (var device in _deviceService.Devices) 
                device.ReportUpdated += Device_ReportUpdated;
        }

        private void Device_ReportUpdated(IDevice sender, IReport report)
        {
            ReportUpdated?.Invoke(sender, report);
        }
    }
}