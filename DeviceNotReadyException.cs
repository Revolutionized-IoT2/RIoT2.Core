using System;

namespace RIoT2.Core
{
    /// <summary>
    /// The device that owns a command exists but isn't running or accepting work yet, for example while
    /// the node applies a configuration. A node reports it as
    /// <see cref="Models.CommandResultStatus.Rejected"/>, so the orchestrator delivers the command again.
    /// </summary>
    public sealed class DeviceNotReadyException : InvalidOperationException
    {
        public DeviceNotReadyException(string message) : base(message) { }
    }
}
