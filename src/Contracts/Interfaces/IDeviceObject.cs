using System;
using System.Collections.Generic;
using System.Text;

namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// Represents an addressable device object identified by a name and an address.
    /// </summary>
    public interface IDeviceObject
    {
        /// <summary>
        /// Gets or sets the display name of the device object.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the address of the device object.
        /// </summary>
        string Address { get; set; }
    }
}
