using RIoT2.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace RIoT2.Core.Models
{
    public class Rule
    {
        private ValueModel _data;
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public List<string> Tags { get; set; }
        virtual public List<IRuleItem> RuleItems { get; set; }
        public object DataModel 
        {
            get 
            {
                if(_data == null)
                    return null;

                return _data.GetAsObject();
            }
            set 
            {
                _data = new ValueModel(value);  
            }
        }

        public ValueModel Model { get { return _data; } }
        public RuleTrigger Trigger { 
            get 
            {
                if(RuleItems == null || RuleItems.Count < 1)
                    return null;

                return RuleItems[0] as RuleTrigger;
            } 
        }
        public IEnumerable<string> Validate()
        {
            //if there is only one item -> not valid
            if (RuleItems == null || RuleItems.Count < 2)
            {
                //rules.RemoveAt(i);
                IsActive = false;
                yield return "Missing rule items. Minimum is 2";
                yield break; // no point going any further
            }

            //first item must be trigger
            if (RuleItems?[0] == null || RuleItems[0].RuleType != RuleType.Trigger)
            {
                //rules.RemoveAt(i);
                IsActive = false;
                yield return "First rule item is not Trigger.";
            }

            //there must be at least one output
            if (RuleItems != null && !RuleItems.Any(x => x.RuleType == RuleType.Output))
            {
                //rules.RemoveAt(i);
                IsActive = false;
                yield return "Rule is missing output.";
            }

            foreach (var item in RuleItems) 
            {
                var itemValidationResults = item.Validate();
                if (itemValidationResults != null) 
                {
                    foreach(var result in itemValidationResults)
                        yield return result;
                }
            }
        }
    }
}