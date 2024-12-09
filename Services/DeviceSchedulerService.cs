using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl;
using static Quartz.Logging.OperationName;
using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;
using System.Linq;
using RIoT2.Core.Interfaces;
using RIoT2.Core;

namespace RIoT2.Common.Services
{
    public class DeviceSchedulerService : IHostedService
    {
        private readonly ILogger<DeviceSchedulerService> _logger;
        private IScheduler _scheduler;
        private IDeviceService _deviceService;
        private readonly INodeConfigurationService _configuration;
        private CancellationToken _cancellationToken;

        public static event SchedulerEventHandler SchedulerEvent;

        public static void TriggerSchedulerEvent(string group, string name) 
        {
            SchedulerEvent(group, name);
        }

        public DeviceSchedulerService(ILogger<DeviceSchedulerService> logger, IDeviceService deviceService, INodeConfigurationService configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _deviceService = deviceService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _deviceService.DevicesUpdated += _deviceService_DevicesUpdated;
            //_configuration.DeviceConfigurationUpdated += _configuration_DeviceConfigurationUpdated;
            _logger.LogInformation("Scheduler initialized and waiting configuration.");
            await Task.CompletedTask;
        }

        private void _deviceService_DevicesUpdated(ServiceEvent serviceEvent)
        {
            if (serviceEvent == ServiceEvent.Started) 
            {
                configureDeviceTriggers().Wait();
            }
        }

        private async Task configureDeviceTriggers()
        {
            await stopScheduler(); //ensure that scheduler is stopped before continue

            List<SchedulerTrigger> allTriggers = new List<SchedulerTrigger>();
            foreach (var device in _deviceService.Devices)
            {
                if (!(device is IRefreshableReportDevice))
                    continue;

                var deviceTriggers = device.Configuration?.GetRefreshSchedules();
                if (deviceTriggers != null && deviceTriggers.Count() > 0)
                {
                    allTriggers.AddRange(deviceTriggers);
                    SchedulerEvent += (device as IRefreshableReportDevice).RefreshReport;
                }
            }

            if (allTriggers.Count > 0)
            {
                await configure(allTriggers);

                _logger.LogInformation("starting Scheduler.");
                await _scheduler.Start(_cancellationToken);
            }
            else
            {
                _logger.LogWarning("Scheduler not started! No triggers defined.");
            }
        }

        public async Task StopAsync(CancellationToken stoppingToken)
        {
            _deviceService.DevicesUpdated -= _deviceService_DevicesUpdated;
            //_configuration.DeviceConfigurationUpdated -= _configuration_DeviceConfigurationUpdated;
            await stopScheduler(stoppingToken);
        }

        private async Task stopScheduler(CancellationToken cancellationToken = default) 
        {
            if (_scheduler != null && (_scheduler.IsStarted || _scheduler.InStandbyMode))
                await _scheduler.Shutdown(true, cancellationToken);
        }

        private async Task configure(List<SchedulerTrigger> triggers) 
        {
            if (triggers == null || triggers.Count == 0)
                return;

        
            StdSchedulerFactory factory = new StdSchedulerFactory();
            _scheduler = await factory.GetScheduler();

            try
            {
                foreach (var trg in triggers)
                {
                    ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity(trg.Name, trg.Group)
                    .WithCronSchedule(trg.CronSchedule)
                    .Build();

                    IJobDetail raiseEventJob = JobBuilder.Create<RaiseEventJob>()
                   .Build();

                    await _scheduler.ScheduleJob(raiseEventJob, trigger);
                }
            }
            catch (Exception x) 
            {
                _logger.LogError(x, $"Could not create scheduler trigger");
            }
        }
    }

    public class RaiseEventJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            DeviceSchedulerService.TriggerSchedulerEvent(context.Trigger.Key.Group, context.Trigger.Key.Name);
            await Task.CompletedTask;
        }
    }
}
