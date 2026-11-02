# Changelog

All notable changes to `RIoT2.Core`. A version is released by pushing a git tag. CI then publishes
the NuGet package to GitHub Packages. Versions without a tag were only packed locally (into
`.localfeed`) and are not on GitHub Packages.

## [Unreleased]

## [1.16.1] - 2026-11-02

- Fixed: `AsyncDeviceBase` treated every `OperationCanceledException` as a cancellation. An
  HttpClient timeout during a start left the device `Stopped` without a reason, and one during a
  refresh didn't mark it `Error`. Only the caller's cancellation stops a device now; any other is a
  failure ([I31](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i31-async-cleanup-part-2-and-threading-analyzers-m)). Ships with Node 1.11.1.
- Build: the threading analyzers ([I31](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i31-async-cleanup-part-2-and-threading-analyzers-m)) run in every project from the shared
  `Directory.Build.props` and `.editorconfig`: a blocking wait, `async void` or an unobserved task
  fails the CI build. No change to the shipped binaries. The synchronous shims of the synchronous contracts
  (`AsyncDeviceBase`, `DeviceServiceBase`, `CommandService`, `DownloadPluginPackage`), `MqttClient.Dispose`
  and the test kit's synchronous `Verify` and `Dispose` keep their waits, each with a justified
  suppression (plan M11 group A). No API change.

## [1.16.0] - 2026-11-02

Typed device options ([I30](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i30-typed-device-options-m), plan M12 step 4). Additive: no golden file
or hash changes. A plugin built on it needs Node 1.11.0, which ships it.

- Added: `DeviceBase<TOptions>` and `AsyncDeviceBase<TOptions>` (`IDeviceWithOptions`). Before
  `ConfigureDevice` they bind `deviceParameters` to `Options`, an instance of the options class
  whose properties carry `[DeviceParameter(key, Required, Secret, Default, Description)]`. An empty
  value takes `Default` or keeps the property's initial value; a missing required or invalid value
  throws `DeviceOptionsException`, so the device is in `Error` with every problem in its state
  message (secret values are never echoed). `DeviceOptions.Describe` and `Bind` do the same for tests.
- Added (contract, optional): `DeviceConfiguration.ParameterDefinitions`, a list of
  `DeviceParameterDefinition` (`key`, `type`, `required`, `secret`, `default`, `description`) with
  `DeviceParameterType` (`Text`, `Number`, `Integer`, `Boolean`), omitted while `null`.
  `DeviceTemplates.Create` fills it for devices with options and adds every missing key with its
  default. New golden file `http/device-templates.json`; the schema has both types.
- Fixed: divergence C1. `GetConfiguration<T>` and binding match parameter keys case-insensitively
  when no key matches exactly, so a key the configuration download camel-cased is still found.

## [1.15.0] - 2026-11-02

Plugin packaging ([I29](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i29-plugin-packaging-s), plan M12 step 3). No API, wire, storage or hash change.

- Added: the `RIoT2.Core.Devices.Build` package, released from the same tag. A plugin references it
  with `PrivateAssets="all"`; `dotnet publish` then runs its `RIoT2PluginPack` target, which writes
  `<RIoT2PluginPackage>_<version>.zip` and a `.sha256` sidecar (`sha256sum` format) to
  `bin/plugin/` (`RIoT2PluginOutputDir`). The zip holds the plugin's top-level assemblies and native
  libraries from the publish output, without the assemblies in
  `PluginHostContract.SharedAssemblyPrefixes` or `RIoT2PluginExclude` (default: the
  `Microsoft.Extensions.*`, MQTTnet, Newtonsoft.Json and Quartz assemblies the node ships), plus
  `PluginManifest.json` (`name`, `date`, `version`, `installedPackageFilename`, `coreVersion`).
  `coreVersion` is the resolved `RIoT2.Core.Devices` version (`RIoT2.Core` for a facade plugin).
  This is what the hub's `plugin-release.yml` did in bash until now; the workflow only checks and
  uploads the result. The task targets .NET Standard 2.0, has no run-time dependencies and runs
  in MSBuild 17.14 or newer.

