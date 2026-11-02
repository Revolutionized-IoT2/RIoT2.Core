# AGENTS.md — Contoso.Riot2Plugin

Applies to: this repository, a RIoT2 device plugin created with `dotnet new riot2-plugin`. Platform documentation:
[Revolutionized-IoT2/.github](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/README.md); the plugin
guide is [guides/plugin-authoring.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/plugin-authoring.md).

## What this is

A .NET 10 class library the RIoT2 node loads as a plugin. `Plugin` registers the devices; the node resolves them
from DI, configures them from the UI and calls them asynchronously.

## Commands

```bash
dotnet build
dotnet test
dotnet publish Contoso.Riot2Plugin.csproj -c Release   # bin/plugin/<zip> and <zip>.sha256
```

## Layout

| Path | Contents |
|---|---|
| `Plugin.cs` | `PluginBase`: `AddDevice<T>()` and `AddService<TService, TImplementation>()` |
| `Devices/` | The devices, each with its options class |
| `Tests/` | MSTest: `PluginConformance.Verify` and device tests on `PluginTestHost` |
| `Directory.Packages.props` | The RIoT2 package versions (one Core version for all three) |
| `.github/workflows/` | CI and release on the shared RIoT2 workflows |

## Rules

- Devices derive from `AsyncDeviceBase` or `AsyncDeviceBase<TOptions>` and implement `IAsyncCommandDevice` for
  commands. Never block on a task (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`): the threading analyzers
  fail the build.
- Parameters are `[DeviceParameter]` properties, not string keys; keep the keys stable, because stored
  configurations use them.
- Command values arrive as JSON text (`"21.5"`, `"true"`, `"\"text\""`).
- Keep `PluginConformance.Verify(typeof(Plugin))` passing; it is what the node relies on.
- Don't ship `RIoT2.Core*` assemblies: the node provides them, and the build package leaves them out.
- Keep the three RIoT2 package versions equal, and no newer than the Core of the oldest node you support.

## Pitfalls

- A device that throws from `StartDeviceAsync` is shown in `Error` with the message; one that hangs blocks its
  node's configuration apply. Honour the cancellation token.
- A device's reports are sent only while it runs (`SendReport`).

## Related work

- `CHANGELOG.md`: what changed in each release.
