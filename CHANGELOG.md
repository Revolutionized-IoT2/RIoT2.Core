# Changelog

All notable changes to `RIoT2.Core`. A version is released by pushing a git tag. CI then publishes
the NuGet package to GitHub Packages. Versions without a tag were only packed locally (into
`.localfeed`) and are not on GitHub Packages.

## [Unreleased]

## [2.4.0] - 2026-11-04

Device account sign-in: a device that signs in to a cloud account with an OAuth 2.0 authorization code (Netatmo
first) can be connected from the UI instead of the user copying tokens. Additive: objects that don't set the new
members serialize as before, and the existing golden files are unchanged.

- Added (`Contracts`): `DeviceAuthorization` (`provider`, `setupUrl`, `instructions`) and
  `DeviceConfiguration.Authorization`, template-only like `parameterDefinitions`: the orchestrator doesn't store it.
- Added (`Contracts`): `DeviceAuthorizationStatus` (`state`, `account`, `message`), `DeviceAuthorizationState`
  (`NotConfigured` 0, `NotConnected` 1, `Connected` 2, `Expired` 3) and `DeviceStatus.Authorization`. Omitted when
  `null`.
- Added (`Contracts`): `DeviceAuthorizationStart` (`redirectUri`, `state`), `DeviceAuthorizationStartResult`
  (`authorizeUrl`) and `DeviceAuthorizationCompletion` (`code`, `redirectUri`), contract root types; and the URLs
  `Constants.ApiDeviceAuthorizationStartUrl` (`/api/device/{id}/authorization/start`),
  `ApiDeviceAuthorizationCompleteUrl` (`/api/device/{id}/authorization/complete`) on the node and
  `ApiOAuthCallbackPath` (`/api/oauth/callback`) on the orchestrator.
- Added (`Devices`): `IDeviceWithAuthorization` (`Authorization`, `GetAuthorizationStatus`, `BeginAuthorizationAsync`,
  `CompleteAuthorizationAsync`), and `DeviceCalls.GetAuthorizationStatus`, `BeginAuthorizationAsync` and
  `CompleteAuthorizationAsync`, through which hosts call it. `DeviceTemplates.Create` sets the template's
  `Authorization`.
- Changed (`Devices.Testing`): `PluginConformance` checks that a device implementing `IDeviceWithAuthorization` names
  its provider and answers `GetAuthorizationStatus` before and after it is initialized. Plugins without such devices
  are unaffected.

## [2.3.0] - 2026-11-04

Forecast values ([ADR 0019](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0019-forecasts-and-command-plans.md)):
reports can carry values over time ahead, such as a weather forecast or day-ahead electricity prices, and workflows,
language models and the UI can query them. Additive: existing golden files are unchanged.

- Added (`Contracts`): `ForecastSeries` (`issuedAt`, `kind` `forecast` or `confirmed`, `unit`, `resolution`, `points`
  of `ForecastPoint` `start`, `end`, `value`), read from a report value with `ForecastSeries.From` and checked with
  `Validate` (time order, no overlaps, at most 1,000 points), written with `ToValue`.
- Added (`Contracts`): `ForecastWindow.TryResolve` (`all`, `today`, `tomorrow` in a time zone, `next` hours, `range`) and
  `ForecastWindow.Summarize`, which returns a `ForecastSummary`: the window's points, `min`, `max`, the time-weighted
  `average`, `sum`, the `current` point, the lowest and highest N points, and `complete` (values cover the window).
- Added (`Contracts`): `ValueSemantics.Forecast` (`forecast`) and `ValueSchema.Forecast(item)`, the schema a forecast
  report template declares. `ForecastSeries` and `ForecastSummary` are contract root types, with the golden files
  `messages/forecast-series.json` and `http/forecast-summary.json`.
## [2.2.0] - 2026-11-03

Dashboard card kit and device capability discovery: the contract for the UI's new dashboard cards and for
templates that describe their values. Additive: objects that don't set the new members serialize as before, and
the message and response golden files are unchanged. The stored dashboard sample in `stored-saved/` gains a page
`id`, because Orchestrator 1.21.0 gives pages, cards and elements without one an id when it saves the dashboard.

