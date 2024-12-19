using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;

namespace RIoT2.Core.Services
{
    public class CommandService : ICommandService
    {
        IDeviceService _deviceService;
        public CommandService(IDeviceService deviceService) 
        {
            _deviceService = deviceService;
        }

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
