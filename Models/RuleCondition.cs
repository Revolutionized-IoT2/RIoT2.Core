using RIoT2.Core.Interfaces;
using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class RuleCondition : IRuleItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public RuleType RuleType { get { return RuleType.Condition; } }

        //condition
        public string Description { get; set; }
        public ConditionType ConditionType { get; set; }

        //if-else params
        public string IfClause { get; set; }
        public FlowOperator ThenOperator { get; set; }
        public int ThenJumpTo { get; set; }
        public FlowOperator ElseOperator { get; set; }
        public int ElseJumpTo { get; set; }

        //switch params
        public string SwitchVariable { get; set; }
        public List<SwitchCase> SwitchCases { get; set; }

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(Description))
                yield return $"Condition [{Id}]: Missing Description";

            if (string.IsNullOrEmpty(Name))
                yield return $"Condition [{Id}]: Missing Name";

            if (ConditionType == ConditionType.IfElse)
            {
                if (string.IsNullOrEmpty(IfClause))
                    yield return $"Condition [{Id}]: Missing IF-Clause";

                if (ThenOperator == FlowOperator.Jump && ThenJumpTo == default(int))
                    yield return $"Condition [{Id}]: Invalid ThenJumpTo value";

                if (ElseOperator == FlowOperator.Jump && ElseJumpTo == default)
                    yield return $"Condition [{Id}]: Invalid ElseJumpTo value";
            }
            else 
            {
                if (string.IsNullOrEmpty(SwitchVariable))
                    yield return $"Condition [{Id}]: Missing SwitchVariable";

                if (SwitchCases == null || SwitchCases.Count < 1)
                    yield return $"Condition [{Id}]: Missing switch cases";
                else 
                {
                    foreach (var c in SwitchCases)
                    {
                        if(string.IsNullOrEmpty(c.Case))
                            yield return $"Condition [{Id}]: Empty case value";

                        if (c.Operation == FlowOperator.Jump && c.JumpTo == default)
                            yield return $"Condition [{Id}]: Invalid Case: {c.Case} JumpTo value";
                    }
                }
            }
        }
    }
}