- Added (`Contracts`): `DashboardPage.Id`, and `DashboardComponent.Id`, `Kind` and `Settings`. `kind` names the UI
  card kind, an open set (`button`, `gauge`, `sensor`, `light`, `entities`, `markdown`, `motion`, …), so a new card
  needs no Core change; readers that don't know it use `type`, which the UI still writes. `settings` holds the card's
  own settings (an `ExpandoObject`, like `DashboardElement.Properties`). All omitted when `null`.
- Added (`Contracts`): `ValueSchema`, a JSON-Schema-compatible description of a value (`type`, `title`,
  `description`, `unit`, `semantic`, `minimum`, `maximum`, `multipleOf`, `enum`, `properties`, `items`, `readOnly`),
  with factories (`Boolean`, `Number`, `Integer`, `Text`, `Entity`, `ArrayOf`) and the well-known meanings in
  `ValueSemantics`. `ReportTemplate.Schema` and `CommandTemplate.Schema` (so also the node template listings) carry
  it; the UI builds forms and picks cards from it and infers from `model` without one. Omitted when `null`.
- Changed (`Contracts`): `ContractSchemas` exports recursive types (`ValueSchema`) as one definition, and fails on a
  JSON pointer that isn't a `$defs` reference.
- Added (`RIoT2.Connector.Sdk`): `ConnectorTemplate.Schema`, read from the orchestrator's template routes.
- Changed (`Devices.Templates`): the sample device declares a `Schema` on its report and command templates.

## [2.1.0] - 2026-11-03

The connector SDK's write-back side ([I47](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-3-4.md#i47-home-assistant-connector-m), design 7.3 phase 4). Additive.

- Added (`RIoT2.Connector.Sdk`): `IRiot2CommandClient` (`Riot2CommandClient`): sends a command through the
  orchestrator's `POST /api/v2/commands` with an optional time-to-live and `wait`, with `X-RIoT2-Key` and the outbound
  URL policy (`HttpClient` `riot2-commands`). Throws `CommandRejectedException` for 400 and 404.
- Added (`RIoT2.Connector.Sdk`): `OrchestratorEndpoint`, set from the configuration command's `apiBaseUrl`, and
  `IConnectorTemplateObserver`, told about every complete set of templates (a failing observer is logged and the
  others still run). `ConnectorMqttService` has a constructor with both; `AddRIoT2Connector` registers them.
- Added (`RIoT2.Connector.Sdk`): `ConnectorTemplate.Type`, `Address`, `DeviceId`, `NodeId` and `Parameters`, read from
  the orchestrator's template routes.

## [2.0.0] - 2026-11-03

