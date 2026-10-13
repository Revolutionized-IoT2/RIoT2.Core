# Changelog

All notable changes to `RIoT2.Core`. A version is released by pushing a git tag. CI then publishes
the NuGet package to GitHub Packages. Versions without a tag were only packed locally (into
`.localfeed`) and are not on GitHub Packages.

## [Unreleased]

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
