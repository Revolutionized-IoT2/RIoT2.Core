namespace RIoT2.Core.Models.Matter
{
    /// <summary>
    /// Binds a Matter cluster attribute to a RIoT command: the inbound path that lets a Matter
    /// controller such as Google Home actually drive the device.
    /// </summary>
    /// <remarks>
    /// When a controller changes <see cref="Attribute"/> (by invoking a cluster command, e.g. On/Off
    /// Toggle or Level Control MoveToLevel), the bridge applies the inverse of <see cref="Scale"/>,
    /// writes the value into <see cref="ValuePath"/> of a payload seeded from
    /// <see cref="ValueTemplateJson"/>, and sends it as a <see cref="Command"/> against
    /// <see cref="CommandTemplateId"/> over the existing MQTT command path.
    /// <para>
    /// Several bindings may target the same <see cref="CommandTemplateId"/>: for an entity command such
    /// as a light's <c>{ "on": true, "brightness": 80 }</c> payload, declare one binding per attribute
    /// with a different <see cref="ValuePath"/> and a shared <see cref="ValueTemplateJson"/>.
    /// </para>
    /// </remarks>
    public class MatterCommandBinding
    {
        /// <summary>The cluster attribute whose change triggers the command.</summary>
        public MatterAttribute Attribute { get; set; }

        /// <summary>
        /// The id of the <see cref="CommandTemplate"/> to send. Must be an id from the same
        /// <see cref="DeviceConfiguration"/> the declaration was built against.
        /// </summary>
        public string CommandTemplateId { get; set; }

        /// <summary>
        /// A dotted path into the command's <see cref="ValueModel"/> to write the value to (e.g.
        /// <c>"brightness"</c>), or <see langword="null"/> to send the value as the whole payload.
        /// </summary>
        public string ValuePath { get; set; }

        /// <summary>
        /// An optional JSON payload the command value is seeded from before <see cref="ValuePath"/> is
        /// written, for entity commands that require fields beyond the bound attribute. When
        /// <see langword="null"/>, the command template's own model is used.
        /// </summary>
        public string ValueTemplateJson { get; set; }

        /// <summary>
        /// The unit conversion between RIoT and Matter, declared in the RIoT-to-Matter direction; the
        /// inverse is applied here. Defaults to <see cref="MatterValueScale.None"/>.
        /// </summary>
        public MatterValueScale Scale { get; set; }
    }
}
