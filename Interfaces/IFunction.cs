using RIoT2.Core.Models;
using System.Collections.Generic;

namespace RIoT2.Core.Interfaces
{
    public interface IFunction
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        IEnumerable<ParameterTemplate> ExpectedParameters { get; }
        ValueModel Run(ValueModel data, IEnumerable<Parameter> parameters = null);
    }
}
