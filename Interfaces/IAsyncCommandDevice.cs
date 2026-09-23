using System.Threading;
using System.Threading.Tasks;

namespace RIoT2.Core.Interfaces
{
    public interface IAsyncCommandDevice : ICommandDevice
    {
        Task ExecuteCommandAsync(string commandId, string value, CancellationToken cancellationToken);
    }
}
