using System.Collections.Generic;

namespace RIoT2.Core.Interfaces.Services
{
    public interface IFunctionService
    {
        IEnumerable<IFunction> GetFunctions();
        IFunction GetFunction(string id);
    }
}