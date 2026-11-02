using System;
using System.Collections.Generic;

namespace RIoT2.Core.Plugins
{
    /// <summary>
    /// The device parameters don't bind to the device's options: a required one is missing, or a value doesn't
    /// convert. <see cref="Exception.Message"/> lists every problem.
    /// </summary>
    public class DeviceOptionsException : Exception
    {
        public DeviceOptionsException(IReadOnlyList<string> problems)
            : base("Invalid device parameters: " + string.Join("; ", problems ?? Array.Empty<string>()))
        {
            Problems = problems ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Problems { get; }
    }
}
