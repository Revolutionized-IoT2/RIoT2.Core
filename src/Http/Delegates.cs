using System.Collections.Generic;

namespace RIoT2.Core
{
    public delegate void WebMessageHandler(string method, string body, Dictionary<string, string> querystrings, Dictionary<string, string> headers);
}
