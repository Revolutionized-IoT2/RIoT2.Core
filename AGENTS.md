# AGENTS.md — RIoT2.Core

Applies to: this repository. Read the platform guide first:
[.github/AGENTS.md](https://github.com/Revolutionized-IoT2/.github/blob/main/AGENTS.md). It covers the
workspace map, platform-wide rules and the documentation rules. In the local workspace, every
`https://github.com/Revolutionized-IoT2/<Repo>/blob/main/<path>` link is the file
`C:\Src\RIoT2\<Repo>\<path>`; read the local file instead of fetching the URL.

## What this is

A .NET Standard 2.0 class library, shipped as the NuGet package `RIoT2.Core` (GitHub Packages). It
holds the shared wire models, MQTT topics, the MQTT client, the device and plugin base classes,
and the node runtime services. Every .NET component references it as a **package**. RIoT2.Tests
is the only project reference.

## Commands

Run from the workspace root (`C:\Src\RIoT2`), in PowerShell:

```powershell
dotnet build .\RIoT2.Core\RIoT2.Core.csproj
dotnet build .\RIoT2.Core\RIoT2.Core.csproj -p:CI=true
dotnet test .\RIoT2.Tests\RIoT2.Tests.csproj    # Core, Orchestrator and InfluxDB tests (project references)
dotnet test .\RIoT2.Tests\RIoT2.Tests.Integration\RIoT2.Tests.Integration.csproj -c Release    # End-to-end: broker, Orchestrator, Node, Devices plugin
```

- To check a consumer against unreleased Core, pack Core into the local feed:
  `dotnet pack .\RIoT2.Core\RIoT2.Core.csproj -c Release -p:PackageVersion=<next-version> -o .\.localfeed`.
  Then restore the consumer with `.localfeed` as an extra source. A local pack is not a release.
- Every push runs `.github/workflows/ci.yml` (build, pack smoke test, secret scan). To release,
  push a git tag `x.y.z`: `release.yml` validates, then packs and pushes to GitHub Packages
  ([CI guide](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/ci.md)). Core's tests live in RIoT2.Tests.
- Shared build settings are in `Directory.Build.props` and `.editorconfig`; package versions are
  centralized in `Directory.Packages.props`. `-p:CI=true` enables CI warning treatment locally.

## Layout

| Path | Contents |
|---|---|
| `Constants.cs` | MQTT topic templates and shared API URLs (`Get`, `GetTopicId`) |
| `Enums.cs` | `ValueType`, `MqttTopic`, `NodeType`, `DeviceState`, … (numeric values are wire format) |
| `Models/` | Wire and configuration models: `Report`, `Command`, `ValueModel`, `NodeOnlineMessage`, `DeviceConfiguration`, templates |
| `Models/Matter/` | `MatterEndpointTemplate` and its bindings (declarations only, no protocol code) |
| `Abstracts/` | `DeviceBase`, `AsyncDeviceBase`, `DeviceServiceBase`, `NodeConfigurationServiceBase` |
| `Interfaces/` | Device, message, template and service contracts |
| `Services/` | `NodeMqttService`, `ReportService`, `DeviceSchedulerService`, `CodeProviderService`, … |
| `Utils/` | `Json` (Newtonsoft with a restricted binder), `MqttClient`, `Web` |
| `Configuration/` | `SettingsReader` (reads `RIOT2_*` settings from `IConfiguration`, collects every problem), `MqttOptions`, `ConfigurationValidationException` |
| `contracts/golden/` | Golden files for every message, response and stored format; read by RIoT2.Tests, Elsa, UI and firmware tests |

## Contracts implemented here

Core is the code side of these hub documents. Change both together:

- [mqtt-topics.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/mqtt-topics.md): `Constants.cs`, `Models/Report.cs`, `Command.cs`, `ConfigurationCommand.cs`, `NodeOnlineMessage.cs`, `Utils/MqttClient.cs`, `Services/NodeMqttService.cs`
- [configuration.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/configuration.md): `Models/NodeDeviceConfiguration.cs`, `DeviceConfiguration.cs`, templates, `Abstracts/NodeConfigurationServiceBase.cs`
- [http-api.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/http-api.md): the shared URLs in `Constants.cs`

The firmware has its own copy of the topics and models in `RIoT2.Ard.Shared`. Contract changes
must stay compatible with it.

## Rules

- Keep .NET Standard 2.0. Use no APIs outside that surface.
- Public API changes must be **additive**. Every consumer compiles against a pinned package
  version, and device plugins run inside the Node's Core version.
- Keep `PackageReference` items versionless; edit `Directory.Packages.props` for package versions.
- Serialize wire JSON with `Json.Serialize` / `Json.SerializeIgnoreNulls` (camelCase). Create
  messages with the `Create(string json)` factories.
- The files in `contracts/golden/` define the formats. A change to `Models/`, `ValueModel` or
  `Utils/Json.cs` that alters them must regenerate them (RIoT2.Tests with `RIOT2_UPDATE_GOLDEN=1`),
  and every consumer in [contracts/golden/README.md](contracts/golden/README.md) must still pass.
  Never commit a stored sample before replacing its credentials, tokens, IP and MAC addresses.
- Don't widen `RIoT2SerializationBinder` in `Utils/Json.cs`. `TypeNameHandling` is used on
  configuration posted over REST and MQTT, and the binder is what blocks deserialization gadgets.
- `NodeConfigurationServiceBase` must keep extracting plugin zips only inside `Plugins/`
  (zip-slip guard) and sanitizing package file names.
- New device code derives from `AsyncDeviceBase` and overrides the async methods. Don't add
  `async void` lifecycle methods or blocking waits.
- The internal rule engine, its function catalog, its models and NCalc are gone. Don't bring them
  back ([ADR 0003](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0003-elsa-sole-automation-engine.md)).
- Record every released change in `CHANGELOG.md` under the version being tagged.

## Pitfalls

- `Json.Serialize` also camel-cases **dictionary keys**, so `deviceParameters` keys arrive
  camel-cased, while `DeviceBase.GetConfiguration<T>(key)` is case-sensitive. Use camelCase
  parameter keys (contract divergence C1).
- `MqttClient` publishes at QoS 2 but subscribes at QoS 0, uses port 1883 with no TLS, and by
  default does not retain its last will (divergences D1, D4; plan
  [M10](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m10-mqtt-client-robustness.md)).
- MQTT receive callbacks are serialized. Don't await device I/O inside them: a configuration
  message must be able to cancel a stalled command. `NodeMqttService` starts command dispatch at
  admission, tracks at most 64 commands, and awaits them at shutdown.
- `ValueModel` keeps JSON-looking strings and large integers as they are. Path updates
  (`a.b`, `items[0].value`) throw `ArgumentException` when the parent is missing; they don't
  create it.
- `MqttClient.SetLastWill(topic, payload, retain)` replaces the default last will and must be
  called before `Start`. `Stop` publishes the same message, because the broker sends the last
  will only when the connection drops. The orchestrator uses it for its retained presence.
- `DeviceServiceBase.ReconfigureChangedDevicesAsync` compares each device's entry, serialized with
  `Json.Serialize`, with the one it was last initialized with. Keep that serialization
  deterministic: a field that changes on every download would restart the device every time.
- The current Core package is `1.2.0`, and `<Version>` is `1.2.0`. Raise `<Version>` to the next
  release with the first change after a release.
- Services read settings only through an options type built with `SettingsReader` (plan M4); no
  service calls `Environment.GetEnvironmentVariable`. Messages keep the format
  `Invalid {component} configuration: …`, which the services' tests pin. Tag `1.0.0` failed CI and was never published: don't
  reference it. A tag whose CI run fails publishes nothing.
- `<Version>` in `RIoT2.Core.csproj` is the next release and must be at least the version the
  Orchestrator, Node and InfluxDB connector pin. RIoT2.Tests references this project and those
  packages at once, and NuGet silently picks the higher version, so a lower project version makes
  the tests run against the published package instead of this source.

## Related work

- [M1](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m01-split-core-packages.md): split Core into contract and runtime packages.
- [M2](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m02-system-text-json-persistence.md): one JSON stack.
- [M10](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m10-mqtt-client-robustness.md): MQTT client robustness.
- [M11](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m11-async-cleanup.md): remaining blocking code.
- [M12](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m12-device-plugin-sdk.md) ([ADR 0014](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0014-device-plugin-sdk.md)): the `RIoT2.Core.Devices*` plugin SDK packages.
- Backlog items 3, 4, 11, 12 and 23 in [open-issues.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/backlog/open-issues.md).
