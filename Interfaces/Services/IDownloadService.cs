namespace RIoT2.Core.Interfaces.Services
{
    public interface IDownloadService
    {
        void SetBaseUrl(string url);
        string GetDownloadUrl(string filename);
    }
}