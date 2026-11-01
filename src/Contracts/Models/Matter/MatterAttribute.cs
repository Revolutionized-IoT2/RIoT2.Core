namespace RIoT2.Core.Models.Matter
{
    /// <summary>
    /// A cluster attribute on a bridged Matter endpoint that a RIoT report can drive (see
    /// <see cref="MatterAttributeBinding"/>) or that can drive a RIoT command (see
    /// <see cref="MatterCommandBinding"/>).
    /// </summary>
    /// <remarks>
    /// Only attributes valid for the endpoint's <see cref="MatterDeviceType"/> are composed; a binding
    /// naming an attribute the device type does not host is ignored by the bridge.
    /// </remarks>
    public enum MatterAttribute
    {
        /// <summary>On/Off cluster (0x0006), OnOff. A boolean.</summary>
        OnOff = 0,

        /// <summary>Level Control cluster (0x0008), CurrentLevel. 0-254.</summary>
        CurrentLevel = 1,

        /// <summary>Color Control cluster (0x0300), CurrentHue. 0-254.</summary>
        CurrentHue = 2,

        /// <summary>Color Control cluster (0x0300), CurrentSaturation. 0-254.</summary>
        CurrentSaturation = 3,

        /// <summary>Color Control cluster (0x0300), ColorTemperatureMireds. In mireds (1000000 / kelvin).</summary>
        ColorTemperatureMireds = 4,

        /// <summary>Temperature Measurement cluster (0x0402), MeasuredValue. In 0.01 Celsius.</summary>
        TemperatureMeasuredValue = 5,

        /// <summary>Relative Humidity Measurement cluster (0x0405), MeasuredValue. In 0.01 percent.</summary>
        HumidityMeasuredValue = 6,

        /// <summary>Illuminance Measurement cluster (0x0400), MeasuredValue. On the spec's logarithmic scale.</summary>
        IlluminanceMeasuredValue = 7,

        /// <summary>Occupancy Sensing cluster (0x0406), Occupancy. A boolean (bit 0 of the bitmap).</summary>
        Occupancy = 8,

        /// <summary>Boolean State cluster (0x0045), StateValue. A boolean.</summary>
        BooleanStateValue = 9,

        /// <summary>Thermostat cluster (0x0201), LocalTemperature. In 0.01 Celsius.</summary>
        LocalTemperature = 10,

        /// <summary>Thermostat cluster (0x0201), OccupiedHeatingSetpoint. In 0.01 Celsius.</summary>
        OccupiedHeatingSetpoint = 11,

        /// <summary>Thermostat cluster (0x0201), OccupiedCoolingSetpoint. In 0.01 Celsius.</summary>
        OccupiedCoolingSetpoint = 12,

        /// <summary>Thermostat cluster (0x0201), SystemMode. 0 = Off, 1 = Auto, 3 = Cool, 4 = Heat.</summary>
        ThermostatSystemMode = 13
    }
}
