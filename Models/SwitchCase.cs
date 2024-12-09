namespace RIoT2.Core.Models
{
    public class SwitchCase
    {
        public string Case { get; set; }
        public FlowOperator Operation { get; set; }
        public int JumpTo { get; set; }
    }
}