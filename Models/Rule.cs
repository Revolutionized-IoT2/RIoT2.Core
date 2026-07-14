using RIoT2.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace RIoT2.Core.Models
{
    /// <summary>
    /// Represents a rule composed of ordered rule items (trigger, functions, conditions, outputs)
    /// that are evaluated against a data model.
    /// </summary>
    public class Rule
    {
        private ValueModel _data;

        /// <summary>
        /// Gets or sets the unique identifier of the rule.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the display name of the rule.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the human-readable description of the rule.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the rule is active and should be evaluated.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the tags used to categorize the rule.
        /// </summary>
        public List<string> Tags { get; set; }

        /// <summary>
        /// Gets or sets the ordered items that make up the rule.
        /// </summary>
        virtual public List<IRuleItem> RuleItems { get; set; }

        /// <summary>
        /// Gets or sets the data model the rule operates on, as a plain object.
        /// </summary>
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

        /// <summary>
        /// Gets the rule's data model as a <see cref="ValueModel"/>.
        /// </summary>
        public ValueModel Model { get { return _data; } }

        /// <summary>
        /// Gets the trigger rule item (the first item), or <c>null</c> if the rule has no items.
        /// </summary>
        public RuleTrigger Trigger { 
            get 
            {
                if(RuleItems == null || RuleItems.Count < 1)
                    return null;

                return RuleItems[0] as RuleTrigger;
            } 
        }

        /// <summary>
        /// Validates the rule's structure and its items, deactivating the rule when validation fails.
        /// </summary>
        /// <returns>A sequence of validation error messages; empty when the rule is valid.</returns>
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