## [1.14.1] - 2026-11-01

- Fixed: `RIoT2.Core.Mqtt` declares its `Microsoft.Bcl.AsyncInterfaces` dependency. A .NET 10
  service that referenced only `Contracts` and `Mqtt` (Orchestrator 1.13.0, Elsa 1.7.0, connector
  1.6.0) did not get it, because `System.Threading.Channels` brings it only to netstandard2.0
  consumers, and failed at start with `FileNotFoundException`. RIoT2.Tests now checks that every
  package declares what it references outside the shared framework. No API change.

## [1.14.0] - 2026-11-01

Narrow references and generated models ([I28](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i28-narrow-references-and-generated-models-m), plan M1 steps 4–5). Additive: no
wire, storage or hash change.

- Added: `RIoT2.Core.Schema.ContractSchemas` in `RIoT2.Core.Contracts` exports the JSON Schema
  (draft 2020-12) of the contract types with System.Text.Json's `JsonSchemaExporter`: one `$defs`
  entry per type (the MQTT messages, the node configuration with its templates and Matter
  declarations, the template listings, `Variable`, `DashboardConfiguration`, `DeviceStatus` and the
  enums with their names). It describes what `RIoT2JsonOptions.Default` writes; members left out
  while null are optional. See [contracts/schemas/README.md](contracts/schemas/README.md).
- Added: `contracts/schemas/riot2-contracts.schema.json`, written on every build by
  `tools/SchemaExport` (not packed). CI fails when the build changes it. The UI generates its
  TypeScript models from it, and RIoT2.Tests validates every golden file against it.
- Changed: consumers reference the narrowest packages: Elsa and the InfluxDB connector `Contracts`
  and `Mqtt`, the Orchestrator also `Http` (with its own Quartz reference for cron expressions),
  the plugins `Devices` (RIoT2.Net.Devices also `Http` and `Mqtt`), the node `Node`, with the
  facade kept only at run time for plugins built before 1.13.0.
- Note: a plugin built against 1.14.0 needs a node on Core 1.14.0 or later (Node 1.10.0): the
  runtime does not load a newer `RIoT2.Core.*` assembly version than the node ships.

## [1.13.0] - 2026-11-01

Core package split ([I27](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i27-core-package-split-m), plan M1 steps 1–3, [ADR 0005](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0005-split-core-packages.md)).
Source and binary compatible: namespaces are unchanged, and `RIoT2.Core` forwards every moved type,
so services and plugins built against an older `RIoT2.Core` keep working.

- Added: five packages, released from the same tag with the same version as `RIoT2.Core`:
  - `RIoT2.Core.Contracts`: models, topics, enums, the message, template and `IStoredObject`
    interfaces, `Json`, `RIoT2JsonOptions`, `ConfigurationHash`, `MeterNames`. Depends on
    System.Text.Json and, until Core 2.0, Newtonsoft.Json.
  - `RIoT2.Core.Mqtt`: `MqttClient`, `MqttEventArgs`, `LogScope`, and the service settings
    (`SettingsReader`, `MqttOptions`, `LoggingOptions`, `TelemetryOptions`,
    `ConfigurationValidationException`).
  - `RIoT2.Core.Http`: `Web`.
  - `RIoT2.Core.Devices`, the plugin SDK: `PluginBase`, `DeviceBase`, `AsyncDeviceBase`, the device
    and plugin interfaces, `ICommandService`, `IReportService`, `INodeConfigurationService`,
    `PluginHostContract`, `DeviceTemplates`. No MQTTnet, Quartz or Hosting.
  - `RIoT2.Core.Node`, the node runtime: `NodeMqttService`, `DeviceServiceBase`,
    `NodeConfigurationServiceBase`, `DeviceSchedulerService`, `CommandService`, `ReportService`,
    `NodeConfigurationStatus`, `NodeMetrics`, `CorrelationCache`.
