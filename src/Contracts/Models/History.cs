using RIoT2.Core.Interfaces;
using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class History
    {
        public string Id { get; set; }
        public IEnumerable<IReport> Reports { get; set; }
    }
}
