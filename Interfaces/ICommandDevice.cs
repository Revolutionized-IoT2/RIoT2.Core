using RIoT2.Core.Models;
using System.Collections.Generic;

namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// Represents a device that can receive and execute commands.
    /// </summary>
    public interface ICommandDevice : IDevice
    {
        /// <summary>
        /// Gets the command templates describing the commands the device accepts.
        /// </summary>
        IEnumerable<CommandTemplate> CommandTemplates { get; }

        /// <summary>
        /// Executes the specified command on the device.
        /// </summary>
        /// <param name="commandId">The identifier of the command to execute.</param>
        /// <param name="value">The command value, serialized as JSON.</param>
        void ExecuteCommand(string commandId, string value);
    }
}