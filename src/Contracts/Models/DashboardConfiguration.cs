using RIoT2.Core.Interfaces;
using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class DashboardConfiguration : IStoredObject
    {
        public List<DashboardPage> Pages { get; set; }
        public string Name { get; set; }
        public string Id { get; set; }
    }
}
