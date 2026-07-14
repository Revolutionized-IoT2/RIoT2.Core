using RIoT2.Core.Models;
using System.Collections.Generic;

namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// Represents a reusable function that can be evaluated as part of a rule against an input value.
    /// </summary>
    public interface IFunction
    {
        /// <summary>
        /// Gets the unique identifier of the function.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the display name of the function.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the human-readable description of what the function does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the parameter templates describing the parameters the function expects.
        /// </summary>
        IEnumerable<ParameterTemplate> ExpectedParameters { get; }

        /// <summary>
        /// Executes the function against the supplied input value.
        /// </summary>
        /// <param name="data">The input value to process.</param>
        /// <param name="parameters">The optional parameters that configure the execution.</param>
        /// <returns>The resulting value produced by the function.</returns>
        ValueModel Run(ValueModel data, IEnumerable<Parameter> parameters = null);
    }
}