Core 2.0: the compatibility layers are removed ([I45](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-3-4.md#i45-core-20-remove-compatibility-layers-m), plans M1 step 6, M2 steps 2, 5 and 6,
M12). **Breaking.** The node refuses a plugin built on another Core major, so Node 2.0, Devices
2.0 and RasPi.Devices 2.0 are released together, and other plugins must be rebuilt on 2.0.0 (the
[upgrading guide](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/upgrading.md)). The wire format, `Contract.Version` (1) and every configuration hash are unchanged:
the golden files are the same.

- Removed: the `RIoT2.Core` facade package (type forwards for plugins built before 1.13.0) and its
  `[Obsolete]` types (`IStoredObjectService`, `IMessageStateService`, `MessageStateService`,
  `IOrchestratorConfigurationService`, `IOrchestratorMqttService`, `ICodeProviderService`,
  `CodeProviderService`, the stored-object delegates). Ten packages remain.
- Removed (`Contracts`): the Newtonsoft.Json dependency, with `JsonNewtonsoft.cs` (the Newtonsoft
  `ValueModelConverter`, `JsonEntity`, `RIoT2SerializationBinder`, `PascalCaseNamingStrategy`),
  `NewtonsoftTokenConverterFactory`, and `Json.DeserializeAutoTypeNameHandling`,
  `SerializeAutoTypeNameHandling`, `DeserializePascal`, `Deserialize(string)` and
  `ToDictionary(JObject)`. `RIoT2JsonOptions` no longer reads Newtonsoft's `[JsonProperty]` and
  `[JsonIgnore]`: plugin types use `[JsonPropertyName]`, `[JsonIgnore]` and `[JsonInclude]` (for
  non-public members). An explicit name is written as given; Newtonsoft camel-cased it. The member
  order Newtonsoft used is kept, because the configuration hashes depend on it.
- Changed (`Contracts`): `NewtonsoftDoubleConverter`, `NewtonsoftSingleConverter`,
  `NewtonsoftDecimalConverter` and `NewtonsoftJsonEncoder` are renamed `RIoT2DoubleConverter`,
  `RIoT2SingleConverter`, `RIoT2DecimalConverter` and `RIoT2JsonEncoder`; their output is the same.
- Changed (`Contracts`): `ValueModel.Merge` and `ReplaceAllPlaceholdersWithValue` run on `JsonNode`
  with Newtonsoft's merge rules (names match case-insensitively and keep the existing spelling,
  objects merge, a JSON null doesn't overwrite, arrays gain the items they lack). A placeholder can
  now be replaced with an entity too, and a replacing number keeps its own text.
- Added (`Contracts`): `RIoT2JsonContext`, source-generated metadata for the contract types
  (ADR 0015). It is the first resolver of `RIoT2JsonOptions`; other types still use reflection.
- Removed (`Devices`, `Mqtt`, `Node`, `Contracts`): `IDevicePlugin.Devices` and `PluginBase.Devices`,
  the synchronous `INodeConfigurationService.DownloadPluginPackage` and its
  `NodeConfigurationServiceBase` implementation, `MqttClient.Publish`, the static
  `DeviceSchedulerService.SchedulerEvent` and `TriggerSchedulerEvent`, and
  `DeviceConfiguration.GetDeviceRefreshSchedule`.
- Deprecated (`Devices`): the constructors of `DeviceBase` and `DeviceBase<TOptions>` are
  `[Obsolete]`, so a device deriving from them directly (the synchronous lifecycle) gets CS0618;
  derive from `AsyncDeviceBase` or `AsyncDeviceBase<TOptions>`, which get no warning. The node
  still runs synchronous devices in 2.x; Core 3.0 removes them.
- Changed (`Devices.Build`): the default `RIoT2PluginExclude` no longer lists `Newtonsoft.Json.dll`.
  The node doesn't ship it any more, so a plugin that needs it (Google.Apis does) packs its own.
  `coreVersion` is read from `RIoT2.Core.Devices` only.

## [1.23.0] - 2026-11-02

Async-first plugins and the plugin template ([I41](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-3-4.md#i41-async-first-plugins-and-the-plugin-template-m), [M12](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m12-device-plugin-sdk.md) steps 5 and 6).
Plugins built on 1.23.0 need Node 1.15.0 or newer; older plugin binaries keep working.

- Changed (`RIoT2.Core.Devices`): `AsyncDeviceBase` no longer blocks. Its synchronous
  `StartDevice`, `StopDevice` and `Refresh` throw `NotSupportedException` instead of waiting for the
  async members, so a host must call `StartAsync`, `StopAsync` and `RefreshReportAsync`, as the node
  has since 1.11. It provides a virtual `ExecuteCommand` that throws, which satisfies
  `ICommandDevice` for `IAsyncCommandDevice` devices: drop a `public void ExecuteCommand` shim
  (it now hides the base member, CS0114), or `override` it to keep synchronous commands.
  `RefreshAsync` is virtual (nothing by default) instead of abstract. `DeviceBase` is documented as
  the legacy base class.
- Added (`RIoT2.Core.Devices`): `RIoT2.Core.Plugins.DeviceCalls`, the one way a host calls a device
  (`StartAsync`, `StopAsync`, `ExecuteCommandAsync`, `RefreshAsync`, `IsAsynchronous`); legacy
  synchronous devices run on the thread pool. The node runtime (`DeviceServiceBase`'s operations,
  `CommandService`, `DeviceSchedulerService`), `PluginTestHost` and `PluginConformance` use it.
  `DeviceSchedulerService` with a device service that isn't an `IAsyncDeviceService` now awaits an
  async device's scheduled refresh (it subscribed the synchronous `RefreshReport`).
- Added: the package `RIoT2.Core.Devices.Templates` (`src/Devices.Templates/`), `dotnet new
  riot2-plugin`: a plugin on `PluginBase` with an async sample device with typed options, a test
  project running `PluginConformance`, `dotnet publish` packing the zip, CI and release workflows,
  and README, AGENTS and CHANGELOG. `--CoreVersion` (default: the package's version) and
  `--DisplayName`. Released from the same tag; CI's `plugin-template` job
  (`tools/verify-plugin-template.ps1`) generates, tests and publishes a plugin from the packed
  template.

## [1.22.0] - 2026-11-02

Connectors as their own node type ([I39](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-3-4.md#i39-connectors-in-the-ui-and-a-prometheus-exporter-m), [design 7.3](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/design/connector-sdk.md) phases 3 and 4). Additive; the golden file
`node-online-connector` is new.

- Added (`RIoT2.Core.Contracts`): `NodeType.Connector = 4`; the JSON Schema lists it.
- Changed (`RIoT2.Connector.Sdk`): the presence carries `nodeType` 4, the name (`RIOT2_CONNECTOR_NAME`, else
  the connector's default), `nodeBaseUrl` from the optional `RIOT2_CONNECTOR_URL` (absolute http/https),
  and a manifest with the connector's name and version and the SDK's version as `coreVersion`.
- Added (`RIoT2.Connector.Sdk`): `EntityFlattener`, moved from the InfluxDB connector, for every connector.

## [1.21.0] - 2026-11-02

The connector spool ([I38](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-3-4.md#i38-connector-spool-m), [design 7.3](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/design/connector-sdk.md) phase 2). Only
`RIoT2.Connector.Sdk` changes; the other nine packages are unchanged apart from the version.

- Added: a disk spool in `ConnectorPipeline` (`ConnectorSpoolOptions`; `RIOT2_CONNECTOR_SPOOL`, on by
  default, `RIOT2_CONNECTOR_SPOOL_MAX_MB` 100, `RIOT2_CONNECTOR_SPOOL_MAX_AGE_DAYS` 7). The first transient
  failure, or a memory queue 80 % full, sends messages to JSON-lines segments in `Data/spool`; they are
  written in order from a checkpoint, also after a restart. The oldest segment is dropped at the size or
  age limit. Stopping finishes the write in flight and spools what waits in memory.
- Added: `dead-letter.jsonl` for rejected batches, with the error (moved to `dead-letter.1.jsonl` at 10 MB).
- Added: `ConnectorHealthReport` behind `/health` (MQTT, the sink's `CheckHealthAsync`, queue and spool;
  503 while MQTT is disconnected); `ConnectorMqttService.IsConnected`.
- Added: `riot2.connector.spool_depth`, `riot2.connector.spool_bytes` and `riot2.connector.spool_dropped`
  (`size`, `age`, `unreadable`).
- Unchanged: with the spool off (and in options built in code, where it is off by default) the pipeline
  behaves as in 1.20.0.

## [1.20.0] - 2026-11-02

The connector SDK ([I37](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-3-4.md#i37-connector-sdk-m), [design 7.3](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/design/connector-sdk.md) phase 1). A new package; the
other nine are unchanged apart from the version.

- Added: `RIoT2.Connector.Sdk` (.NET 10, in `Connector/`, see its README). `AddRIoT2Connector<TSink, TSettings>`
  and `MapRIoT2Connector`; `IConnectorSink` with `TransientSinkException` and `SinkRejectedException`;
  `ConnectorMessage` (kind, node id, raw JSON, receive time); `ConnectorOptions` (`RIOT2_CONNECTOR_ID`,
  `RIOT2_HANDLE_COMMANDS`, MQTT, logging, telemetry, security, and the queue's limits);
  `ConnectorMqttService`; `TemplateCatalog`; `ConnectorPipeline` (bounded queue, batches of 100 or after
  1 s, retry 1 s doubling to 60 s, rejection, a 10 s drain on stop); `ConnectorMetrics` (the
  `riot2.connector.*` instruments, moved from the InfluxDB connector; `writes` now counts messages by
  `written`, `retried`, `rejected`, `discarded`).

## [1.19.0] - 2026-11-02

Verified plugin updates on the node ([I35](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-3-4.md#i35-verified-plugin-updates-m), [design 7.2](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/design/desired-state-configuration.md) phase 4, backlog item 5).
Additive; the golden file `node-status-plugin-rejected` is new and the schema gains two optional
members.

- Added (`RIoT2.Core.Contracts`): `PackageManifest.CoreVersion` and `Sha256`, omitted when `null`.
  The node writes `sha256` (of the installed zip) into `Plugins/PluginManifest.json`, so both reach
  the status topic's `pluginManifest`. `NodeConfiguration.ReloadPluginManifest()`.
- Added (`RIoT2.Core.Node`, namespace `RIoT2.Core.Plugins`): `PluginPackageInstaller`.
  `UpdateAsync` compares the package's `.sha256` sidecar with the installed `sha256` (the file name
  without a sidecar, or for packages installed before), downloads a changed package, verifies its
  hash, extracts it into `Plugins.new/` and checks its `coreVersion`; a refused package never
  touches `Plugins/`. `PromoteStaged` (at start) moves the contents of `Plugins/` to
  `Plugins.previous/` and of `Plugins.new/` to `Plugins/`, resuming after an interruption
  (`Data/plugins.install.json`); a swap that fails puts the previous plugins back and refuses the
  package. `ConfirmStartup` keeps the package, or rolls it back once when it doesn't load (a first
  install, with nothing to go back to, stays). Packages refused for good (incompatible, unsafe, not loadable) are remembered in
  `Data/plugins.rejected.json` and not downloaded again. `PluginCompatibility.IsCompatible`: the
  same Core major and a minor no newer than the node's. `PluginUpdateResult`,
  `PluginStartupResult`, `PluginRejection`, `PluginPackageException`.
- Changed: `NodeConfigurationServiceBase.PluginPackages`; `InstallPluginPackage()` installs the
  staged package (and a zip an older node left in `Data/`, verified the same way) instead of
  emptying `Plugins/`; `DownloadPluginPackageAsync` verifies and stages, and throws when the package
  is refused. New overridables `PluginsFolder`, `PluginDataFolder` and `PluginLogger`.

## [1.18.0] - 2026-11-02

The optional security mode's seams ([I34](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i34-security-seams-with-the-mode-fixed-at-off-m), [design 7.5](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/design/security-mode.md) phase S0). Additive: with the default mode `off`
nothing is enforced and nothing behaves differently. Golden files and hashes are unchanged; the
schema gains `SecurityInfo`.

- Added (`RIoT2.Core.Mqtt`): `SecurityOptions` reads `RIOT2_SECURITY_MODE`,
  `RIOT2_SECURITY_FEATURES`, `RIOT2_API_KEY` and `RIOT2_CORS_ORIGINS`. Only `off` is accepted
  (`SupportedModes`); `audit` or `on` is a configuration error until they are implemented.
- Added (`RIoT2.Core.Mqtt`): MQTT over TLS. `RIOT2_MQTT_TLS=true` (`MqttOptions.UseTls`,
  `MqttConfiguration.UseTls`) connects with TLS and host-name validation, port 8883 by default;
  `RIOT2_MQTT_CA_FILE` (`CaFile`) makes the broker's certificate chain to a private CA.
- Added (`RIoT2.Core.Http`, namespace `RIoT2.Core.Security`): `ServiceApiKey` and `ApiKeyHandler`
  send `X-RIoT2-Key`; `IOutboundUrlPolicy`, `AllowAllOutboundUrlPolicy`, `OutboundUrlPolicyHandler`,
  `OutboundPurposes` and `OutboundUrlRejectedException` check outbound URLs. `Web` checks every
  request against `Web.OutboundUrlPolicy` (allow-all by default), and has `SendAsync` and
  `DownloadFile` overloads that take a purpose.
- Added (`RIoT2.Core.Contracts`): `SecurityInfo`, the body of `GET /api/security/info`, in the
  contract schema; golden file `http/security-info.json`.
- Added (`RIoT2.Core.Hosting`, which now depends on `RIoT2.Core.Http`): `AddRIoT2Security()`,
  `UseRIoT2Security()`, `MapRIoT2SecurityInfo()`, the `Viewer`/`Operator`/`Admin` policies
  (`SecurityPolicies`) with the `RIoT2LocalTrust` scheme (`LocalTrustAuthenticationHandler`),
  `DefaultPolicyConvention` for MVC actions without a policy, and
  `RIoT2Security.EndpointsWithoutPolicy()` for tests.
- Changed (`RIoT2.Core.Node`): `NodeConfigurationServiceBase` sends the node's key
  (`ServiceApiKey`, override it) when it fetches its configuration, and downloads plugin packages
  with the `plugin` purpose.
- Build: `renovate.json` extends the shared Renovate preset ([I33](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i33-supply-chain-s)). Renovate opens grouped
  update pull requests once the app is installed (MA7).
- Release: every package gets `<package>.<version>.spdx.json`, an SPDX SBOM of its dependencies, on
  the GitHub release of the tag (`release.yml` needs `contents: write`).

## [1.17.0] - 2026-11-02

Nullable reference types ([I32](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i32-nullable-reference-types-m), plan M8 step 9). Every package is now
nullable-annotated, and CI fails on a nullable warning. Runtime behaviour, golden files, hashes
and the schema are unchanged. Annotations are compile-time only: a consumer without nullable
enabled sees no difference, and binaries built against 1.16 still load. A plugin built on this
version needs Node 1.12.0, which ships it.

- Members that can be null are now `?`:
  - Contract members that are optional or left out while null (for example `Hash`, `Error`,
    `CorrelationId`, `ParameterDefinitions`, `Capabilities`, `Manifest`, `PluginManifest`,
    `MatterEndpoints`), `DeviceConfiguration.CommandTemplates` (a device without commands has
    none), `ReportTemplate.Model` and `CommandTemplate.Model`.
  - Results that are null for bad or missing input: `Json.Deserialize<T>`, `Report.Create` and
    the other `Create` methods, `ValueModel.GetValue<T>`, `DeviceBase.GetConfiguration<T>` and
    `Json.SerializeIgnoreNulls`.
  - `INodeConfigurationService.DeviceConfiguration` and `OnlineMessage`, which are null until
    they are loaded or set.
  - `IDeviceService.GetDeviceByCommandId`/`GetDeviceByReportId` (null when no device matches).
- `DeviceBase.Refresh` and `AsyncDeviceBase.RefreshAsync` take `ReportTemplate? report`. Null
  means a device-level refresh, as before. A plugin that enables nullable overrides them with
  `ReportTemplate?`.
- Serializer-populated contract models turn off CS8618 in a nested `.editorconfig`. There, a
  non-nullable member is one that a valid message carries.
- `DeviceBase<TOptions>.Options` starts as a new options instance instead of null, until binding
  replaces it before `ConfigureDevice`. `DeviceBase.ReportTemplates` starts empty instead of null,
  until `Initialize`.
- No polyfill package: the netstandard2.0 projects use no nullable attributes.
- Fixed (since 1.16.1): an `AsyncDeviceBase` device stopped while a refresh ran ended in `Error`,
  because the refresh treated the cancellation caused by the stop as a failure. It ends `Stopped`
  again. The node wasn't affected: it cancels a device's work before stopping the device.

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
