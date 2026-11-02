# Matter device declarations

Applies to: device plugins (`RIoT2.Core.Devices`). A device is bridged to a Matter ecosystem (for example Google Home) when it implements
`IMatterDevice` as well as `IDeviceWithConfiguration`, and returns one `MatterEndpointTemplate`
per endpoint: the Matter device type, and how its templates map onto cluster attributes:

```csharp
public IEnumerable<MatterEndpointTemplate> GetMatterEndpoints(DeviceConfiguration configuration)
{
    var report = configuration.ReportTemplates[0];
    var command = configuration.CommandTemplates[0];

    yield return new MatterEndpointTemplate
    {
        Id = $"{configuration.Id}:lamp",
        Name = "Living Room Lamp",
        DeviceType = MatterDeviceType.DimmableLight,
        Attributes = { new MatterAttributeBinding { Attribute = MatterAttribute.OnOff, ReportTemplateId = report.Id, ValuePath = "on" } },
        Commands = { new MatterCommandBinding { Attribute = MatterAttribute.OnOff, CommandTemplateId = command.Id, ValuePath = "on" } }
    };
}
```

Build the declaration from the `DeviceConfiguration` passed in, so its template ids are the
persisted ones. A binding's `Scale` (for example `Percent0To100ToLevel0To254`) is declared in the
RIoT-to-Matter direction; the bridge inverts it for commands. The templates reach the
orchestrator's bridge on `DeviceConfiguration.MatterEndpoints`; Core has no Matter protocol code.