- Changed: `RIoT2.Core` is a facade that references the five packages and forwards their types
  (`Facade/TypeForwards.cs`). It now depends on the hosting, logging and configuration
  abstractions instead of `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Logging`.
  `RIoT2.Core.Devices.Testing` depends on `RIoT2.Core.Node`, `RIoT2.Core.Hosting` on
  `RIoT2.Core.Mqtt`.
- Deprecated: the orchestrator-only `IOrchestratorConfigurationService`, `IOrchestratorMqttService`,
  `IMessageStateService` and `MessageStateService` (the orchestrator has its own since 1.12.0), and
  the unused `ICodeProviderService` and `CodeProviderService`. They stay in the facade until Core 2.0.
- Deprecated: `DeviceConfiguration.GetDeviceRefreshSchedule()`. It no longer validates the cron
  expression (the contract package has no Quartz); the node's scheduler does, and skips an invalid one.
- Changed: the sources moved into `src/<Package>/` and the version into `Directory.Build.repo.props`.

## [1.12.0] - 2026-11-01

Typed persistence ([I26](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i26-typed-persistence-m), plan M2 steps 4–5), for Orchestrator 1.11.0. Additive.

- Added: `Interfaces/IStoredObject` (`string Id`), implemented by `Variable`,
  `NodeDeviceConfiguration` and `DashboardConfiguration`: the types the orchestrator stores.
- Added: `ExpandoObjectJsonConverter` in `RIoT2JsonOptions`. An `ExpandoObject` (such as
  `DashboardElement.Properties`) is read as Newtonsoft read it: nested objects are
  `ExpandoObject`s, arrays `List<object>`, integers `long` and other numbers `double`. A string that
  looks like a date stays a string.
- Deprecated: `IStoredObjectService` and `StoredObjectEventHandler` (`dynamic`). Only the
  orchestrator used them, and it has its own typed store; they are removed in Core 2.0 (I45).
- Fixed: `RIoT2JsonOptions` escapes exactly what Newtonsoft escaped (`NewtonsoftJsonEncoder`). Since
  1.11.0 an emoji was written as a `\u` escape pair and DEL as `\u007f`; they are written as they
  are again, and other control characters use lowercase hex, as before 1.11.0. A node
  configuration with an emoji gets its pre-1.11.0 hash back.

## [1.11.0] - 2026-11-01

System.Text.Json on the wire ([I25](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i25-systemtextjson-on-the-wire-m), plan M2 steps 2–3,
[ADR 0015](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0015-system-text-json-typed-persistence.md)).
The JSON is unchanged: every golden file and configuration hash is the same, and a test compares
the output with the old Newtonsoft settings byte for byte.

- Changed: `Json.Serialize`, `SerializeIgnoreNulls`, `Deserialize<T>`, `Deserialize(string, Type)`,
  `ToDictionary(string)`, `ToDictionary(object)`, `GetValue`, `FindValue`, `SetValue`, `IsJson`
  and the `Inject*` helpers (and the `ToJson`, `ToObj`, `ToDict`, `FindValue` and `SetValue`
  extensions) run on System.Text.Json with the new `RIoT2JsonOptions`. They write what Newtonsoft
  wrote (`21.0`, `1.0` for a decimal, camelCase dictionary keys, enums as numbers, interface
  members with their runtime type) and read what it read (case-insensitive names, numbers in
  strings and the reverse, enum names, non-ISO dates, `""` as null for a nullable member, the last
  of duplicate property names). Newtonsoft's `[JsonProperty]` names (camelCased), null handling and
  non-public `[JsonProperty]` members, its member order (fields first) and `[JsonIgnore]` still
  apply.
