using RIoT2.Core.Models;
using System.Collections.Generic;

namespace RIoT2.Core.Interfaces
{
    public interface ICommandDevice : IDevice
    {
        IEnumerable<CommandTemplate> CommandTemplates { get; }
        void ExecuteCommand(string commandId, string value);
    }
}