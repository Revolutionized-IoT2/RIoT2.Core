using System.IO;

namespace RIoT2.Core.Services.FTP
{
    public class InMemoryStream : MemoryStream
    {
        private readonly string _username;
        private readonly string _filename;

        public InMemoryStream(string username, string filename)
        {
            _username = username;
            _filename = filename;
        }

        public string Username => _username;
        public string Filename => _filename;
    }
}