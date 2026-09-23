namespace RIoT2.Core.Models.Matter
{
    /// <summary>
    /// The unit conversion the RIoT Control Bridge applies between a RIoT device value and a Matter
    /// cluster attribute value.
    /// </summary>
    /// <remarks>
    /// Every scale is declared in the <em>RIoT to Matter</em> direction, the way a report flows. On the
    /// command path (Matter to RIoT) the bridge applies the inverse, so one scale on a
    /// <see cref="MatterAttributeBinding"/> and its paired <see cref="MatterCommandBinding"/> keeps the
    /// round trip consistent.
    /// </remarks>
    public enum MatterValueScale
    {
        /// <summary>Pass the value through unchanged; it is already in the attribute's native units.</summary>
        None = 0,

        /// <summary>Negate a boolean (e.g. a RIoT "closed" flag driving a Matter "open" state).</summary>
        InvertBoolean = 1,

        /// <summary>0-100 percent to the Level Control 0-254 range.</summary>
        Percent0To100ToLevel0To254 = 2,

        /// <summary>0.0-1.0 fraction to the Level Control 0-254 range.</summary>
        Fraction0To1ToLevel0To254 = 3,

        /// <summary>0-360 degrees to the Color Control hue 0-254 range.</summary>
        Degrees0To360ToHue0To254 = 4,

        /// <summary>0-100 percent to the Color Control saturation 0-254 range.</summary>
        Percent0To100ToSaturation0To254 = 5,

        /// <summary>Colour temperature in kelvin to mireds (1000000 / kelvin).</summary>
        KelvinToMireds = 6,

        /// <summary>Degrees Celsius to the hundredths-of-a-degree Matter unit.</summary>
        CelsiusToHundredths = 7,

        /// <summary>0-100 percent to the hundredths-of-a-percent Matter unit.</summary>
        PercentToHundredths = 8,

        /// <summary>Illuminance in lux to the Illuminance Measurement logarithmic scale.</summary>
        LuxToLogScale = 9
    }
}
