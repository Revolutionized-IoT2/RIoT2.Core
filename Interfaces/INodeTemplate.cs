using System;
using System.Collections.Generic;
using System.Text;

namespace RIoT2.Core.Interfaces
{
    public interface INodeTemplate
    {
        string NodeId { get; set; }
        string Node { get; set; }
        string DeviceId { get; set; }
        string Device { get; set; }
    }
}
