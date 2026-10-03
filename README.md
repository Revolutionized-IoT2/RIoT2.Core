# RIoT2.Core

Shared library for the [RIoT2](https://github.com/Revolutionized-IoT2) platform. It contains the
wire models and MQTT topics, the MQTT client, the device and plugin base classes, and the node
runtime services used by every .NET component.

- Type: class library, NuGet package `RIoT2.Core` on GitHub Packages
- Target framework: .NET Standard 2.0
- Root namespace: `RIoT2.Core`

How Core fits into the platform: [architecture overview](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/architecture/overview.md).

## Contents

| Path | Contents |
| --- | --- |
| `Constants.cs` | MQTT topic templates, shared API URLs, and helpers to build and parse topics |
| `Enums.cs` | Shared enumerations (`ValueType`, `MqttTopic`, `NodeType`, `DeviceState`, …) |
| `Models/` | Messages (`Report`, `Command`, `NodeOnlineMessage`), `ValueModel`, configuration and template models |
| `Models/Matter/` | Matter endpoint declarations used by `IMatterDevice` |
| `Abstracts/` | Base classes: `DeviceBase`, `AsyncDeviceBase`, `DeviceServiceBase`, `NodeConfigurationServiceBase` |
| `Interfaces/` | Device, message, template and service contracts |
| `Services/` | `NodeMqttService`, `ReportService`, `DeviceSchedulerService`, `CodeProviderService` |
| `Utils/` | `Json`, `MqttClient`, `Web` helpers |

The topics and payloads that these types implement are specified in the platform contracts:

- [MQTT topics and payloads](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/mqtt-topics.md)
- [Node configuration, templates and plugins](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/configuration.md)

## Using Core

### Messages and values

- `Report` and `Command` are the MQTT payloads. Create them with `Report.Create(json)` /
  `Command.Create(json)`, and serialize with `Json.SerializeIgnoreNulls`. The JSON is
  camelCase.
- `ValueModel` wraps any JSON value: a primitive, an array or an object. It reports its
  `ValueType`, supports path access such as `items[0].value`, and keeps large integers and
  JSON-looking strings as they are.
- Build topic strings with `Constants.Get(id, MqttTopic.Report)`, and read the id back with
  `Constants.GetTopicId`.

### Writing devices

- Device plugins implement `IDevicePlugin`, and their devices derive from `AsyncDeviceBase`.
  Override its async start, stop, command and refresh methods.
- `DeviceServiceBase` runs one serialized operation queue per device. When the configuration
  is replaced, the old work is cancelled and awaited before the new devices start.
- Existing synchronous devices (`DeviceBase`) still work. Their calls can't be interrupted,
  though, so long-running devices should move to `AsyncDeviceBase`.
- Read device settings with `GetConfiguration<T>("key")`. Keys are case-sensitive, and the
  configuration download camel-cases them, so use camelCase keys.

## Matter device declarations

A device plugin opts in to being bridged to a Matter ecosystem (for example Google Home) by
implementing `IMatterDevice` in addition to `IDeviceWithConfiguration`. The device returns one
`MatterEndpointTemplate` per endpoint it wants exposed, describing the Matter device type and how its
report and command templates map onto Matter cluster attributes:

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
        Attributes =
        {
            new MatterAttributeBinding { Attribute = MatterAttribute.OnOff, ReportTemplateId = report.Id, ValuePath = "on" },
            new MatterAttributeBinding { Attribute = MatterAttribute.CurrentLevel, ReportTemplateId = report.Id, ValuePath = "brightness", Scale = MatterValueScale.Percent0To100ToLevel0To254 }
        },
        Commands =
        {
            new MatterCommandBinding { Attribute = MatterAttribute.OnOff, CommandTemplateId = command.Id, ValuePath = "on" },
            new MatterCommandBinding { Attribute = MatterAttribute.CurrentLevel, CommandTemplateId = command.Id, ValuePath = "brightness", Scale = MatterValueScale.Percent0To100ToLevel0To254 }
        }
    };
}
```

The declaration must be built against the `DeviceConfiguration` instance passed in, because devices
that mint template ids per call would otherwise reference ids that are never persisted. Scales are
declared in the RIoT-to-Matter direction; the bridge applies the inverse on the command path.

The templates travel to the orchestrator on `DeviceConfiguration.MatterEndpoints`, through the
existing configuration-template path, and the orchestrator's Matter bridge turns them into bridged
endpoints. `RIoT2.Core` itself contains no Matter protocol code.

## Dependencies

- `Newtonsoft.Json` (wire JSON, with a restricted type binder) and `System.Text.Json`
- `MQTTnet` and `MQTTnet.Extensions.ManagedClient`
- `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Logging`
- `Quartz` (device refresh scheduling)

Automation is handled by [RIoT2.Elsa](https://github.com/Revolutionized-IoT2/RIoT2.Elsa), not
by Core.

## Build and test

From the workspace root (`C:\Src\RIoT2`):

```powershell
dotnet build .\RIoT2.Core\RIoT2.Core.csproj
dotnet test .\RIoT2.Tests\RIoT2.Tests.csproj
```

[RIoT2.Tests](https://github.com/Revolutionized-IoT2/RIoT2.Tests) references Core,
RIoT2.Net.Orchestrator and RIoT2.Connector.InfluxDB as projects, so those repositories must be
checked out next to this one.

## Versions and releases

- Release notes are in [CHANGELOG.md](CHANGELOG.md).
- To release, push a tag `x.y.z`. CI publishes the package to GitHub Packages.
- Keep the public API additive. Consumers pin package versions, and device plugins run inside the
  Node's Core version, so the Node image and the plugin packages must be released together.

## Contributing

- Instructions for AI coding agents: [AGENTS.md](AGENTS.md).
- Platform documentation: [.github/docs](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/README.md).

## License

See [LICENSE](LICENSE).