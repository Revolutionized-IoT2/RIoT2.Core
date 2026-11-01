using System.Threading;
using System.Threading.Tasks;

namespace RIoT2.Core.Interfaces.Services
{
    public interface IAsyncCommandService : ICommandService
    {
        Task ExecuteJsonCommandAsync(string json, CancellationToken cancellationToken);
    }
}