- Changed, for plugins: an `object` member read from JSON holds a `JsonElement` for an object or
  array (it was a `JObject`/`JArray`; both give their JSON from `ToString()`); integers are still
  `long`, other numbers `double`. Invalid JSON throws `System.Text.Json.JsonException`. `FindValue`
  and `SetValue` support `$`, member names, `['quoted names']` and `[index]`, not wildcards,
  filters or recursive descent. A character outside the Basic Multilingual Plane (an emoji) is
  written as a `\u` escape pair.
- Added: `RIoT2JsonOptions` (`Default`, `IgnoreNulls`, `Create`) and its converters,
  `ValueModelJsonConverter` among them.
- Deprecated (`[Obsolete]`, still Newtonsoft): `DeserializeAutoTypeNameHandling`,
  `SerializeAutoTypeNameHandling` (stored configuration, until typed persistence in I26),
  `DeserializePascal`, `Deserialize(string)`, `ToDictionary(JObject)` and the Newtonsoft
  `ValueModelConverter`. `SerializeAutoTypeNameHandling(obj)` without type names is
  `SerializeIgnoreNulls(obj)`. The Newtonsoft classes (`ValueModelConverter`, `JsonEntity`,
  `RIoT2SerializationBinder`) move to `Utils/JsonNewtonsoft.cs`.
- Changed: `ConfigurationBase` reads the package manifest and `DeviceServiceBase` compares device
  configurations with System.Text.Json.
- Added: golden files `contracts/golden/http/templates-report.json`, `templates-command.json` and
  `templates-variable.json`: the orchestrator's template listings ([I23](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i23-orchestrator-restructure-m)).
  Contract files.

## [1.10.0] - 2026-11-01

Metrics ([I22](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i22-metrics-m), design 7.4 phase 4). Additive.

- Added: `RIoT2.Core.Hosting`'s `AddRIoT2Telemetry(service, id, meters)`: the service's meters,
  `RIoT2.Mqtt`, ASP.NET Core and runtime metrics over OTLP, and traces with `RIOT2_TRACING=true`,
  only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (OpenTelemetry 1.20.1). `Configuration/TelemetryOptions`
  reads and validates both settings.
- Added: `Diagnostics/MeterNames` and the node's instruments in `NodeMetrics`:
  `riot2.node.devices` (by state), `device_restarts` (running devices stopped by a configuration
  change), `commands` (by outcome), `reports.published`, `reports.dropped` and `config_applies`
  (applied, failed or unchanged).
- Added: `MqttClient` measures `riot2.mqtt.connected` and `riot2.mqtt.reconnects` per client, next to
  `riot2.mqtt.dropped`.

## [1.9.0] - 2026-11-01

Consistent logging ([I20](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i20-consistent-logging-s), design 7.4 phase 2). Additive.

- Added: the package `RIoT2.Core.Hosting` (from `Hosting/`, .NET 10, released from the same tag)
  with `AddRIoT2Logging()`: Serilog to the console and `Logs/RIoT2.log` (rolled daily and at 50 MB,
  14 files kept), readable text or compact JSON, `Microsoft.*` and `System.*` from `Warning`, and
  the log scope ids on every line. It returns a logger factory for start-up code whose loggers
  forward to the host's factory once it exists. `RIoT2.Core` itself has no Serilog dependency.
- Added: `Configuration/LoggingOptions` (`RIOT2_LOG_FORMAT` `text`/`json`, `RIOT2_LOG_LEVEL`),
  read by every service's options type, and `Utils/LogScope` (`CorrelationId`, `MessageId`,
  `BeginIdScope`).
- Changed: `NodeMqttService` runs each command in a `CorrelationId` scope, so the device's own log
  lines carry the id.

## [1.8.0] - 2026-10-14

Desired-state configuration on the node ([I16](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i16-desired-state-configuration-for-net-nodes-m), design 7.2 phases 2–3). Additive.

- Added: `ConfigurationHash` (the configuration hashes of design 7.2), `NodeConfigurationStatus`
  and `INodeConfigurationStatusSource`, and `Web.SendAsync`.
