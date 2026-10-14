using System;

namespace RIoT2.Core
{
    /// <summary>
    /// No device on the node handles the command id. A node reports it as
    /// <see cref="Models.CommandResultStatus.UnknownCommand"/> (design 7.1).
    /// </summary>
    public sealed class UnknownCommandException : InvalidOperationException
    {
        public UnknownCommandException(string commandId)
            : base("No device handles command " + commandId + ".")
        {
            CommandId = commandId;
        }

        /// <summary>The command id that no device handles.</summary>
        public string CommandId { get; }
    }
}
