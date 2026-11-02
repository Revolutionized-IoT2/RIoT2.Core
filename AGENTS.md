# AGENTS.md — RIoT2.Core

Applies to: this repository. Read the platform guide first:
[.github/AGENTS.md](https://github.com/Revolutionized-IoT2/.github/blob/main/AGENTS.md). It covers the
workspace map, platform-wide rules and the documentation rules. In the local workspace, every
`https://github.com/Revolutionized-IoT2/<Repo>/blob/main/<path>` link is the file
`C:\Src\RIoT2\<Repo>\<path>`; read the local file instead of fetching the URL.

## What this is

The RIoT2 core libraries, released from one tag as nine NuGet packages (GitHub Packages) since
1.13.0 ([ADR 0005](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0005-split-core-packages.md)). Five .NET Standard 2.0 packages under `src/`, each
depending only on the ones below it: `RIoT2.Core.Contracts` (wire models, topics, JSON),
`.Mqtt` (MQTT client and service settings), `.Http`, `.Devices` (the plugin SDK) and `.Node` (the
node runtime). `RIoT2.Core` is a facade over all five that forwards their types, so binaries
built against an older `RIoT2.Core` keep working until Core 2.0 (I45). `RIoT2.Core.Devices.Testing`
and `RIoT2.Core.Hosting` are .NET 10. `RIoT2.Core.Devices.Build` (1.15.0) is an MSBuild task that
packs a plugin's zip on `dotnet publish`. Consumers reference the narrowest **packages** (I28); RIoT2.Tests
is the only project reference.

## Commands

Run from the workspace root (`C:\Src\RIoT2`), in PowerShell:

```powershell
dotnet build .\RIoT2.Core\RIoT2.Core.csproj
dotnet build .\RIoT2.Core\RIoT2.Core.csproj -p:CI=true
dotnet test .\RIoT2.Tests\RIoT2.Tests.csproj    # Core, Orchestrator and InfluxDB tests (project references)
dotnet test .\RIoT2.Tests\RIoT2.Tests.Integration\RIoT2.Tests.Integration.csproj -c Release    # End-to-end: broker, Orchestrator, Node, Devices plugin
```

- To check a consumer against unreleased Core, `dotnet pack` **every** packable project into
  `.\.localfeed`, clear the version from the NuGet cache, and restore the consumer with
  `.localfeed` as an extra source. A local pack is not a release.
- Every push runs `.github/workflows/ci.yml` (build, pack smoke test of all nine packages, secret
  scan). To release, push a git tag `x.y.z`: `release.yml` validates, then packs and pushes all
  nine with that version ([CI guide](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/ci.md)). Core's tests live in RIoT2.Tests.
- Shared build settings are in `Directory.Build.props` and `.editorconfig`; package versions are
  centralized in `Directory.Packages.props`. `-p:CI=true` enables CI warning treatment locally.

## Layout

| Path | Contents |
|---|---|
| `src/Contracts/` | `RIoT2.Core.Contracts` (Newtonsoft.Json only until I45): `Constants` (topics, URLs), `Enums` (numbers are wire format), `Models/`, message and template interfaces, `Json` with `RIoT2JsonOptions` and its converters (`[Obsolete]` Newtonsoft parts in `JsonNewtonsoft.cs`), `ConfigurationHash`, `MeterNames`, `Schema/ContractSchemas` (JSON Schema export) |
| `src/Mqtt/` | `RIoT2.Core.Mqtt`: `MqttClient` (and the `RIoT2.Mqtt` meter), `MqttEventArgs`, `LogScope`, and `Configuration/`: `SettingsReader`, `MqttOptions`, `LoggingOptions`, `TelemetryOptions` |
| `src/Http/` | `RIoT2.Core.Http`: `Web` |
| `src/Devices/` | `RIoT2.Core.Devices`, the plugin SDK: `PluginBase`, `DeviceBase`, `AsyncDeviceBase`, device and service interfaces, `PluginHostContract`, `DeviceTemplates` |
| `src/Node/` | `RIoT2.Core.Node`, the node runtime: `NodeMqttService`, `DeviceServiceBase`, `NodeConfigurationServiceBase`, `DeviceSchedulerService` (Quartz), `NodeMetrics`, `CorrelationCache` |
| `RIoT2.Core.csproj`, `Facade/` | The facade: `TypeForwards.cs` (every public type of the packages) and the `[Obsolete]` orchestrator-only and unused types, removed in I45 |
| `src/Devices.Build/` | `RIoT2.Core.Devices.Build`: `build/*.targets` (`RIoT2PluginPack`, after `Publish`), the `PackPlugin` task and `PluginPackager` |
| `Testing/` | `RIoT2.Core.Devices.Testing` (.NET 10): `PluginTestHost`, `PluginConformance`, `DeviceConfigurationBuilder` |
| `Hosting/` | `RIoT2.Core.Hosting` (.NET 10, Serilog, OpenTelemetry): `AddRIoT2Logging()`, `AddRIoT2Telemetry()` |
| `Directory.Build.repo.props` | The one version of every package |
| `contracts/golden/` | Golden files for every message, response and stored format; read by RIoT2.Tests, Elsa, UI and firmware tests |
| `contracts/schemas/`, `tools/SchemaExport/` | The contract JSON Schema, written by the tool on every build; the UI generates its models from it |

## Contracts implemented here

Core is the code side of these hub documents. Change both together:

- [mqtt-topics.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/mqtt-topics.md): `src/Contracts/Constants.cs`, `src/Contracts/Contract.cs`, `src/Contracts/Models/Report.cs`, `Command.cs`, `CommandResult.cs`, `ConfigurationCommand.cs`, `NodeOnlineMessage.cs`, `NodeStatusMessage.cs`, `src/Mqtt/Utils/MqttClient.cs`, `src/Node/Services/NodeMqttService.cs`
- [configuration.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/configuration.md): `src/Contracts/Models/NodeDeviceConfiguration.cs`, `DeviceConfiguration.cs`, templates, `src/Node/Abstracts/NodeConfigurationServiceBase.cs`
- [http-api.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/http-api.md): the shared URLs in `src/Contracts/Constants.cs`

The firmware has its own copy of the topics and models in `RIoT2.Ard.Shared`. Contract changes
must stay compatible with it.

## Rules

- Keep the packages under `src/` on .NET Standard 2.0. A new type goes into the lowest package
  whose dependencies it needs; `Contracts` takes no dependency beyond the JSON libraries, and
  `Devices` none on MQTTnet, Quartz or Hosting (`CorePackageSplitTests` in RIoT2.Tests checks the
  layers). Add every new public type to `Facade/TypeForwards.cs`, and declare a PackageReference for
  every assembly a package references outside .NET 10 (1.14.1 fix); the same tests check both.
- Public API changes must be **additive**. Every consumer compiles against a pinned package
  version, and device plugins run inside the Node's Core version.
- Keep `PackageReference` items versionless; edit `Directory.Packages.props` for package versions.
- Serialize wire JSON with `Json.Serialize` / `Json.SerializeIgnoreNulls` (camelCase). Create
  messages with the `Create(string json)` factories.
- The files in `contracts/golden/` define the formats. A change to `Models/`, `ValueModel` or
  `src/Contracts/Utils/Json.cs` that alters them must regenerate them (RIoT2.Tests with `RIOT2_UPDATE_GOLDEN=1`),
  and every consumer in [contracts/golden/README.md](contracts/golden/README.md) must still pass.
  `contracts/schemas/` is generated: commit what the build writes (CI fails otherwise), and new
  root types go into `ContractSchemas.RootTypes`. Never commit a stored sample before replacing its credentials, tokens, IP and MAC addresses.
- `Json` writes exactly what Newtonsoft wrote (the configuration hashes are computed over it);
  `JsonCompatibilityTests` in RIoT2.Tests compares the two. Change `RIoT2JsonOptions` or a converter
  only with a golden-file update and a new hash for every node configuration.
- A new optional property on a model must not change existing output: make it nullable and give
  it both `[System.Text.Json.Serialization.JsonIgnore(Condition = WhenWritingNull)]` and
  `[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]` (for the `[Obsolete]` Newtonsoft
  helpers, until I45), and add it after the existing properties. Don't use `JsonProperty(Order)`: one
  ordered property reorders the whole object. `GoldenAdditiveContractTests` in RIoT2.Tests checks
  both serializers.
- New golden message files must not start with `command-` or `report-`: the InfluxDB tests and the
  UI read every file with those prefixes as a `Command` or `Report`.
- `contractVersion` stays 1 (`Contract.Version`) until a breaking change, which needs an ADR.
- Don't widen `RIoT2SerializationBinder` in `src/Contracts/Utils/JsonNewtonsoft.cs`: it blocks deserialization
  gadgets in the `[Obsolete]` `*AutoTypeNameHandling` helpers, which only old plugins still call.
- A type the orchestrator stores implements `IStoredObject`; its storage format is pinned by the
  `stored-saved/` golden files.
- `NodeConfigurationServiceBase` must keep extracting plugin zips only inside `Plugins/`
  (zip-slip guard) and sanitizing package file names.
- New device code derives from `AsyncDeviceBase` and overrides the async methods. Don't add
  `async void` lifecycle methods or blocking waits.
- Plugins derive from `PluginBase`. Keep `PluginHostContract.SharedAssemblyPrefixes` the only list
  of assemblies the node shares with plugins; the node's `PluginLoadContext` reads it, and
  `RIoT2.Core.Devices.Build` compiles the same file (internal there, `DEVICES_BUILD_TASK`) to leave
  them out of the zip, so keep that file dependency-free. A change to
  `PluginConformance` changes what every plugin repository's CI accepts: say so in the CHANGELOG.
- `DeviceBase.ParameterRead` is the internal hook the test kit uses to record parameter reads
  (`InternalsVisibleTo RIoT2.Core.Devices.Testing`). Don't make it public.
- The internal rule engine and NCalc are gone. Don't bring them back ([ADR 0003](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0003-elsa-sole-automation-engine.md)).
- Record every released change in `CHANGELOG.md` under the version being tagged.

## Pitfalls

- `Json.Serialize` also camel-cases **dictionary keys**, so `deviceParameters` keys arrive
  camel-cased, while `DeviceBase.GetConfiguration<T>(key)` is case-sensitive. Use camelCase
  parameter keys (contract divergence C1).
- `MqttClient` uses QoS 1, so receivers must tolerate a duplicate; port `RIOT2_MQTT_PORT` (1883),
  no TLS yet (I34, I51). Only `NodeMqttService` retains its last will and presence. Publishing
  before a start throws.
- Don't expose MQTTnet types from Core's public API: `MqttQos`, `MqttSubscription` and
  `MqttEventArgs` stand in for them, so a later MQTTnet 5 migration only touches `MqttClient`
  ([M10](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m10-mqtt-client-robustness.md)).
- MQTT receive callbacks are serialized (one worker per client; receiving pauses at 1,000
  waiting). Don't await device I/O inside them: a configuration must be able to cancel a stalled command. `NodeMqttService` tracks at most 64
  commands and awaits them at shutdown.
- A command with a `correlationId` gets exactly one `CommandResult` from `NodeMqttService`, except
  when shutdown cancels it (none; the id is forgotten so the redelivery runs). A redelivery never
  runs: `duplicate` after `executed`, else the first `failed`/`unknownCommand` again. Any other
  cancellation is `failed`. Answer `rejected`, and forget the id, when the command didn't run and
  may run later (`DeviceNotReadyException`, a configuration being applied, the 64-command limit).
  Throw `UnknownCommandException` for a command no device owns (design 7.1).
- Desired state (design 7.2, 1.8.0): `NodeConfigurationServiceBase.LoadDeviceConfiguration` skips
  a command whose `hash` is applied with every device running, or being applied; a failed fetch
  keeps the current configuration and reports `failed` (never set `DeviceConfiguration` to `null`).
  The host reports `Applied`/`Failed`/`PendingRestart` on `ConfigurationStatus` and calls
  `SaveAppliedConfiguration`, or the status stays `applying`. `NodeMqttService` advertises `desired-state/1` only for a configuration
  service that implements `INodeConfigurationStatusSource`. The device diff key must keep ignoring
  the entry's `hash`, or the first hashed configuration restarts every device.
- `NodeMqttService` publishes reports through a bounded queue (1,000, oldest dropped) and one
  publisher, in the order the devices raised them. Device events are synchronous: never handle one
  with an `async void` method; hand the work to a queue or a tracked task.
- `ValueModel` keeps JSON-looking strings and large integers as they are. Path updates throw
  `ArgumentException` when the parent is missing.
- Call `MqttClient.SetLastWill` before `Start`; `StopAsync` publishes the same message.
- `DeviceServiceBase.ReconfigureChangedDevicesAsync` compares each device's entry, serialized with
  `Json.Serialize`, with the one it was last initialized with. Keep that serialization
  deterministic: a field that changes on every download would restart the device every time.
- `<Version>` in `Directory.Build.repo.props` is the latest CHANGELOG heading until the first change
  after a release, then the next release. It must be at least what consumers pin: RIoT2.Tests
  references these projects and their packages at once, and NuGet picks the higher version.
- RIoT2.Tests' solution must list every project of `RIoT2.Core.sln`: a referenced project missing
  from the solution builds in Debug, and the end-to-end tests refuse a Debug `RIoT2.Core.Node`.
- The plugin pack task runs inside MSBuild, also on .NET Framework in Visual Studio: keep
  `src/Devices.Build/` on .NET Standard 2.0 with no run-time dependencies (no System.Text.Json),
  compiled against MSBuild 17.14 with `ExcludeAssets="runtime"`. The Node's
  `PluginCompatibilityTests` load a zip it packed.
- Keep Serilog and the ASP.NET Core framework out of the packages under `src/`: plugins reference them.
  Service-only hosting code goes in `Hosting/`, and so does OpenTelemetry: Core instruments use
  `System.Diagnostics.Metrics` only. Use `LogScope` names for ids in log scopes, so the text format shows them.
- A new, renamed or removed `riot2.*` instrument changes the
  [metrics contract](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/metrics.md) and the Grafana dashboard in the hub's `deploy/`;
  RIoT2.Tests `MetricsTests` pins the names. `riot2.node.device_restarts` must stay at zero for an
  unchanged configuration.
- Services read settings only through an options type built with `SettingsReader` (plan M4); no
  service calls `Environment.GetEnvironmentVariable`. Messages keep the format
  `Invalid {component} configuration: …`, which the services' tests pin.

## Related work

- [M1](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m01-split-core-packages.md): the package split (steps 1–3 in I27, 4–5 in I28; removing the facade is I45).
- [M2](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m02-system-text-json-persistence.md): one JSON stack.
- [M10](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m10-mqtt-client-robustness.md): MQTT client robustness (TLS: I34, I51).
- [M11](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m11-async-cleanup.md): remaining blocking code.
- [M12](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m12-device-plugin-sdk.md) ([ADR 0014](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0014-device-plugin-sdk.md)): the `RIoT2.Core.Devices*` plugin SDK packages.
- Backlog items 3, 4, 11, 12 and 23 in [open-issues.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/backlog/open-issues.md).
