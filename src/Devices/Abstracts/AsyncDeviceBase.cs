using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Models;
using System.Linq;

namespace RIoT2.Core.Abstracts
{
    /// <summary>
    /// The base class for devices (plan M12 step 5, Core 1.23.0; deriving from <see cref="DeviceBase"/> directly is
    /// the deprecated synchronous lifecycle). The node awaits <see cref="StartDeviceAsync"/>, <see cref="StopDeviceAsync"/> and <see cref="RefreshAsync"/>
    /// through <see cref="IAsyncDevice"/> and <see cref="IAsyncRefreshableReportDevice"/>; a command device implements
    /// <see cref="IAsyncCommandDevice.ExecuteCommandAsync"/>, and this class supplies
    /// <see cref="ICommandDevice.ExecuteCommand"/>.
    /// <para>
    /// The synchronous <see cref="IDevice"/> members don't block on the async ones: they throw
    /// <see cref="NotSupportedException"/>. Hosts since Node 1.11 call the async members; call a device the way the node
    /// does with <see cref="Plugins.DeviceCalls"/>.
    /// </para>
    /// </summary>
    public abstract class AsyncDeviceBase : DeviceBase, IAsyncDevice, IAsyncRefreshableReportDevice
    {
        protected AsyncDeviceBase(ILogger logger) : base(logger, legacy: false) { }

        protected abstract Task StartDeviceAsync(CancellationToken cancellationToken);
        protected abstract Task StopDeviceAsync(CancellationToken cancellationToken);

        /// <summary>Refreshes one report, or every report when <paramref name="report"/> is null; nothing by default.</summary>
        protected virtual Task RefreshAsync(ReportTemplate? report, CancellationToken cancellationToken) => Task.CompletedTask;

        public sealed override void StartDevice() => throw NotSynchronous(nameof(StartAsync));
        public sealed override void StopDevice() => throw NotSynchronous(nameof(StopAsync));
        public sealed override void Refresh(ReportTemplate? report) => throw NotSynchronous(nameof(RefreshReportAsync));

        /// <summary>
        /// <see cref="ICommandDevice.ExecuteCommand"/> for a device that implements <see cref="IAsyncCommandDevice"/>:
        /// throws, because the host calls <c>ExecuteCommandAsync</c>. A device with synchronous commands overrides it.
        /// </summary>
        public virtual void ExecuteCommand(string commandId, string value) => throw NotSynchronous("ExecuteCommandAsync");

        private NotSupportedException NotSynchronous(string member) =>
            new NotSupportedException($"{GetType().Name} is an asynchronous device: call {member} " +
                "(RIoT2.Core.Plugins.DeviceCalls does). Its synchronous members don't block (plan M12 step 5).");

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                SetState(DeviceState.Running);
                await StartDeviceAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SetState(DeviceState.Stopped);
                throw;
            }
            catch (Exception error)
            {
                SetState(DeviceState.Error, error.Message);
                throw new Exception($"Error starting {Name}:{Id}", error);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            SetState(DeviceState.Stopped);
            try { await StopDeviceAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception error)
            {
                SetState(DeviceState.Error, error.Message);
                throw;
            }
        }

        public async Task RefreshReportAsync(string group, string name, CancellationToken cancellationToken)
        {
            if (group != Id)
                return;
            if (State != DeviceState.Running)
                throw new InvalidOperationException("The device is not running.");
            try
            {
                await RefreshAsync(ReportTemplates?.FirstOrDefault(r => r.Id == name), cancellationToken).ConfigureAwait(false);
            }
            // The caller's cancellation, or one caused by stopping the device, isn't a failure; another one
            // (an HTTP timeout while the device runs) is.
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || State != DeviceState.Running) { throw; }
            catch (Exception error)
            {
                SetState(DeviceState.Error, error.Message);
                throw;
            }
        }
    }
}
