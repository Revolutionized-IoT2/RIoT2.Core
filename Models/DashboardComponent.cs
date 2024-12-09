using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class DashboardComponent
    {

        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public DashboardComponentType Type { get; set; }
        public DashboardComponentSize Size { get; set; }
        public List<DashboardElement> Elements { get; set; }
    }
}
