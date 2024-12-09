using RIoT2.Core.Interfaces;
using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class RuleTrigger : IRuleItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public RuleType RuleType { get { return RuleType.Trigger; } }

        //trigger
        public string ReportId { get; set; }
        public string Description { get; set; }
        public string Filter { get; set; }

        //Template values! -> update from template when given to UI
        public List<string> FilterOptions { get; set; }

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(ReportId))
                yield return "Trigger: Missing ReportId";

            if (string.IsNullOrEmpty(Description))
                yield return "Trigge: Missing Description";

            if (string.IsNullOrEmpty(Name))
                yield return "Trigger: Missing Name";
        }
    }
}