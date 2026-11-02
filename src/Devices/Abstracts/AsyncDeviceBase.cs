using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Models;
using System.Linq;

namespace RIoT2.Core.Abstracts
{
    public abstract class AsyncDeviceBase : DeviceBase, IAsyncDevice, IAsyncRefreshableReportDevice
    {
        protected AsyncDeviceBase(ILogger logger) : base(logger) { }

        protected abstract Task StartDeviceAsync(CancellationToken cancellationToken);
        protected abstract Task StopDeviceAsync(CancellationToken cancellationToken);
        protected abstract Task RefreshAsync(ReportTemplate report, CancellationToken cancellationToken);

#pragma warning disable VSTHRD002 // Plan M11 group A: DeviceBase's synchronous contract for older hosts; the node calls the async members.
        public sealed override void StartDevice() => StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        public sealed override void StopDevice() => StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        public sealed override void Refresh(ReportTemplate report) => RefreshAsync(report, CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

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
            // Only the caller's cancellation; another one (an HTTP timeout) is a failure.
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                SetState(DeviceState.Error, error.Message);
                throw;
            }
        }
    }
}
