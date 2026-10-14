# RIoT2.Core

Shared library for the [RIoT2](https://github.com/Revolutionized-IoT2) platform. It contains the
wire models and MQTT topics, the MQTT client, the device and plugin base classes, and the node
runtime services used by every .NET component.

- Type: class library, NuGet package `RIoT2.Core` on GitHub Packages. The same tag also publishes
  `RIoT2.Core.Devices.Testing` (from `Testing/`), the test kit for device plugins.
- Target framework: .NET Standard 2.0 (the test kit: .NET 10)
- Versions: [CHANGELOG.md](CHANGELOG.md) (tag `1.0.0` failed CI and was never published)
- Root namespace: `RIoT2.Core`

How Core fits into the platform: [architecture overview](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/architecture/overview.md).

## Contents

| Path | Contents |
| --- | --- |
| `Constants.cs` | MQTT topic templates, shared API URLs, and helpers to build and parse topics |
| `Enums.cs` | Shared enumerations (`ValueType`, `MqttTopic`, `NodeType`, `DeviceState`, …) |
| `Models/` | Messages (`Report`, `Command`, `NodeOnlineMessage`), `ValueModel`, configuration and template models |
| `Models/Matter/` | Matter endpoint declarations used by `IMatterDevice` |
| `Abstracts/` | Base classes: `PluginBase`, `DeviceBase`, `AsyncDeviceBase`, `DeviceServiceBase`, `NodeConfigurationServiceBase` |
| `Plugins/` | What node and plugins share: `PluginHostContract` (shared assemblies), `DeviceTemplates` (the template a device shows in the UI), `[RequiresHardware]` |
| `Testing/` | The `RIoT2.Core.Devices.Testing` package: `PluginTestHost`, `PluginConformance`, `DeviceConfigurationBuilder`. Not part of the `RIoT2.Core` package. |
| `Interfaces/` | Device, message, template and service contracts |
| `Services/` | `NodeMqttService`, `ReportService`, `DeviceSchedulerService`, `CodeProviderService` |
| `Utils/` | `Json`, `MqttClient`, `Web` helpers |
| `Configuration/` | `SettingsReader`, `MqttOptions` and `ConfigurationValidationException`: how every service reads and validates its `RIOT2_*` settings |
| `contracts/golden/` | Golden files: the JSON on MQTT, in orchestrator responses and in `StoredObjects`. Not part of the package. See [contracts/golden/README.md](contracts/golden/README.md). |

The topics and payloads that these types implement are specified in the platform contracts:

- [MQTT topics and payloads](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/mqtt-topics.md)
- [Node configuration, templates and plugins](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/configuration.md)

The [golden files](contracts/golden/README.md) are examples of each format. RIoT2.Tests, Elsa,
the UI and the firmware tests read them, so a format change fails their CI.

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

- A plugin derives from `PluginBase` and registers its services and devices in `Register()` with
  `AddService<TService, TImplementation>()` and `AddDevice<TDevice>()`. Everything is a singleton
  owned by the node, which also adds the plugin's controllers. `IDevicePlugin.Devices` is obsolete
  (Core 1.5.0): the node never read it, and it's removed in the next major release.
- Devices derive from `AsyncDeviceBase`. Override its async start, stop, command and refresh
  methods.
- A device that reads parameters implements `IDeviceWithConfiguration` and declares every key it
  reads in its template, so the UI shows them. `DeviceTemplates.Create(device)` builds what the
  node returns, with a default template for devices that don't implement it.
- Mark a device that needs hardware or a remote service with `[RequiresHardware("reason")]`.
- `DeviceServiceBase` runs one serialized operation queue per device. When a device's
  configuration is replaced, its old work is cancelled and awaited before it starts again.
- `ReconfigureChangedDevicesAsync` (Core 1.1.0) restarts only the devices whose configuration
  entry was added, changed or removed, plus configured devices that are not running. An identical
  configuration, as sent after every reconnect, starts and stops nothing.
  `ReconfigureDevicesAsync` still restarts every device.
- Existing synchronous devices (`DeviceBase`) still work. Their calls can't be interrupted,
  though, so long-running devices should move to `AsyncDeviceBase`.
- Read device settings with `GetConfiguration<T>("key")`. Keys are case-sensitive, and the
  configuration download camel-cases them, so use camelCase keys.

### Testing a plugin

Reference `RIoT2.Core.Devices.Testing` from the plugin's test project and run the conformance
test. Every plugin repository must have it ([ADR 0014](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0014-device-plugin-sdk.md)):

```csharp
[TestMethod]
public void PluginConforms() => PluginConformance.Verify(typeof(Plugin));
```

It loads the plugin as the node does and checks that every device resolves, returns a template
for its own class with a valid Quartz schedule, and reads only the parameters its template
declares when initialized with that template. Devices without `[RequiresHardware]` must also
start and stop. Pass a second argument to replace services, for example hardware transports, with
fakes. `PluginTestHost` hosts a plugin for device tests: it captures reports and sends commands
and refreshes the way the node does; `DeviceConfigurationBuilder` builds configurations.

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

- `Newtonsoft.Json` (wire JSON, with a restricted type binder) and `System.Text.Json` 10.0.12
- `MQTTnet` and `MQTTnet.Extensions.ManagedClient`
- `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging` and `Microsoft.Bcl.AsyncInterfaces` 10.0.12
- `Quartz` (device refresh scheduling)

Automation is handled by [RIoT2.Elsa](https://github.com/Revolutionized-IoT2/RIoT2.Elsa), not
by Core.

## Build and test

From the workspace root (`C:\Src\RIoT2`):

```powershell
dotnet build .\RIoT2.Core\RIoT2.Core.csproj
dotnet test .\RIoT2.Tests\RIoT2.Tests.csproj
dotnet test .\RIoT2.Tests\RIoT2.Tests.Integration\RIoT2.Tests.Integration.csproj -c Release
```

Add `-p:CI=true` to `dotnet build` to reproduce CI analyzer settings locally. Shared build
settings come from `Directory.Build.props`, `.editorconfig` and `Directory.Packages.props`; package
versions are centralized in `Directory.Packages.props`.

[RIoT2.Tests](https://github.com/Revolutionized-IoT2/RIoT2.Tests) references Core,
RIoT2.Net.Orchestrator and RIoT2.Connector.InfluxDB as projects, so those repositories must be
checked out next to this one. Its [end-to-end tests](https://github.com/Revolutionized-IoT2/RIoT2.Tests/blob/main/README.md#end-to-end-tests) also run this source with the
Orchestrator, the Node and the Devices plugin. A change to a message or stored format must update
the golden files:
run the RIoT2.Tests suite with `RIOT2_UPDATE_GOLDEN=1`, review the diff, and commit it here
([contracts/golden/README.md](contracts/golden/README.md)).

## Versions and releases

- Release notes are in [CHANGELOG.md](CHANGELOG.md).
- To release, push a tag `x.y.z`. The release workflow validates, then publishes the package to
  GitHub Packages. A tag whose run fails publishes nothing; fix the build and push the next
  version. Every push also runs CI ([CI guide](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/ci.md)).
- To try an unreleased Core in a consumer, pack it into `C:\Src\RIoT2\.localfeed` and restore with
  that folder as an extra source. A local pack is not a release.
- Keep the public API additive. Consumers pin package versions, and device plugins run inside the
  Node's Core version, so the Node image and the plugin packages must be released together.

## Contributing

- Instructions for AI coding agents: [AGENTS.md](AGENTS.md).
- Platform documentation: [.github/docs](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/README.md).

## License

See [LICENSE](LICENSE).
