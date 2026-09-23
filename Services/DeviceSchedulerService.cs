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

namespace RIoT2.Core.Services
{
    /// <summary>
    /// Hosted service that schedules periodic report refreshes for refreshable devices using Quartz
    /// cron triggers.
    /// </summary>
    public class DeviceSchedulerService : IHostedService
    {
        private readonly ILogger<DeviceSchedulerService> _logger;
        private IScheduler _scheduler;
        private IDeviceService _deviceService;
        private readonly INodeConfigurationService _configuration;
        private CancellationToken _cancellationToken;
        private readonly SemaphoreSlim _lifecycle = new SemaphoreSlim(1, 1);
        private readonly List<IRefreshableReportDevice> _subscriptions = new List<IRefreshableReportDevice>();
        private readonly Dictionary<string, Func<CancellationToken, Task>> _refreshActions = new Dictionary<string, Func<CancellationToken, Task>>();
        private readonly string _schedulerName = "RIoT2-" + Guid.NewGuid().ToString("N");
        private bool _active;

        /// <summary>
        /// Occurs when a scheduled trigger fires, identifying the trigger by group and name.
        /// </summary>
        public static event SchedulerEventHandler SchedulerEvent;

        /// <summary>
        /// Raises the <see cref="SchedulerEvent"/> for the specified trigger group and name.
        /// </summary>
        /// <param name="group">The group of the trigger that fired.</param>
        /// <param name="name">The name of the trigger that fired.</param>
        public static void TriggerSchedulerEvent(string group, string name) 
        {
            SchedulerEvent?.Invoke(group, name);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceSchedulerService"/> class.
        /// </summary>
        /// <param name="logger">The logger used to record scheduler activity.</param>
        /// <param name="deviceService">The device service providing the devices to schedule.</param>
        /// <param name="configuration">The node configuration service.</param>
        public DeviceSchedulerService(ILogger<DeviceSchedulerService> logger, IDeviceService deviceService, INodeConfigurationService configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _deviceService = deviceService;
        }

        /// <summary>
        /// Starts the hosted service and begins listening for device updates in order to configure triggers.
        /// </summary>
        /// <param name="cancellationToken">A token that signals the start operation should be aborted.</param>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _lifecycle.WaitAsync(cancellationToken);
            try
            {
                if (_active)
                    return;
                _active = true;
                _cancellationToken = cancellationToken;
                _deviceService.DevicesUpdated += _deviceService_DevicesUpdated;
                _logger.LogInformation("Scheduler initialized and waiting configuration.");
            }
            finally { _lifecycle.Release(); }
        }

        private void _deviceService_DevicesUpdated(ServiceEvent serviceEvent)
        {
            _lifecycle.Wait();
            try
            {
                if (!_active)
                    return;
                if (serviceEvent == ServiceEvent.Started)
                    configureDeviceTriggers().GetAwaiter().GetResult();
                else if (serviceEvent == ServiceEvent.Stopped)
                    stopScheduler().GetAwaiter().GetResult();
            }
            finally { _lifecycle.Release(); }
        }

        private async Task configureDeviceTriggers()
        {
            await stopScheduler(); //ensure that scheduler is stopped before continue

            List<SchedulerTrigger> allTriggers = new List<SchedulerTrigger>();
            foreach (var device in _deviceService.Devices)
            {
                if (!(device is IRefreshableReportDevice))
                    continue;

                var deviceTrigger = device.Configuration?.GetDeviceRefreshSchedule();
                if (deviceTrigger != null)
                {
                    _logger.LogInformation($"Adding scheduler trigger for device {device.Configuration.Name} with schedule {deviceTrigger.CronSchedule}");
                    allTriggers.Add(deviceTrigger);
                    if (_deviceService is IAsyncDeviceService asynchronous)
                        _refreshActions[deviceTrigger.Group] = token =>
                            asynchronous.RefreshReportAsync(device, deviceTrigger.Group, deviceTrigger.Name, token);
                    else
                    {
                        SchedulerEvent += ((IRefreshableReportDevice)device).RefreshReport;
                        _subscriptions.Add((IRefreshableReportDevice)device);
                    }
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

        /// <summary>
        /// Stops the hosted service and shuts down the scheduler.
        /// </summary>
        /// <param name="stoppingToken">A token that signals the stop operation should be aborted.</param>
        public async Task StopAsync(CancellationToken stoppingToken)
        {
            _deviceService.DevicesUpdated -= _deviceService_DevicesUpdated;
            await _lifecycle.WaitAsync(stoppingToken);
            try
            {
                _active = false;
                await stopScheduler(stoppingToken);
            }
            finally { _lifecycle.Release(); }
        }

        private async Task stopScheduler(CancellationToken cancellationToken = default) 
        {
            foreach (var device in _subscriptions)
                SchedulerEvent -= device.RefreshReport;
            _subscriptions.Clear();
            if (_scheduler != null && !_scheduler.IsShutdown)
                await _scheduler.Shutdown(true, cancellationToken);
            _scheduler = null;
            _refreshActions.Clear();
        }

        private async Task configure(List<SchedulerTrigger> triggers) 
        {
            if (triggers == null || triggers.Count == 0)
                return;

        
            StdSchedulerFactory factory = new StdSchedulerFactory(new System.Collections.Specialized.NameValueCollection
            {
                ["quartz.scheduler.instanceName"] = _schedulerName
            });
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
                    if (_refreshActions.TryGetValue(trg.Group, out var refresh))
                        raiseEventJob.JobDataMap["refresh"] = refresh;

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
            if (context.MergedJobDataMap.ContainsKey("refresh") &&
                context.MergedJobDataMap["refresh"] is Func<CancellationToken, Task> refresh)
                await refresh(context.CancellationToken).ConfigureAwait(false);
            else
                DeviceSchedulerService.TriggerSchedulerEvent(context.Trigger.Key.Group, context.Trigger.Key.Name);
        }
    }
}
