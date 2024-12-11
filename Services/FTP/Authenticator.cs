﻿using System.Collections.Generic;
using System.Linq;
using Zhaobang.FtpServer.Authenticate;

namespace RIoT2.Core.Services.FTP
{
    internal class Authenticator : IAuthenticator
    {
        private List<FtpUser> _users;

        public Authenticator(List<FtpUser> users)
        {
            _users = users;
        }

        public bool Authenticate(string username, string password)
        {
            return _users.FirstOrDefault(x => x.UserName == username && x.Password == password) != null;
        }
    }
}