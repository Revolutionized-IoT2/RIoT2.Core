using RIoT2.Core.Models;

namespace RIoT2.Core.Interfaces
{
    public interface IMessage
    {
        string Id { get; set; }
        ValueModel Value { get; }
    }
}
