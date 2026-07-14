using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;

namespace RIoT2.Core.Services
{
    /// <summary>
    /// Default <see cref="ICommandService"/> implementation that routes JSON commands to the
    /// device responsible for handling them.
    /// </summary>
    public class CommandService : ICommandService
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
        {
            var cmd = Command.Create(json);
            if (cmd == null)
                return;

            //TODO CHANGE TO ValueModel
            _deviceService.GetDeviceByCommandId(cmd.Id)?
                .ExecuteCommand(cmd.Id, cmd.Value.ToJson());
        }
    }
}
