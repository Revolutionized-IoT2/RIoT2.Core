# Changelog

All notable changes to `RIoT2.Core`. A version is released by pushing a git tag. CI then publishes
the NuGet package to GitHub Packages. Versions without a tag were only packed locally (into
`.localfeed`) and are not on GitHub Packages.

## [Unreleased]

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
