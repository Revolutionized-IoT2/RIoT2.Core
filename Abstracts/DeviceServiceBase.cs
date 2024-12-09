using RIoT2.Core.Interfaces;
using RIoT2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RIoT2.Core.Interfaces.Services;

namespace RIoT2.Core.Abstracts
{
    public abstract class DeviceServiceBase : IDeviceService
    {
        private readonly ILogger _logger;
        private readonly INodeConfigurationService _configurationService;
        private bool _isConfigured;
        private bool _devicesStarted;

        public event DeviceServiceUpdatedHandler DevicesUpdated;

        public DeviceServiceBase(INodeConfigurationService configurationService, ILogger logger, List<IDevice> devices) 
        {
            _devicesStarted = false;
            _isConfigured = false;
            _logger = logger;
            _configurationService = configurationService;
            Devices = devices;
            #if (NETCOREAPP)
                             //TODO if net core app, load device plugin dll's
            #endif
        }

        public virtual List<IDevice> Devices { get; private set; }

        public virtual ICommandDevice GetDeviceByCommandId(string commandId)
        {
            return Devices.FirstOrDefault(x => (x is ICommandDevice) && (x as ICommandDevice).CommandTemplates.Any(c => c.Id == commandId)) as ICommandDevice;
        }

        public virtual IDevice GetDeviceByReportId(string reportId)
        {
            return Devices.FirstOrDefault(x => x.ReportTemplates.Any(c => c.Id == reportId));
        }

        public virtual void StartAllDevices()
        {
            if (!_isConfigured)
                ConfigureDevices();

            if (_devicesStarted) 
                StopAllDevices();

            foreach (var device in Devices.Where(x => x.IsInitialized)) 
            {
                try 
                {
                    device.Start();
                }
                catch (Exception x)
                {
                    _logger.LogError(x, $"Error while starting device: {device.Name}");
                }
            }

            _devicesStarted = true;
            DevicesUpdated?.Invoke(ServiceEvent.Started);
        }

        public virtual void StopAllDevices()
        {
            foreach (var device in Devices)
                device.Stop();

            _devicesStarted = false;
            DevicesUpdated?.Invoke(ServiceEvent.Stopped);
        }

        public void ConfigureDevices()
        {
            foreach (var device in Devices)
            {
                var configuration = _configurationService.DeviceConfiguration.DeviceConfigurations.FirstOrDefault(x => x.ClassFullName == device.GetType().FullName);
                if (configuration != null)
                    device.Initialize(configuration);
            }

            _isConfigured = true;
        }
    }
}
