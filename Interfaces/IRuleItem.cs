using System.Collections.Generic;

namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// Represents a single item within a rule, such as a trigger, function, condition, or output.
    /// </summary>
    public interface IRuleItem
    {
        /// <summary>
        /// Gets or sets the identifier of the item within its rule.
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// Gets or sets the display name of the item.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets the kind of rule item.
        /// </summary>
        RuleType RuleType { get; }

        /// <summary>
        /// Validates the item's configuration.
        /// </summary>
        /// <returns>A sequence of validation error messages; empty when the item is valid.</returns>
        IEnumerable<string> Validate(); 
    }
}
