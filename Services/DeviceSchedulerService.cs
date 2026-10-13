using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl;
using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;
using RIoT2.Core.Interfaces;

namespace RIoT2.Core.Services
{
    /// <summary>
    /// Hosted service that schedules periodic report refreshes for refreshable devices using Quartz
    /// cron triggers.
    /// </summary>
    /// <remarks>
    /// <see cref="IDeviceService.DevicesUpdated"/> only queues the update; a background loop rebuilds the
    /// triggers. The device service raises the event while it holds its lifecycle lock, and stopping
    /// Quartz waits for running refresh jobs, which may wait for that lock, so reconfiguring inside the
    /// event could deadlock (plan M10 step 7).
    /// </remarks>
    public class DeviceSchedulerService : IHostedService
    {
        private readonly ILogger<DeviceSchedulerService> _logger;
        private IScheduler _scheduler;
        private IDeviceService _deviceService;
        private readonly INodeConfigurationService _configuration;
        private readonly SemaphoreSlim _lifecycle = new SemaphoreSlim(1, 1);
        private readonly List<IRefreshableReportDevice> _subscriptions = new List<IRefreshableReportDevice>();
        private readonly Dictionary<string, Func<CancellationToken, Task>> _refreshActions = new Dictionary<string, Func<CancellationToken, Task>>();
        private readonly string _schedulerName = "RIoT2-" + Guid.NewGuid().ToString("N");
        private Channel<Update> _updates;
        private Task _worker = Task.CompletedTask;
        private CancellationTokenSource _stopping;
        private bool _active;

        /// <summary>
        /// Occurs when a scheduled trigger of this scheduler fires, identifying the trigger by group and name.
        /// </summary>
        public event SchedulerEventHandler RefreshTriggered;

        /// <summary>
        /// Occurs when a trigger of any scheduler fires. Kept for one release; use <see cref="RefreshTriggered"/>.
        /// </summary>
        [Obsolete("Use the RefreshTriggered event of the scheduler instance. The static event is removed in increment I45.")]
        public static event SchedulerEventHandler SchedulerEvent;

        /// <summary>
        /// Raises the static <see cref="SchedulerEvent"/>. Kept for one release; use <see cref="TriggerRefresh"/>.
        /// </summary>
        [Obsolete("Use TriggerRefresh on the scheduler instance. The static method is removed in increment I45.")]
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
        /// Raises <see cref="RefreshTriggered"/> (and, for one release, the static <see cref="SchedulerEvent"/>)
        /// for the specified trigger group and name, as a firing trigger does.
        /// </summary>
        public void TriggerRefresh(string group, string name)
        {
            RefreshTriggered?.Invoke(group, name);
#pragma warning disable CS0618 // The static event is forwarded until it is removed.
            SchedulerEvent?.Invoke(group, name);
#pragma warning restore CS0618
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
                _stopping?.Dispose();
                _stopping = new CancellationTokenSource();
                var updates = Channel.CreateUnbounded<Update>(new UnboundedChannelOptions { SingleReader = true });
                _updates = updates;
                var token = _stopping.Token;
                _worker = Task.Run(() => ProcessUpdatesAsync(updates.Reader, token));
                _deviceService.DevicesUpdated += _deviceService_DevicesUpdated;
                _logger.LogInformation("Scheduler initialized and waiting configuration.");
            }
            finally { _lifecycle.Release(); }
        }

        /// <summary>
        /// Completes when every device update queued before the call has been applied to the triggers.
        /// </summary>
        public Task WhenIdleAsync()
        {
            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var updates = _updates;
            if (updates == null || !updates.Writer.TryWrite(new Update(null, done)))
                done.TrySetResult(true);
            return done.Task;
        }

        private void _deviceService_DevicesUpdated(ServiceEvent serviceEvent) =>
            _updates?.Writer.TryWrite(new Update(serviceEvent, null));

        private async Task ProcessUpdatesAsync(ChannelReader<Update> reader, CancellationToken cancellationToken)
        {
            try
            {
                while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var update))
                    {
                        if (update.Event.HasValue)
                            await ApplyAsync(update.Event.Value, cancellationToken).ConfigureAwait(false);
                        update.Done?.TrySetResult(true);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            finally
            {
                // Release anyone waiting for updates that will no longer be applied.
                while (reader.TryRead(out var update))
                    update.Done?.TrySetResult(true);
            }
        }

        private async Task ApplyAsync(ServiceEvent serviceEvent, CancellationToken cancellationToken)
        {
            await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_active)
                    return;
                if (serviceEvent == ServiceEvent.Started)
                    await configureDeviceTriggers(cancellationToken).ConfigureAwait(false);
                else if (serviceEvent == ServiceEvent.Stopped)
                    await stopScheduler(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception x)
            {
                _logger.LogError(x, "Could not update the device refresh schedule after {Event}", serviceEvent);
            }
            finally { _lifecycle.Release(); }
        }

        private async Task configureDeviceTriggers(CancellationToken cancellationToken)
        {
            await stopScheduler(cancellationToken); //ensure that scheduler is stopped before continue

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
                        RefreshTriggered += ((IRefreshableReportDevice)device).RefreshReport;
                        _subscriptions.Add((IRefreshableReportDevice)device);
                    }
                }
            }

            if (allTriggers.Count > 0)
            {
                await configure(allTriggers);

                _logger.LogInformation("starting Scheduler.");
                await _scheduler.Start(cancellationToken);
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
            Task worker;
            await _lifecycle.WaitAsync(stoppingToken);
            try
            {
                _active = false;
                _updates?.Writer.TryComplete();
                _stopping?.Cancel();
                worker = _worker;
                await stopScheduler(stoppingToken);
            }
            finally { _lifecycle.Release(); }
            await worker.ConfigureAwait(false);
        }

        private async Task stopScheduler(CancellationToken cancellationToken = default) 
        {
            foreach (var device in _subscriptions)
                RefreshTriggered -= device.RefreshReport;
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
                        raiseEventJob.JobDataMap[RaiseEventJob.RefreshKey] = refresh;
                    else
                        raiseEventJob.JobDataMap[RaiseEventJob.SchedulerKey] = this;

                    await _scheduler.ScheduleJob(raiseEventJob, trigger);
                }
            }
            catch (Exception x) 
            {
                _logger.LogError(x, $"Could not create scheduler trigger");
            }
        }

        private sealed class Update
        {
            public Update(ServiceEvent? serviceEvent, TaskCompletionSource<bool> done)
            {
                Event = serviceEvent;
                Done = done;
            }

            public ServiceEvent? Event { get; }
            public TaskCompletionSource<bool> Done { get; }
        }
    }

    public class RaiseEventJob : IJob
    {
        internal const string RefreshKey = "refresh";
        internal const string SchedulerKey = "scheduler";

        public async Task Execute(IJobExecutionContext context)
        {
            var data = context.MergedJobDataMap;
            if (data.ContainsKey(RefreshKey) && data[RefreshKey] is Func<CancellationToken, Task> refresh)
                await refresh(context.CancellationToken).ConfigureAwait(false);
            else if (data.ContainsKey(SchedulerKey) && data[SchedulerKey] is DeviceSchedulerService scheduler)
                scheduler.TriggerRefresh(context.Trigger.Key.Group, context.Trigger.Key.Name);
        }
    }
}