- Changed: `NodeConfigurationServiceBase.LoadDeviceConfiguration` doesn't fetch a configuration
  whose `hash` it already runs with every device running, or is applying; it answers with its
  status. It fetches with `If-None-Match`, and a `304` applies the current configuration again
  (starting devices that aren't running).
- Fixed: a failed configuration fetch was ignored silently and left `DeviceConfigurationLoaded`
  `false`. The node now keeps its configuration, reports `failed` with the reason, and throws, so
  the failure is logged.
- Added: `SaveAppliedConfiguration` and `LoadAppliedConfiguration`
  (`Data/configuration.applied.json`, overridable with `AppliedConfigurationPath`), so a node can
  start with its last configuration while the orchestrator is down.
- Changed: `NodeMqttService` retains its presence and last will (divergence D1 fixed), publishes
  the configuration status retained on `riot2/node/{id}/status` on every connect (before its
  presence) and on every change, and advertises `desired-state/1` when its configuration service
  implements `INodeConfigurationStatusSource`, as `NodeConfigurationServiceBase` does.
- Changed: `DeviceServiceBase.ReconfigureChangedDevicesAsync` ignores an entry's `hash`, so the
  first configuration with hashes restarts nothing.

## [1.7.0] - 2026-10-14

Command results on the node ([I15](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i15-command-results-and-api-v2-m), design 7.1 phase 2). Additive.

- Added: `NodeMqttService` answers every command that has a `correlationId` on
  `riot2/node/{id}/command/result`: `executed`, `failed` (the exception message), `unknownCommand`,
  `rejected` (64 commands pending, stopping, device or configuration not ready) or `duplicate`. It
  keeps the last 256 correlation ids and their outcomes for 10 minutes and doesn't run a repeat: a
  repeat of a running command gets no answer of its own, one of an executed command `duplicate`, one
  of a failed command the same failure. A command cancelled because its device was stopped or
  reconfigured is `failed`; one cancelled by the node stopping gets no answer and its id is
  forgotten. A command without a `correlationId` behaves as before. It adds `command-result/1` to the node's online message.
- Added: `CorrelationCache`, `UnknownCommandException` (thrown by `CommandService` and
  `DeviceServiceBase` when no device owns a command) and `DeviceNotReadyException` (thrown by the
  device runtime when a device isn't running or accepting work). Both derive from
  `InvalidOperationException`, as before.

## [1.6.0] - 2026-10-14

Additive contract release ([I14](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i14-additive-contract-release-s); designs 7.1 phase 2 and 7.2 phase 1, ADR 0005).
Nothing uses the new types yet: command results come with I15, desired-state configuration with
I16. Every new optional property is omitted while unset, with Newtonsoft and System.Text.Json, so
existing payloads, responses and stored files are byte-for-byte unchanged and every existing
golden file still matches.

- Added: topics `MqttTopic.CommandResult` (6, `riot2/node/{id}/command/result`) and
  `MqttTopic.NodeStatus` (7, `riot2/node/{id}/status`).
- Added: `CommandResult` with `CommandResultStatus` (`executed`, `failed`, `rejected`,
  `unknownCommand`, `duplicate`), and `NodeStatusMessage` with `NodeConfigurationState`
  (`applied`, `applying`, `failed`, `pendingRestart`) and `FailedDevice`. Both have `Create(json)`
  and `ToJson()`.
- Added: optional `Command.CorrelationId`, `NodeOnlineMessage.Capabilities` (with
  `HasCapability`), `ConfigurationCommand.Revision` and `Hash`, `NodeDeviceConfiguration.Revision`
  and `Hash`, and `DeviceConfiguration.Hash`.
- Added: `Contract.Version` (1) and `Contract.Capabilities` (`command-result/1`,
  `desired-state/1`). Every MQTT payload (`Report`, `Command`, `NodeOnlineMessage`,
  `ConfigurationCommand`) has an optional `contractVersion`; absent means 1. The two new messages
  set it to 1.
- Golden files for the new shapes: `messages/correlated-command.json`, `result-executed.json`,
  `result-failed.json`, `node-status-applied.json`, `node-status-failed.json`,
  `node-online-capabilities.json`, `configuration-command-hash.json` and
  `http/node-configuration-hash.json`.

## [1.5.0] - 2026-10-14

Plugin configuration and conformance ([I10](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-0-1.md#i10-plugin-configuration-and-conformance-m), plans M6 and M12 steps 1–2). Additive:
plugins built against 1.4.0 or earlier keep loading. A plugin built on `PluginBase` needs a node
with Core 1.5.0.

- Added: `PluginBase`, the base class for device plugins. `Register()` calls `AddService` and
  `AddDevice`; every registration is a singleton. Plugins no longer call `AddControllers`, the node
  adds their controllers.
- Added: `PluginHostContract.SharedAssemblyPrefixes` and `IsShared`, the one list of assemblies the
  node shares with plugins (`RIoT2.Core*` and the DI and logging abstractions).
- Added: `DeviceTemplates.Create(device)`, the configuration template the node returns for a
  device, moved from the node. Fixed: the default template of a refreshable device had the
  5-field schedule `0 * * * *`, which Quartz rejects, so the device was never refreshed. It is now
  `0 * * ? * * *` (every minute).
- Added: `[RequiresHardware]` for devices that need hardware or a remote service.
- Added: the `RIoT2.Core.Devices.Testing` package, released with the same version: `PluginTestHost`,
  `PluginConformance` (the conformance test every plugin runs), `DeviceConfigurationBuilder` and
  `TestNodeConfigurationService`.
- Deprecated: `IDevicePlugin.Devices`. The node resolves devices from DI and never read the list;
  it is removed in the next major release.

## [1.4.0] - 2026-10-13

Async cleanup, part 1 ([I09](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-0-1.md#i09-async-cleanup-part-1-s), plan M11). Additive: plugins and services built against 1.3.0 or
earlier keep working.

- Changed: `NodeMqttService` publishes reports through a bounded queue (`ReportQueueCapacity`,
  1,000) and one publisher, so reports are published in the order the devices raised them, and a
  failed publish is logged and doesn't stop the next one. The handler used to be `async void`, one
  continuation per report. When the queue is full the oldest report is dropped, counted
  (`NodeMqttService.DroppedReports`) and logged at most once a minute. `Stop` publishes the reports
  already queued before the client goes offline.
- Deprecated: `INodeConfigurationService.DownloadPluginPackage` and its base implementation block
  the calling thread; use `DownloadPluginPackageAsync`. The node no longer calls them.

## [1.3.0] - 2026-10-13

MQTT client robustness ([I08](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-0-1.md#i08-mqtt-client-robustness-m), plan M10). Additive API: plugins and services built against 1.2.0
or earlier keep working, and the members marked obsolete are removed in I45.

- Changed: `MqttClient` publishes, subscribes and sends its last will at **QoS 1** (divergence D4:
  publishing at QoS 2 while subscribing at QoS 0 meant everything was delivered at QoS 0). A
  receiver can now see a duplicate after a reconnect.
- Changed: messages published while the broker is unreachable wait in a bounded queue
  (`MqttConfiguration.MaxPendingMessages`, default 10,000). When it is full, the oldest message is
  dropped, counted (`MqttClient.DroppedMessages`, metric `riot2.mqtt.dropped`) and logged at most
  once a minute. It used to grow without limit.
- Changed: received messages go through a bounded queue (1,000) to one worker per client, which
  calls every handler in order, each in its own `try/catch`; a failing handler is logged and no
  longer affects the other handlers or the next message. When the queue is full, receiving pauses.
- Changed: `DeviceSchedulerService` rebuilds its Quartz triggers in a background loop instead of
  inside `DevicesUpdated`, which the device service raises while it holds its lifecycle lock; a
  refresh job waiting for that lock could deadlock a reconfiguration. Its trigger event is per
  instance, so two schedulers in one process no longer trigger each other's devices.
- Added `MqttClient(MqttConfiguration, ILogger)`, `StartAsync(subscriptions, cancellationToken)`
  with a `MqttQos` per `MqttSubscription`, `PublishAsync(topic, payload, qos, retain,
  cancellationToken)`, which throws `InvalidOperationException("MQTT client not started")` before a
  start, `StopAsync`, `IAsyncDisposable`, `ConnectionStateChanged` and `DroppedMessages`. `Dispose`
  waits at most 5 s. No MQTTnet type is part of the public API.
- Added `RIOT2_MQTT_PORT` (`MqttOptions.Port`, `MqttConfiguration.Port`, default 1883).
- Added `DeviceSchedulerService.RefreshTriggered`, `TriggerRefresh` and `WhenIdleAsync`.
- Deprecated `MqttClient.Publish` (use `PublishAsync`), and the static
  `DeviceSchedulerService.SchedulerEvent` and `TriggerSchedulerEvent`.

## [1.2.0] - 2026-10-13

Typed configuration ([I07](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-0-1.md#i07-typed-configuration-s)). Additive API; consumers on 1.1.0 need no change.

- Added `RIoT2.Core.Configuration`. `SettingsReader` reads `RIOT2_*` settings from an
  `IConfiguration` (a host's configuration includes the environment variables) and collects every
  missing or invalid one; `ThrowIfInvalid` logs them as critical and throws
  `ConfigurationValidationException` with one message in the same format for every service:
  `Invalid {component} configuration: {problem}; {problem}.` `MqttOptions` maps `RIOT2_MQTT_IP`, `RIOT2_MQTT_USERNAME` and
  `RIOT2_MQTT_PASSWORD`; an unset user or password is `null`, so the client connects anonymously.
- Fixed: `NodeMqttService.Stop` threw `NullReferenceException` when called twice at the same time,
  as a host does when its owner and `Run` both stop it. Only the first call stops now
  ([I06](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-0-1.md#i06-in-process-end-to-end-harness-m), found by the end-to-end tests).
- Fixed: `MqttClient.Stop` threw `MqttClientDisconnectedException` when the connection dropped
  while it published the offline message, and concurrent calls published it twice. Calls are now
  serialized, and a lost connection or a 5 s timeout is ignored, because the broker then publishes
  the last will itself.

- Added `contracts/golden/` (in the repository, not in the package), the golden files ([I05](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-0-1.md#i05-golden-files-and-route-contract-m)): one `Report` and `Command` per
  `ValueType`, the presence, last-will and configuration messages, the orchestrator's HTTP
  responses, and real `StoredObjects` samples with credentials scrubbed, before and after the
  orchestrator saves them. RIoT2.Tests, Elsa, the UI and the firmware tests read them.

## [1.1.0] - 2026-10-12

Restart only changed devices ([I04](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-0-1.md#i04-restart-only-changed-devices-s)).
Additive API; consumers on 1.0.1 need no change.

- Added `DeviceServiceBase.ReconfigureChangedDevicesAsync`. It restarts only the devices whose
  configuration entry was added, changed or removed, plus configured devices that are not running,
  and returns a `DeviceReconfigurationResult`. An identical configuration starts and stops
  nothing, so reconnects and orchestrator restarts no longer restart every device
  ([backlog item 18](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/backlog/open-issues.md)).
  `ReconfigureDevicesAsync` still restarts every device and is now also the baseline for the diff.
- Added `MqttClient.SetLastWill(topic, payload, retain)`. `Stop` publishes the configured message
  on a graceful stop. The default (node presence, not retained) is unchanged.
- `RIoT2.Core.csproj` sets `<Version>` to the next release, so RIoT2.Tests builds against this
  source rather than the published package.
- CI: every push runs `ci.yml` (build, pack smoke test, secret scan), and a tag runs `release.yml`,
  which validates before it publishes. Both call the reusable workflows in the `.github` hub.

## [1.0.1] - 2026-10-03

The .NET 10 baseline release ([M8](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m08-dotnet10-migration.md)).
It contains the 0.1.44 fixes. Tag `1.0.0` has the same code but failed CI (CA1873 under the
newer SDK), so it was never published; use 1.0.1.

- Build: added shared `Directory.Build.props`, `.editorconfig` and central
  `Directory.Packages.props`; CI builds use the .NET 10 SDK and `-p:CI=true` treats warnings as
  errors.
- Build: SourceLink, and the PDB is embedded in the DLL (`DebugType=embedded`).
- Changed package dependencies from 9.0.0 to 10.0.12 for `Microsoft.Extensions.Hosting`,
  `Microsoft.Extensions.Logging`, `Microsoft.Bcl.AsyncInterfaces` and `System.Text.Json`.
- Fixed `DeviceBase` threshold-check error logging so the report id is passed through a template
  placeholder.
- Documentation: `AGENTS.md` is the AI instruction file, `CLAUDE.md` imports it, and version notes
  moved from the README to this file.

## [0.1.44] - 2026-09-25

### Security

- `Utils/Json.cs`: `TypeNameHandling.Auto` deserialization goes through `RIoT2SerializationBinder`.
  It allows only RIoT2 assemblies, primitives, arrays and generic collections of allowed types.
  This closes a remote deserialization-gadget vector on configuration posted over REST and MQTT.
- `NodeConfigurationServiceBase`: plugin package file names are sanitized, and zip extraction
  outside `Plugins/` is rejected (zip-slip).
- `Utils/Web.cs`: the TLS bypass is limited to the header-based overloads used for self-signed
  device certificates (Hue).

### Fixed

- `ToEpoch()` converts local-time `DateTime` values to UTC first.
- `AdvertisementData` parses beacon payloads as hex instead of ASCII.

## [0.1.43] - 2026-09-23

### Added

- `NodeMqttService` dispatches commands responsively. The receive callback starts command
  dispatch without awaiting device I/O, so a configuration message can cancel a stalled command.
  At most 64 commands are outstanding. Overflow and commands arriving during stop are rejected
  with a warning and not retried. Stop cancels and awaits all admitted work. Queued commands can't
  move to a replacement configuration.
- `NodeMqttService` gets an extra constructor overload with a client factory, for isolated
  transports such as loopback integration tests.

## [0.1.42] - not tagged

### Added

- Opt-in asynchronous devices:
  - `IAsyncDevice`, `IAsyncCommandDevice` and `IAsyncRefreshableReportDevice`;
  - `AsyncDeviceBase`, with state transitions and synchronous compatibility entry points.
- `DeviceServiceBase` serializes operations per device:
  - Replacing the configuration cancels and awaits old work, stops the old generation, then
    starts the new one.
  - Removed devices stay stopped. A failed shutdown blocks the replacement.
- `ReportService` suppresses reports from inactive devices. Quartz refreshes are awaited through
  the same adapter.
- `DisposeAsync` on device services.
- `MqttClient.MessageReceivedAsync`, an awaited event. The legacy event is unchanged.

## [0.1.41] - not tagged

### Added

- `MqttClient.ConnectedAsync`, raised on the initial connection and on every reconnection.
  Register handlers before `Start`.

### Changed

- State and history reads return detached snapshots.
- Scheduler reloads and shutdown remove the device refresh subscriptions they own.

## [0.1.40] - not tagged

### Added

- `NodeOnlineMessage.GrpcBaseUrl` (optional). Workflow nodes advertise their gRPC endpoint
  separately from `NodeBaseUrl`.

### Changed

- Message deserialization preserves large integers and JSON-looking text values.

## [0.1.39] - 2026-09-23

### Removed

- The internal rule engine: the rule evaluator, the rule and function models, and the NCalc
  dependency. **Breaking API change.** Automation is handled by Elsa 3.

## Earlier versions

Tags `0.1.30`–`0.1.38`. See `git log` and the tags; there are no release notes for them.
