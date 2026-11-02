# RIoT2.Core

Shared libraries for the [RIoT2](https://github.com/Revolutionized-IoT2) platform: the wire models
and MQTT topics, the MQTT client, the device plugin SDK and the node runtime used by every .NET
component.

- Type: class libraries, released from one tag as nine NuGet packages on GitHub Packages (since
  1.13.0, [ADR 0005](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0005-split-core-packages.md)):

  | Package | Contents | Depends on |
  | --- | --- | --- |
  | `RIoT2.Core.Contracts` | Wire models, topics, enums, `Json` | System.Text.Json, Newtonsoft.Json (until Core 2.0) |
  | `RIoT2.Core.Mqtt` | `MqttClient`, `LogScope`, the service settings (`SettingsReader`, `MqttOptions`, …) | Contracts, MQTTnet |
  | `RIoT2.Core.Http` | `Web` | Contracts |
  | `RIoT2.Core.Devices` | The plugin SDK: `PluginBase`, `AsyncDeviceBase`, `DeviceBase`, typed options, device interfaces | Contracts, DI and logging abstractions |
  | `RIoT2.Core.Node` | The node runtime: `NodeMqttService`, `DeviceServiceBase`, the scheduler | all of the above, Quartz |
  | `RIoT2.Core` | Facade: references all five and forwards their types, so code built against an older `RIoT2.Core` keeps working; removed in Core 2.0 | all five |
  | `RIoT2.Core.Devices.Testing` | The plugin test kit (.NET 10) | Node |
  | `RIoT2.Core.Devices.Build` | MSBuild target that packs the plugin zip on `dotnet publish` (since 1.15.0) | none |
  | `RIoT2.Core.Hosting` | The services' logging and metrics (.NET 10, Serilog, OpenTelemetry) | Mqtt |

  Reference the narrowest packages (since 1.14.0, [I28](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i28-narrow-references-and-generated-models-m)): a service
  `Contracts` and `Mqtt` (plus `Http` if it calls `Web`), a plugin `Devices` (plus `Http` or `Mqtt`
  if its devices use them), the node `Node`. Only binaries built before 1.13.0 need the facade.

- Target framework: .NET Standard 2.0 (the test kit and Hosting: .NET 10)
- Versions: [CHANGELOG.md](CHANGELOG.md) (tag `1.0.0` failed CI and was never published)
- Root namespace: `RIoT2.Core`

How Core fits into the platform: [architecture overview](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/architecture/overview.md).

## Contents

| Path | Contents |
| --- | --- |
| `src/Contracts/` | `Constants.cs` (topic templates, API URLs, topic helpers), `Contract.cs` (`Contract.Version`, capability names), `Enums.cs`, `Models/` (messages such as `Report`, `Command`, `NodeOnlineMessage`, `CommandResult`, `NodeStatusMessage`; `ValueModel`; configuration, template and Matter models), the message and template interfaces, `Utils/` (`Json`, `RIoT2JsonOptions`, `ConfigurationHash`), `Schema/ContractSchemas.cs` (the JSON Schema export), `Diagnostics/MeterNames.cs` |
| `src/Mqtt/` | `MqttClient`, `LogScope` (the `CorrelationId` and `MessageId` log scopes), and `Configuration/`: how every service reads and validates its `RIOT2_*` settings |
| `src/Http/` | `Web` helpers |
| `src/Devices/` | `PluginBase`, `DeviceBase(<TOptions>)`, `AsyncDeviceBase(<TOptions>)`, the device and plugin interfaces, `Plugins/` (`PluginHostContract`, `DeviceTemplates`, `DeviceOptions`, `[DeviceParameter]`, `[RequiresHardware]`) |
| `src/Node/` | `NodeMqttService`, `DeviceServiceBase`, `NodeConfigurationServiceBase`, `DeviceSchedulerService`, `CommandService`, `ReportService`, `NodeConfigurationStatus`, `NodeMetrics` (`riot2.node.*`) |
| `RIoT2.Core.csproj`, `Facade/` | The `RIoT2.Core` facade: type forwards and the `[Obsolete]` orchestrator-only types |
| `src/Devices.Build/` | `RIoT2.Core.Devices.Build`: the `RIoT2PluginPack` target and its task (`PluginPackager`); compiles `PluginHostContract.cs` from `src/Devices/` |
| `Testing/` | `RIoT2.Core.Devices.Testing`: `PluginTestHost`, `PluginConformance`, `DeviceConfigurationBuilder` |
| `Hosting/` | `RIoT2.Core.Hosting` for the services (not plugins): `AddRIoT2Logging()`, Serilog to the console and `Logs/RIoT2.log`, text or JSON ([design 7.4](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/design/operations.md)), and `AddRIoT2Telemetry()` |
| `contracts/golden/` | Golden files: the JSON on MQTT, in orchestrator responses and in `StoredObjects`. Not part of a package. See [contracts/golden/README.md](contracts/golden/README.md). |
| `contracts/schemas/` | `riot2-contracts.schema.json`: the JSON Schema of the contract types, written by `tools/SchemaExport` on every build. The UI generates its TypeScript models from it. See [contracts/schemas/README.md](contracts/schemas/README.md). |
| `tools/SchemaExport/` | The console tool that writes `contracts/schemas/` (not packed) |

The topics and payloads that these types implement are specified in the platform contracts:

- [MQTT topics and payloads](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/mqtt-topics.md)
- [Node configuration, templates and plugins](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/configuration.md)

The [golden files](contracts/golden/README.md) are examples of each format. RIoT2.Tests, Elsa,
the UI and the firmware tests read them, so a format change fails their CI. The
[JSON Schema](contracts/schemas/README.md) describes the same types; RIoT2.Tests validates every
golden file against it.

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
- Contract additions are optional properties, omitted while unset, so older readers and the
  golden files don't see them (since 1.6.0: `Command.CorrelationId`, `NodeOnlineMessage.Capabilities`,
  the configuration `Revision`/`Hash`, `contractVersion`). Check a node's capabilities with
  `NodeOnlineMessage.HasCapability` before you rely on `CommandResult` or `NodeStatusMessage`.
