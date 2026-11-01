using RIoT2.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace RIoT2.Core.Models
{
    public class NodeReportTemplate : ReportTemplate, INodeTemplate
    {
        public string NodeId { get; set; }
        public string Node { get; set; }
        public string DeviceId { get; set; }
        public string Device { get; set; }
    }
}
