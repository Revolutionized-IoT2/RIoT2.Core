using System.Threading;
using System.Threading.Tasks;

namespace RIoT2.Core.Interfaces
{
    /// <summary>Optional awaited lifecycle. Implementations must finish owned work before StopAsync returns.</summary>
    public interface IAsyncDevice : IDevice
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }
}
