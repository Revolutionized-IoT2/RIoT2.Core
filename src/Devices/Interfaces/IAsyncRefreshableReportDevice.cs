using System.Threading;
using System.Threading.Tasks;

namespace RIoT2.Core.Interfaces
{
    public interface IAsyncRefreshableReportDevice : IRefreshableReportDevice
    {
        Task RefreshReportAsync(string group, string name, CancellationToken cancellationToken);
    }
}
