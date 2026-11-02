# RIoT2.Core.Devices.Templates

`dotnet new riot2-plugin` creates a [RIoT2](https://github.com/Revolutionized-IoT2) device plugin repository
(plan M12 step 6):

- a plugin project on `PluginBase` with an async sample device (`Counter`, `AsyncDeviceBase<TOptions>`) and its
  typed options;
- a test project running `PluginConformance` and the device;
- `dotnet publish` writing the node's plugin zip with `PluginManifest.json` and a `.sha256` sidecar
  (`RIoT2.Core.Devices.Build`);
- CI and release workflows on the shared RIoT2 workflows, and README, AGENTS.md and CHANGELOG.md.

```bash
dotnet new install RIoT2.Core.Devices.Templates
dotnet new riot2-plugin -n Contoso.Riot2Plugin --DisplayName "Contoso devices"
```

`--CoreVersion` picks the RIoT2.Core.Devices version (default: this package's). A node installs a plugin only
with the same major and a minor no newer than its own. The packages are on GitHub Packages; see the generated
README. Plugin authoring guide:
[guides/plugin-authoring.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/plugin-authoring.md).
