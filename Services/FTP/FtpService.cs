using RIoT2.Core.Interfaces.Services;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using Zhaobang.FtpServer;
using static RIoT2.Core.Services.FTP.CustomLocalDataConnection;

namespace RIoT2.Core.Services.FTP
{
    public class FtpService : IFtpService
    {
        private FtpServer _ftpServer;
        private CancellationToken _token;
        private CancellationTokenSource _tokenSource;

        public event FileReceivedHandler FileReceived;

        private void fileReceived(InMemoryStream inMemoryStream)
        {
            FileReceived(inMemoryStream);
        }

        public void Stop()
        {
            if (_token != null && _token.CanBeCanceled)
                _tokenSource.Cancel();

            _tokenSource = null;
        }

        public async Task StartAsync(List<FtpUser> users, int port)
        {
            _ftpServer = new FtpServer(
                new IPEndPoint(IPAddress.Any, port),
                new InMemoryFileProviderFactory(),
                new CustomLocalDataConnectionFactory(fileReceived),
                new Authenticator(users)
            );

            _tokenSource = new CancellationTokenSource();
            _token = _tokenSource.Token;
            await _ftpServer.RunAsync(_token);
        }
    }
}