using RIoT2.Core.Interfaces;
using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;
using RIoT2.Core.Services;
using RIoT2.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RIoT2.Core.Abstracts
{
    public abstract class DeviceServiceBase : IAsyncDeviceService, IDisposable, IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly INodeConfigurationService _configurationService;
        private readonly SemaphoreSlim _lifecycle = new SemaphoreSlim(1, 1);
        private readonly Dictionary<IDevice, DeviceOperationAdapter> _runtimes = new Dictionary<IDevice, DeviceOperationAdapter>();
        // Serialized configuration each device was last initialized with; the key of the per-device diff.
        private readonly Dictionary<IDevice, string> _applied = new Dictionary<IDevice, string>();
        private int _disposed;

        public event DeviceServiceUpdatedHandler DevicesUpdated;

        public DeviceServiceBase(INodeConfigurationService configurationService, ILogger logger, List<IDevice> devices)
        {
            _logger = logger;
            _configurationService = configurationService;
            Devices = devices;
        }

        public virtual List<IDevice> Devices { get; private set; }

        public virtual ICommandDevice GetDeviceByCommandId(string commandId) =>
            Devices.OfType<ICommandDevice>().FirstOrDefault(d => d.CommandTemplates?.Any(c => c.Id == commandId) == true);

        public virtual IDevice GetDeviceByReportId(string reportId) =>
            Devices.FirstOrDefault(d => d.ReportTemplates?.Any(r => r.Id == reportId) == true);

        private DeviceOperationAdapter Runtime(IDevice device)
        {
            lock (_runtimes)
            {
                if (!_runtimes.TryGetValue(device, out var runtime))
                    _runtimes.Add(device, runtime = new DeviceOperationAdapter(device));
                return runtime;
            }
        }

        public bool IsActive(IDevice device)
        {
            lock (_runtimes)
                return _runtimes.TryGetValue(device, out var runtime) && runtime.IsActive;
        }

        public virtual void StartAllDevices(bool restartDevicesInErrorState = false) =>
            StartAllDevicesAsync(restartDevicesInErrorState, CancellationToken.None).GetAwaiter().GetResult();

        public virtual void StopAllDevices() => StopAllDevicesAsync(CancellationToken.None).GetAwaiter().GetResult();

        public void ConfigureDevices()
        {
            _lifecycle.Wait();
            try
            {
                foreach (var device in Devices.Where(d => d.State != DeviceState.Running))
                {
                    var configuration = _configurationService?.DeviceConfiguration?.DeviceConfigurations?
                        .FirstOrDefault(c => c.ClassFullName == device.GetType().FullName);
                    if (configuration == null)
                        continue;
                    var key = Json.Serialize(configuration);
                    if (InitializeAsync(device, configuration, CancellationToken.None).GetAwaiter().GetResult())
                        SetApplied(device, key);
                }
            }
            finally { _lifecycle.Release(); }
        }

        /// <summary>
        /// Stops every device, then initializes and starts each device that has an entry in
        /// <paramref name="configuration"/>, whether or not its configuration changed.
        /// Prefer <see cref="ReconfigureChangedDevicesAsync"/>, which leaves unchanged devices running.
        /// </summary>
        public async Task ReconfigureDevicesAsync(IEnumerable<DeviceConfiguration> configuration, CancellationToken cancellationToken)
        {
            var desired = Desired(configuration);
            CancelOperations();
            await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                lock (_applied) _applied.Clear();
                await StopCoreAsync(cancellationToken).ConfigureAwait(false);
                var configured = new List<IDevice>();
                foreach (var device in Devices)
                {
                    if (desired.TryGetValue(device, out var entry) &&
                        await InitializeAsync(device, entry.Configuration, cancellationToken).ConfigureAwait(false))
                    {
                        SetApplied(device, entry.Key);
                        configured.Add(device);
                    }
                }
                await StartCoreAsync(configured, cancellationToken).ConfigureAwait(false);
            }
            finally { _lifecycle.Release(); }
        }

        /// <summary>
        /// Applies <paramref name="configuration"/> and restarts only the devices that need it: a device
        /// whose configuration was added, changed or removed, or a configured device that is not running
        /// (for example after a start failure). A device is matched to its entry by
        /// <see cref="DeviceConfiguration.ClassFullName"/>, and two entries are equal when they serialize
        /// to the same JSON. An identical configuration therefore starts and stops nothing.
        /// </summary>
        public async Task<DeviceReconfigurationResult> ReconfigureChangedDevicesAsync(IEnumerable<DeviceConfiguration> configuration, CancellationToken cancellationToken)
        {
            var desired = Desired(configuration);
            // Free devices that are about to be replaced from stalled work before waiting for the lifecycle lock.
            foreach (var device in Changed(desired))
                Runtime(device).Cancel();
            await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var changed = Changed(desired);
                var result = new DeviceReconfigurationResult { Unchanged = Devices.Count - changed.Count };
                if (changed.Count == 0)
                    return result;

                var stopped = new List<IDevice>();
                foreach (var device in changed)
                {
                    try
                    {
                        await Runtime(device).StopAsync(cancellationToken).ConfigureAwait(false);
                        stopped.Add(device);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception error)
                    {
                        _logger.LogError(error, "Error stopping device {DeviceId}; its configuration was not replaced", device.Id);
                        result.Failed++;
                    }
                    finally { RemoveApplied(device); }
                }
                DevicesUpdated?.Invoke(ServiceEvent.Stopped);

                foreach (var device in stopped)
                {
                    if (!desired.TryGetValue(device, out var entry))
                    {
                        result.Removed++;
                        continue;
                    }
                    if (!await InitializeAsync(device, entry.Configuration, cancellationToken).ConfigureAwait(false))
                    {
                        result.Failed++;
                        continue;
                    }
                    SetApplied(device, entry.Key);
                    try
                    {
                        await Runtime(device).StartAsync(cancellationToken).ConfigureAwait(false);
                        result.Started++;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception error)
                    {
                        _logger.LogError(error, "Error starting device {DeviceId}", device.Id);
                        result.Failed++;
                    }
                }

                // The scheduler rebuilds its triggers from all devices, so restore it while any device runs.
                if (Devices.Any(IsActive))
                    DevicesUpdated?.Invoke(ServiceEvent.Started);
                return result;
            }
            finally { _lifecycle.Release(); }
        }

        private Dictionary<IDevice, DesiredDevice> Desired(IEnumerable<DeviceConfiguration> configuration)
        {
            var snapshot = configuration?.Where(c => c != null).ToList() ?? new List<DeviceConfiguration>();
            var desired = new Dictionary<IDevice, DesiredDevice>();
            foreach (var device in Devices)
            {
                var entry = snapshot.FirstOrDefault(c => c.ClassFullName == device.GetType().FullName);
                if (entry != null)
                    desired[device] = new DesiredDevice(entry, Json.Serialize(entry));
            }
            return desired;
        }

        private List<IDevice> Changed(Dictionary<IDevice, DesiredDevice> desired)
        {
            var changed = new List<IDevice>();
            foreach (var device in Devices)
            {
                string applied;
                lock (_applied) _applied.TryGetValue(device, out applied);
                if (desired.TryGetValue(device, out var entry)
                    ? entry.Key != applied || !IsActive(device)
                    : applied != null)
                    changed.Add(device);
            }
            return changed;
        }

        private void SetApplied(IDevice device, string key)
        {
            lock (_applied) _applied[device] = key;
        }

        private void RemoveApplied(IDevice device)
        {
            lock (_applied) _applied.Remove(device);
        }

        private sealed class DesiredDevice
        {
            public DesiredDevice(DeviceConfiguration configuration, string key)
            {
                Configuration = configuration;
                Key = key;
            }

            public DeviceConfiguration Configuration { get; }
            public string Key { get; }
        }

        public async Task StartAllDevicesAsync(bool restartDevicesInErrorState, CancellationToken cancellationToken)
        {
            await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await StartCoreAsync(Devices.Where(d => d.State == DeviceState.Initialized || d.State == DeviceState.Stopped ||
                    (restartDevicesInErrorState && d.State == DeviceState.Error)).ToList(), cancellationToken).ConfigureAwait(false);
            }
            finally { _lifecycle.Release(); }
        }

        public async Task StopAllDevicesAsync(CancellationToken cancellationToken)
        {
            CancelOperations();
            await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { await StopCoreAsync(cancellationToken).ConfigureAwait(false); }
            finally { _lifecycle.Release(); }
        }

        private void CancelOperations()
        {
            DeviceOperationAdapter[] runtimes;
            lock (_runtimes) runtimes = _runtimes.Values.ToArray();
            foreach (var runtime in runtimes)
                runtime.Cancel();
        }

        private async Task<bool> InitializeAsync(IDevice device, DeviceConfiguration configuration, CancellationToken cancellationToken)
        {
            try
            {
                await Runtime(device).InitializeAsync(configuration, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                _logger.LogError(error, "Error configuring device {DeviceId}", device.Id);
                return false;
            }
        }

        private async Task StartCoreAsync(IEnumerable<IDevice> devices, CancellationToken cancellationToken)
        {
            var anyStarted = false;
            foreach (var device in devices)
            {
                try
                {
                    await Runtime(device).StartAsync(cancellationToken).ConfigureAwait(false);
                    anyStarted = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception error)
                {
                    _logger.LogError(error, "Error starting device {DeviceId}", device.Id);
                }
            }
            if (anyStarted)
                DevicesUpdated?.Invoke(ServiceEvent.Started);
        }

        private async Task StopCoreAsync(CancellationToken cancellationToken)
        {
            var errors = new List<Exception>();
            foreach (var device in Devices)
            {
                try { await Runtime(device).StopAsync(cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception error)
                {
                    _logger.LogError(error, "Error stopping device {DeviceId}", device.Id);
                    errors.Add(error);
                }
            }
            DevicesUpdated?.Invoke(ServiceEvent.Stopped);
            if (errors.Count > 0)
                throw new AggregateException("Devices did not stop cleanly; configuration was not replaced.", errors);
        }

        public Task ExecuteCommandAsync(string commandId, string value, CancellationToken cancellationToken)
        {
            var device = GetDeviceByCommandId(commandId);
            if (device == null)
                throw new UnknownCommandException(commandId);
            return Runtime(device).CommandAsync(commandId, value, cancellationToken);
        }

        public Task RefreshReportAsync(IDevice device, string group, string name, CancellationToken cancellationToken)
        {
            if (!(device is IRefreshableReportDevice) || !Devices.Contains(device))
                throw new ArgumentException("The device is not a registered refreshable device.", nameof(device));
            return Runtime(device).RefreshAsync(group, name, cancellationToken);
        }

        public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            await StopAllDevicesAsync(CancellationToken.None).ConfigureAwait(false);
            foreach (var runtime in _runtimes.Values)
                runtime.Dispose();
            _lifecycle.Dispose();
        }
    }
}
