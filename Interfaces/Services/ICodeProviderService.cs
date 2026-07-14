using System;

namespace RIoT2.Core.Interfaces.Services
{
    /// <summary>
    /// Issues and validates time- and usage-limited codes, typically used for device onboarding.
    /// </summary>
    public interface ICodeProviderService
    {
        /// <summary>
        /// Creates a new code with the specified validity constraints.
        /// </summary>
        /// <param name="timesValid">The maximum number of times the code may be used, or <c>null</c> for unlimited.</param>
        /// <param name="from">The earliest time the code becomes valid, or <c>null</c> for immediately.</param>
        /// <param name="to">The latest time the code remains valid, or <c>null</c> for no expiry.</param>
        /// <returns>The newly created code.</returns>
        string CreateCode(int? timesValid = null, DateTime? from = null, DateTime? to = null);

        /// <summary>
        /// Attempts to use (consume) the specified code.
        /// </summary>
        /// <param name="code">The code to validate and consume.</param>
        /// <returns><c>true</c> if the code was valid and successfully used; otherwise, <c>false</c>.</returns>
        bool UseCode(string code);
    }
}
