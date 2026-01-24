using RIoT2.Core.Interfaces;

namespace RIoT2.Core.Models
{
    public class CommandTemplate : ICommand, IDeviceObject, ITemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public object Model { get; set; }
        public ValueType Type { get; set; }
    }
}