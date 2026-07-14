namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// Represents a report produced by a device, carrying the identifier of the report source.
    /// </summary>
    public interface IReport
    {
        /// <summary>
        /// Gets or sets the unique identifier of the report.
        /// </summary>
        string Id { get; set; }
    }
}
