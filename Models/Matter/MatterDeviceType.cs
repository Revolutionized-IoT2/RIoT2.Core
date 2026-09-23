namespace RIoT2.Core.Models.Matter
{
    /// <summary>
    /// The Matter device type a <see cref="MatterEndpointTemplate"/> is exposed as. Each value maps to a
    /// device type in the Matter Device Library, and determines which clusters the RIoT Control Bridge
    /// composes on the bridged endpoint.
    /// </summary>
    /// <remarks>
    /// The numeric values are the Matter device type ids, so a declaration stays readable in the
    /// persisted node configuration JSON and can be cross-checked against the specification.
    /// </remarks>
    public enum MatterDeviceType
    {
        /// <summary>Contact Sensor (0x0015): Identify + Boolean State.</summary>
        ContactSensor = 0x0015,

        /// <summary>On/Off Light (0x0100): Identify + On/Off.</summary>
        OnOffLight = 0x0100,

        /// <summary>Dimmable Light (0x0101): Identify + On/Off + Level Control.</summary>
        DimmableLight = 0x0101,

        /// <summary>Light Sensor (0x0106): Identify + Illuminance Measurement.</summary>
        LightSensor = 0x0106,

        /// <summary>Occupancy Sensor (0x0107): Identify + Occupancy Sensing.</summary>
        OccupancySensor = 0x0107,

        /// <summary>On/Off Plug-in Unit (0x010A): a switchable outlet.</summary>
        OnOffPlugInUnit = 0x010A,

        /// <summary>Dimmable Plug-in Unit (0x010B): a dimmable outlet.</summary>
        DimmablePlugInUnit = 0x010B,

        /// <summary>Color Temperature Light (0x010C): adds Color Control restricted to colour temperature.</summary>
        ColorTemperatureLight = 0x010C,

        /// <summary>Extended Color Light (0x010D): adds Color Control with hue/saturation and colour temperature.</summary>
        ExtendedColorLight = 0x010D,

        /// <summary>Thermostat (0x0301): Identify + Thermostat.</summary>
        Thermostat = 0x0301,

        /// <summary>Temperature Sensor (0x0302): Identify + Temperature Measurement.</summary>
        TemperatureSensor = 0x0302,

        /// <summary>Humidity Sensor (0x0307): Identify + Relative Humidity Measurement.</summary>
        HumiditySensor = 0x0307
    }
}
