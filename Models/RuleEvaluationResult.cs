using RIoT2.Core;

namespace RIoT2.Core.Models
{
    public class RuleEvaluationResult
    {
        public OutputOperation Operation { get; set; }
        public string CommandId { get; set; }
        public ValueModel Value { get; set; }
    }
}