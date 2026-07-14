namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// Describes a template that defines the shape and default model of a device value (report or command).
    /// </summary>
    public interface ITemplate
    {
        /// <summary>
        /// Gets or sets the unique identifier of the template.
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// Gets or sets the display name of the template.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the device address the template is associated with.
        /// </summary>
        string Address { get; set; }

        /// <summary>
        /// Gets or sets the default model that describes the template's value structure.
        /// </summary>
        object Model { get; set; }

        /// <summary>
        /// Gets or sets the value type represented by the template.
        /// </summary>
        ValueType Type { get; set; }
    }
}
