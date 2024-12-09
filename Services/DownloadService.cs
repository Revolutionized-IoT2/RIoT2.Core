using RIoT2.Core.Interfaces.Services;

namespace RIoT2.Core.Services
{
    public class DownloadService : IDownloadService
    {
        private string _baseUrl = "";

        public string GetDownloadUrl(string filename)
        {
            return _baseUrl + filename;
        }

        public void SetBaseUrl(string url)
        {
            _baseUrl = url;
            if (!_baseUrl.EndsWith("/"))
                _baseUrl += "/";
        }
    }
}
