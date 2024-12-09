namespace RIoT2.Core.Models
{
    public class ParameterTemplate
    {
        public bool IsOptional { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } //show description in UI
        public ValueType Type { get; set; }
    }
}