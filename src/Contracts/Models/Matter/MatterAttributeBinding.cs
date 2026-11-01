namespace RIoT2.Core.Models.Matter
{
    /// <summary>
    /// Binds a RIoT report to a Matter cluster attribute: the outbound path that makes a device's state
    /// change in RIoT visible to a Matter controller such as Google Home.
    /// </summary>
    /// <remarks>
    /// When a report matching <see cref="ReportTemplateId"/> (and, when set, <see cref="Filter"/>)
    /// arrives at the orchestrator, the bridge reads <see cref="ValuePath"/> out of the report's
    /// <see cref="ValueModel"/>, applies <see cref="Scale"/>, and writes the result to
    /// <see cref="Attribute"/>. The Matter subscription report to the controller follows automatically.
    /// </remarks>
    public class MatterAttributeBinding
    {
        /// <summary>The cluster attribute this report drives.</summary>
        public MatterAttribute Attribute { get; set; }

        /// <summary>
        /// The id of the <see cref="ReportTemplate"/> whose reports drive the attribute. Must be an id
        /// from the same <see cref="DeviceConfiguration"/> the declaration was built against.
        /// </summary>
        public string ReportTemplateId { get; set; }

        /// <summary>
        /// A dotted path into the report's <see cref="ValueModel"/> (e.g. <c>"state.brightness"</c>), or
        /// <see langword="null"/> to use the whole value.
        /// </summary>
        public string ValuePath { get; set; }

        /// <summary>
        /// An optional report filter to match, for report templates that emit several filtered streams
        /// under one id. <see langword="null"/> matches any filter.
        /// </summary>
        public string Filter { get; set; }

        /// <summary>The unit conversion applied to the extracted value. Defaults to <see cref="MatterValueScale.None"/>.</summary>
        public MatterValueScale Scale { get; set; }
    }
}
