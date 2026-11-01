using System.Collections.Generic;
using System.Dynamic;

namespace RIoT2.Core.Models
{
    public class DashboardElement
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Icon { get; set; }
        public int NumberOfPreviousReports { get; set; }
        public ReportTemplate ReportTemplate { get; set; }
        public CommandTemplate CommandTemplate { get; set; }
        public List<Report> PreviousReports { get; set; }
        public ExpandoObject Properties { get; set; }
    }
}
