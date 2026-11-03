# Contoso.Riot2Plugin

A device plugin for the [RIoT2](https://github.com/Revolutionized-IoT2) platform: Contoso devices. A RIoT2 node
installs the plugin zip, loads it in its own load context and runs the devices you configure in the UI.

- Type: .NET 10 class library (RIoT2 device plugin)
- SDK: `RIoT2.Core.Devices` RIOT2_CORE_VERSION (`PluginBase`, `AsyncDeviceBase`, typed options), packed by
  `RIoT2.Core.Devices.Build`, tested with `RIoT2.Core.Devices.Testing`

## Devices

| Device | What it does | Parameters |
| --- | --- | --- |
| `Counter` | A sample: counts up by `step` on every scheduled refresh and reports the value; the command `set` replaces it | `step` (default 1), `start` (default 0) |

## Build, test and pack

The RIoT2 packages are on GitHub Packages, which needs a token with `read:packages` even for public packages:

```bash
dotnet nuget add source https://nuget.pkg.github.com/Revolutionized-IoT2/index.json --name riot2 \
  --username <github-user> --password <token> --store-password-in-clear-text
dotnet test
dotnet publish Contoso.Riot2Plugin.csproj -c Release
```

`dotnet publish` writes `bin/plugin/Contoso.Riot2Plugin_<version>.zip`, with `PluginManifest.json` inside it, and
`<zip>.sha256`. The node installs a plugin only when its `coreVersion` has the node's major and a minor no newer
than the node's, so build on the oldest Core your nodes run.

## Release

Push a tag `x.y.z`: `.github/workflows/release.yml` tests the plugin and attaches the zip and the sidecar to a
GitHub release. In the RIoT2 UI, set the zip's URL as the node's plugin package. The workflows use the shared
RIoT2 workflows; a repository outside the Revolutionized-IoT2 organization needs a `NUGET_PACKAGE_TOKEN` secret
that can read its packages.

## Writing devices

- Derive from `AsyncDeviceBase<TOptions>`; do the I/O in `StartDeviceAsync`, `StopDeviceAsync`, `RefreshAsync`
  and `ExecuteCommandAsync` (`IAsyncCommandDevice`), and never block.
- Declare parameters as `[DeviceParameter]` properties of the options class; mark secrets `Secret = true`.
- Return the reports and commands the device offers from `GetConfigurationTemplate()`. Describe each value with a
  `Schema` (`ValueSchema.Number("°C", ValueSemantics.Temperature)`, `ValueSchema.Boolean(ValueSemantics.Switch)`, …)
  so the UI can pick a dashboard card and build inputs; without one it guesses from `Model`.
- Register the device in `Plugin.Register()`; `PluginConformance` then checks it.
- Mark a device that needs hardware or a remote service with `[RequiresHardware("...")]`.

Guide: [plugin authoring](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/plugin-authoring.md).
Instructions for AI coding agents: [AGENTS.md](AGENTS.md).
