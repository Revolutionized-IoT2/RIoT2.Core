using System;
using System.Threading;
using System.Threading.Tasks;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Models;
using RIoT2.Core.Plugins;

namespace RIoT2.Core.Services
{
    // Serializes a device's operations and calls it only through DeviceCalls (async; plan M12 step 5).
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
                    await DeviceCalls.StopAsync(_device, cancellationToken).ConfigureAwait(false);
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
                    await DeviceCalls.StartAsync(_device, linked.Token).ConfigureAwait(false);
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
                await DeviceCalls.StopAsync(_device, cancellationToken).ConfigureAwait(false);
                _owned = false;
            }
            finally { _gate.Release(); }
        }

        public Task CommandAsync(string id, string value, CancellationToken cancellationToken) =>
            RunAsync(token => DeviceCalls.ExecuteCommandAsync(_device, id, value, token), cancellationToken);

        public Task RefreshAsync(string group, string name, CancellationToken cancellationToken) =>
            RunAsync(token => DeviceCalls.RefreshAsync(_device, group, name, token), cancellationToken);

        private async Task RunAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
        {
            CancellationTokenSource linked;
            lock (_sync)
            {
                if (!_accepting)
                    throw new DeviceNotReadyException("The device is not accepting work.");
                linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _run.Token);
            }
            using (linked)
            {
                await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
                try
                {
                    linked.Token.ThrowIfCancellationRequested();
                    if (!IsActive)
                        throw new DeviceNotReadyException("The device is not running.");
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
