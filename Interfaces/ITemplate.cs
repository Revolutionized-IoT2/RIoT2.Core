namespace RIoT2.Core.Interfaces
{
    public interface ITemplate
    {
        string Id { get; set; }
        string Name { get; set; }
        string Address { get; set; }
        object Model { get; set; }
        ValueType Type { get; set; }
    }
}
