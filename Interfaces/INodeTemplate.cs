using System;
using System.Collections.Generic;
using System.Text;

namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// Associates a device with the node that hosts it.
    /// </summary>
    public interface INodeTemplate
    {
        /// <summary>
        /// Gets or sets the unique identifier of the hosting node.
        /// </summary>
        string NodeId { get; set; }

        /// <summary>
        /// Gets or sets the display name of the hosting node.
        /// </summary>
        string Node { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the device.
        /// </summary>
        string DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the display name of the device.
        /// </summary>
        string Device { get; set; }
    }
}
