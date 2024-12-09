using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using static RIoT2.Core.Services.FTP.CustomLocalDataConnection;
using RIoT2.Core.Services.FTP;

namespace RIoT2.Core.Interfaces.Services
{
    public interface IFtpService
    {
        event FileReceivedHandler FileReceived;
        Task StartAsync(List<FtpUser> users, int port);
        void Stop();
    }
}