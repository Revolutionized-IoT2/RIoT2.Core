using System.Collections.Generic;

namespace RIoT2.Core.Interfaces
{
    public interface IRuleItem
    {
        int Id { get; set; }
        string Name { get; set; }
        RuleType RuleType { get; }
        IEnumerable<string> Validate(); 
    }
}