- Desired state (1.8.0): `ConfigurationHash.Stamp` computes the hashes, `NodeConfigurationStatus`
  holds the node's status, and `NodeConfigurationServiceBase` skips a configuration it already
  runs and saves and loads the applied one.

### Logging in a service

A service calls `builder.AddRIoT2Logging("<service>")` from `RIoT2.Core.Hosting` first in
`Program.cs` (the returned factory logs before the host is built) and reads
`LoggingOptions.Read(settings)` in its options type, so invalid log settings are reported with
the others. Use
`logger.BeginIdScope(LogScope.CorrelationId, id)` (or `MessageId`) around work for one message:
every line logged inside it, by any logger, ends with the id.

### Metrics in a service

A service calls `builder.AddRIoT2Telemetry("<service>", id, MeterNames.<Service>)` after the logging, and
reads `TelemetryOptions.Read(settings)` in its options type. Nothing is exported until
`OTEL_EXPORTER_OTLP_ENDPOINT` is set; then the service's meter, `RIoT2.Mqtt` (`riot2.mqtt.connected`,
`reconnects`, `dropped`), ASP.NET Core and the runtime go over OTLP, and with `RIOT2_TRACING=true` also
traces. Instruments cost nothing while no one listens. Every `riot2.*` instrument is in the platform's
[metrics contract](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/metrics.md).

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
- Mark a device needing hardware or a remote service with `[RequiresHardware("reason")]`.
- `DeviceServiceBase` runs one serialized operation queue per device. When a device's
  configuration is replaced, its old work is cancelled and awaited before it starts again.
- `ReconfigureChangedDevicesAsync` (Core 1.1.0) restarts only the devices whose configuration
  entry changed, plus configured devices that are not running.
- Synchronous devices (`DeviceBase`) work, but their calls can't be interrupted.
- Declare parameters in an options class and derive from `DeviceBase<TOptions>` or
  `AsyncDeviceBase<TOptions>` (1.16.0): mark each property `[DeviceParameter("key", Required,
  Secret, Default, Description)]` and read `Options` in `ConfigureDevice`. The base class binds
  and validates them first (a problem puts the device into `Error`), and the node adds
  `parameterDefinitions` and the keys to the template: the UI shows typed fields and masks
  secrets. Keys match case-insensitively, also in `GetConfiguration<T>("key")`.

### Testing a plugin

Reference `RIoT2.Core.Devices.Testing` from the plugin's test project and run the conformance
test. Every plugin repository must have it ([ADR 0014](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0014-device-plugin-sdk.md)):

```csharp
[TestMethod]
public void PluginConforms() => PluginConformance.Verify(typeof(Plugin));
```

It loads the plugin as the node does and checks that every device resolves, returns a template
for its own class with a valid Quartz schedule, and reads only parameters its template declares. Devices without `[RequiresHardware]` must also
start and stop. Pass a second argument to replace services, for example hardware transports, with
fakes. `PluginTestHost` hosts a plugin for device tests: it captures reports and sends commands
and refreshes the way the node does; `DeviceConfigurationBuilder` builds configurations.

### Packing a plugin

