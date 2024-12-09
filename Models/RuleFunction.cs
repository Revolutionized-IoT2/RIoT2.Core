using RIoT2.Core.Interfaces;
using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class RuleFunction : IRuleItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public RuleType RuleType { get { return RuleType.Function; } }

        //function 
        public string FunctionId { get; set; }
        public string Description { get; set; }
        public List<Parameter> Parameters { get; set; }
        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(FunctionId))
                yield return $"Function [{Id}]: Missing FunctionId";

            if (string.IsNullOrEmpty(Description))
                yield return $"Function [{Id}]: Missing Description";
             
            if (string.IsNullOrEmpty(Name))
                yield return $"Function [{Id}]: Missing Name";
        }
    }
}
