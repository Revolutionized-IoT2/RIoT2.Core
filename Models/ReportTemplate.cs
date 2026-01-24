using RIoT2.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace RIoT2.Core.Models
{
    public class ReportTemplate : IReport, IDeviceObject, ITemplate
    {
        public ReportTemplate() 
        {
            Parameters = new Dictionary<string, string>();
            Filters = new List<string>();
            RefreshSchedule = null;
            MaintainHistory = false;
            Model = null;  
        }

        public ValueType Type { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
        public IEnumerable<string> Filters { get; set; }
        public string RefreshSchedule { get; set; }
        public bool MaintainHistory { get; set; }
        public object Model { get; set; }

        /// <summary>
        /// Provides default report generated from the template
        /// </summary>
        /// <returns>Default report</returns>
        public Report GetAsReport() 
        {
            return new Report
            {
                Id = this.Id,
                Filter = Filters.FirstOrDefault(),
                Value = new ValueModel(this.Model)
            };
        }
    }
}