Reference `RIoT2.Core.Devices.Build` with `PrivateAssets="all"` and set `RIoT2PluginName`. Then
`dotnet publish -p:Version=x.y.z` also writes `bin/plugin/<AssemblyName>_x.y.z.zip`, the zip the
node installs, and its `.sha256` sidecar (`sha256sum` format). The zip holds the plugin's top-level
assemblies and native libraries without the ones the node shares (`PluginHostContract`) or ships
(`RIoT2PluginExclude`), plus `PluginManifest.json` with `coreVersion`, the `RIoT2.Core.Devices`
version the plugin uses. `plugin-release.yml` only checks and uploads it. Other properties:
`RIoT2PluginPackage`, `RIoT2PluginOutputDir`, `RIoT2PluginCoreVersion`, `RIoT2PluginPack=false`
(see `build/RIoT2.Core.Devices.Build.targets`).

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
        Attributes = { new MatterAttributeBinding { Attribute = MatterAttribute.OnOff, ReportTemplateId = report.Id, ValuePath = "on" } },
        Commands = { new MatterCommandBinding { Attribute = MatterAttribute.OnOff, CommandTemplateId = command.Id, ValuePath = "on" } }
    };
}
```

The declaration must be built against the `DeviceConfiguration` instance passed in, because devices
that mint template ids per call would otherwise reference ids that are never persisted. A binding's
`Scale` (for example `Percent0To100ToLevel0To254` for `CurrentLevel`) is declared in the
RIoT-to-Matter direction; the bridge applies the inverse on the command path.

The templates reach the orchestrator's Matter bridge on `DeviceConfiguration.MatterEndpoints`.
`RIoT2.Core` itself contains no Matter protocol code.

## Dependencies

- Contracts: `System.Text.Json` 10.0.12 (wire JSON, `RIoT2JsonOptions`) and `Newtonsoft.Json`
  (only the `[Obsolete]` helpers and attributes, with a restricted type binder; removed in Core 2.0)
- Mqtt: `MQTTnet` and `MQTTnet.Extensions.ManagedClient`, the configuration and logging abstractions
- Devices: the dependency injection and logging abstractions, `Microsoft.Bcl.AsyncInterfaces`
- Node: `Quartz` (device refresh scheduling), the hosting abstractions
- `RIoT2.Core.Hosting` only: `Serilog.AspNetCore` 10.0.0, `Serilog.Sinks.File` 7.0.0, OpenTelemetry
  1.20.1 and the ASP.NET Core shared framework
- `RIoT2.Core.Devices.Build`: none at run time (compiled against MSBuild 17.14)

Automation is handled by [RIoT2.Elsa](https://github.com/Revolutionized-IoT2/RIoT2.Elsa), not
by Core.

## Build and test

From the workspace root (`C:\Src\RIoT2`):

```powershell
dotnet build .\RIoT2.Core\RIoT2.Core.sln
dotnet test .\RIoT2.Tests\RIoT2.Tests.csproj
dotnet test .\RIoT2.Tests\RIoT2.Tests.Integration\RIoT2.Tests.Integration.csproj -c Release
```

Building the solution rewrites `contracts/schemas/`; commit the result with the change (CI fails
when the build changes it). `-p:CI=true` reproduces CI's analyzer settings. Shared settings are in
`Directory.Build.props` and `.editorconfig`, package versions in `Directory.Packages.props`.

[RIoT2.Tests](https://github.com/Revolutionized-IoT2/RIoT2.Tests) references Core, the
Orchestrator and the InfluxDB connector as projects, so check them out next to this one; its
end-to-end tests also run the Node and the Devices plugin. A change to a message or stored format
must update the golden files: run RIoT2.Tests with `RIOT2_UPDATE_GOLDEN=1`, review the diff and
commit it here ([contracts/golden/README.md](contracts/golden/README.md)).

## Versions and releases

- Release notes are in [CHANGELOG.md](CHANGELOG.md).
- To release, push a tag `x.y.z`: the release workflow validates, then publishes all nine packages
  with that version (kept in `Directory.Build.repo.props`). A failed run publishes nothing; fix the
  build and push the next version. Every push also runs CI ([CI guide](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/ci.md)).
- To try an unreleased Core, pack every project into `C:\Src\RIoT2\.localfeed` and restore the
  consumer with it as an extra source.
- Keep the public API additive. Consumers pin package versions, and device plugins run inside the
  Node's Core version: a plugin built against a newer Core than the node's doesn't load, so release
  the Node image first, then the plugins.

## Contributing

- Instructions for AI coding agents: [AGENTS.md](AGENTS.md).
- Platform documentation: [.github/docs](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/README.md).

## License

See [LICENSE](LICENSE).
