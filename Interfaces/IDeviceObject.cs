using System;
using System.Collections.Generic;
using System.Text;

namespace RIoT2.Core.Interfaces
{
    public interface IDeviceObject
    {
        string Name { get; set; }
        string Address { get; set; }
    }
}
