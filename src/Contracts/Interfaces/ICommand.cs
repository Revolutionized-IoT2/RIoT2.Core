using System;
using System.Collections.Generic;
using System.Text;

namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// Represents a command targeted at a device, identified by its command id.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Gets or sets the unique identifier of the command.
        /// </summary>
        string Id { get; set; }
    }
}
