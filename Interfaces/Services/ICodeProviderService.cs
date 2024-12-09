using System;

namespace RIoT2.Core.Interfaces.Services
{
    public interface ICodeProviderService
    {
        string CreateCode(int? timesValid = null, DateTime? from = null, DateTime? to = null);

        bool UseCode(string code);
    }
}
