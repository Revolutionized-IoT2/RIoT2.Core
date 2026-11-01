namespace RIoT2.Core.Interfaces
{
    public interface IRefreshableReportDevice : IDevice
    {
        void RefreshReport(string group, string name);
    }
}