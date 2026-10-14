using System;

namespace RIoT2.Core.Plugins
{
    /// <summary>
    /// Marks a device that needs hardware or a remote service a test machine doesn't have. The conformance test
    /// still resolves it, reads its template and records the parameters it reads, but tolerates a configuration
    /// error and doesn't start it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class RequiresHardwareAttribute : Attribute
    {
        public RequiresHardwareAttribute(string reason = null) => Reason = reason;

        /// <summary>What the device needs, for example "I2C bus" or "MQTT broker".</summary>
        public string Reason { get; }
    }
}
