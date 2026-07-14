using System.Collections.Generic;
using RIoT2.Core.Interfaces;

namespace RIoT2.Core.Interfaces.Services
{
    /// <summary>
    /// Provides access to the reusable functions available for rule evaluation.
    /// </summary>
    public interface IFunctionService
    {
        /// <summary>
        /// Gets all available functions.
        /// </summary>
        /// <returns>The collection of registered functions.</returns>
        IEnumerable<IFunction> GetFunctions();

        /// <summary>
        /// Gets the function with the specified identifier.
        /// </summary>
        /// <param name="id">The identifier of the function to retrieve.</param>
        /// <returns>The matching function, or <c>null</c> if none is found.</returns>
        IFunction GetFunction(string id);
    }
}