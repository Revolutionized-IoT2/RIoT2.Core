using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class DashboardPage
    {
        public string Name { get; set; }
        public string Icon { get; set; }
        public List<DashboardComponent> Components { get; set; }
    }
}
