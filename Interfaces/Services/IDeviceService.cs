﻿using System.Collections.Generic;

namespace RIoT2.Core.Interfaces.Services
{
    public interface IDeviceService
    {
        event DeviceServiceUpdatedHandler DevicesUpdated;
        List<IDevice> Devices { get; }
        ICommandDevice GetDeviceByCommandId(string commandId);
        IDevice GetDeviceByReportId(string reportId);
        void StartAllDevices();
        void StopAllDevices();
    }
}