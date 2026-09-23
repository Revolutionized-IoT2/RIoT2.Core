using System;
using System.Threading;
using System.Threading.Tasks;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Models;

namespace RIoT2.Core.Services
{
    internal sealed class DeviceOperationAdapter : IDisposable
    {
        private readonly IDevice _device;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly object _sync = new object();
        private CancellationTokenSource _run = new CancellationTokenSource();
        private bool _accepting;
        private bool _owned;

        public DeviceOperationAdapter(IDevice device) => _device = device;
        public bool IsActive { get { lock (_sync) return _accepting && _device.State == DeviceState.Running; } }

        public void Cancel()
        {
            lock (_sync)
            {
                _accepting = false;
                _run.Cancel();
            }
        }

        public async Task InitializeAsync(DeviceConfiguration configuration, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_owned)
                    throw new InvalidOperationException("Stop the device before replacing its configuration.");
                _device.Initialize(configuration);
            }
            finally { _gate.Release(); }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_owned)
                {
                    Cancel();
                    if (_device is IAsyncDevice previous)
                        await previous.StopAsync(cancellationToken).ConfigureAwait(false);
                    else
                        await Task.Run(() => _device.Stop(), cancellationToken).ConfigureAwait(false);
                    _owned = false;
                }
                lock (_sync)
                {
                    _run.Dispose();
                    _run = new CancellationTokenSource();
                    _accepting = true;
                    _owned = true;
                }
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _run.Token))
                {
                    if (_device is IAsyncDevice asynchronous)
                        await asynchronous.StartAsync(linked.Token).ConfigureAwait(false);
                    else
                        await Task.Run(() => _device.Start(), linked.Token).ConfigureAwait(false);
                    linked.Token.ThrowIfCancellationRequested();
                }
            }
            catch
            {
                Cancel();
                throw;
            }
            finally { _gate.Release(); }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Cancel();
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_owned)
                    return;
                if (_device is IAsyncDevice asynchronous)
                    await asynchronous.StopAsync(cancellationToken).ConfigureAwait(false);
                else
                    await Task.Run(() => _device.Stop(), cancellationToken).ConfigureAwait(false);
                _owned = false;
            }
            finally { _gate.Release(); }
        }

        public Task CommandAsync(string id, string value, CancellationToken cancellationToken) =>
            RunAsync(token => _device is IAsyncCommandDevice asynchronous
                ? asynchronous.ExecuteCommandAsync(id, value, token)
                : Task.Run(() => ((ICommandDevice)_device).ExecuteCommand(id, value), token), cancellationToken);

        public Task RefreshAsync(string group, string name, CancellationToken cancellationToken) =>
            RunAsync(token => _device is IAsyncRefreshableReportDevice asynchronous
                ? asynchronous.RefreshReportAsync(group, name, token)
                : Task.Run(() => ((IRefreshableReportDevice)_device).RefreshReport(group, name), token), cancellationToken);

        private async Task RunAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
        {
            CancellationTokenSource linked;
            lock (_sync)
            {
                if (!_accepting)
                    throw new InvalidOperationException("The device is not accepting work.");
                linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _run.Token);
            }
            using (linked)
            {
                await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
                try
                {
                    linked.Token.ThrowIfCancellationRequested();
                    if (!IsActive)
                        throw new InvalidOperationException("The device is not running.");
                    await operation(linked.Token).ConfigureAwait(false);
                    linked.Token.ThrowIfCancellationRequested();
                }
                finally { _gate.Release(); }
            }
        }

        public void Dispose()
        {
            _run.Dispose();
            _gate.Dispose();
        }
    }
}
