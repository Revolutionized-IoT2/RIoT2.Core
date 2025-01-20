using RIoT2.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RIoT2.Core.Interfaces.Services;
using System;

namespace RIoT2.Core.Abstracts
{
    public abstract class DeviceServiceBase : IDeviceService
    {
        private readonly ILogger _logger;
        private readonly INodeConfigurationService _configurationService;

        public event DeviceServiceUpdatedHandler DevicesUpdated;

        public DeviceServiceBase(INodeConfigurationService configurationService, ILogger logger, List<IDevice> devices) 
        {
            _logger = logger;
            _configurationService = configurationService;
            Devices = devices;
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

        public virtual void StartAllDevices(bool restartDevicesInErrorState = false)
        {
            bool anyStarted = false;
            var devicesToStart = Devices.Where(x => x.State == DeviceState.Initialized || x.State == DeviceState.Stopped).ToList();
            
            if (restartDevicesInErrorState)
                devicesToStart.AddRange(Devices.Where(x => x.State == DeviceState.Error));

            foreach (var device in devicesToStart) 
            {
                try
                {
                    device.Start();
                    anyStarted = true;
                }
                catch (Exception x)
                {
                    _logger.LogError(x, $"Error in StartAllDevices");
                }
            }

            if(anyStarted)
                DevicesUpdated?.Invoke(ServiceEvent.Started);
        }

        public virtual void StopAllDevices()
        {
            bool anyStopped = false;
            foreach (var device in Devices) 
            {
                if (device.State == DeviceState.Running) 
                {
                    try
                    {
                        device.Stop();
                        anyStopped = true;
                    }
                    catch (Exception x) 
                    {
                        _logger.LogError(x, $"Error in StopAllDevices");
                    }
                }
            }
                
            if(anyStopped)
                DevicesUpdated?.Invoke(ServiceEvent.Stopped);
        }

        public void ConfigureDevices()
        {
            foreach (var device in Devices)
            {
                var configuration = _configurationService?.DeviceConfiguration?.DeviceConfigurations?.FirstOrDefault(x => x.ClassFullName == device.GetType().FullName);
                if (configuration != null) 
                {
                    try 
                    {
                        if (device.State != DeviceState.Running)
                            device.Initialize(configuration);
                    }
                    catch(Exception x)
                    {
                        _logger.LogError(x, $"Error in ConfigureDevices");
                    }
                }
            }
        }
    }
}
