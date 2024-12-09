using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class DashboardConfiguration
    {
        public List<DashboardPage> Pages { get; set; }
        public string Name { get; set; }
        public string Id { get; set; }
    }
}
