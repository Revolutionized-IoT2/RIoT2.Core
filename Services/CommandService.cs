using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RIoT2.Core.Services
{
    /// <summary>
    /// Default <see cref="ICommandService"/> implementation that routes JSON commands to the
    /// device responsible for handling them.
    /// </summary>
    public class CommandService : IAsyncCommandService
    {
        IDeviceService _deviceService;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandService"/> class.
        /// </summary>
        /// <param name="deviceService">The device service used to resolve the target device for a command.</param>
        public CommandService(IDeviceService deviceService) 
        {
            _deviceService = deviceService;
        }

        /// <inheritdoc/>
        public void ExecuteJsonCommand(string json)
            => ExecuteJsonCommandAsync(json, CancellationToken.None).GetAwaiter().GetResult();

        public async Task ExecuteJsonCommandAsync(string json, CancellationToken cancellationToken)
        {
            var cmd = Command.Create(json);
            if (cmd?.Value == null || string.IsNullOrWhiteSpace(cmd.Id))
                throw new ArgumentException("A command requires an id and value.", nameof(json));
            if (_deviceService is IAsyncDeviceService asynchronous)
                await asynchronous.ExecuteCommandAsync(cmd.Id, cmd.Value.ToJson(), cancellationToken).ConfigureAwait(false);
            else
            {
                var device = _deviceService.GetDeviceByCommandId(cmd.Id);
                if (device == null)
                    throw new InvalidOperationException("No device handles command " + cmd.Id + ".");
                await Task.Run(() => device.ExecuteCommand(cmd.Id, cmd.Value.ToJson()), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
