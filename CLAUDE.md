# RIoT2.Core

`RIoT2.Core` is the foundational **class library** for the RIoT2 solution. It provides a unified
data model, general-purpose utility methods, and reusable program logic shared across all other
projects (nodes, orchestrator, UI, etc.) in the RIoT2 ecosystem.

- **Type:** Library (no entry point)
- **Target framework:** `.NET Standard 2.0`
- **Root namespace:** `RIoT2.Core`

## Purpose

The Core project exists to prevent duplication and enforce consistency across the solution by
centralizing:

1. **Unified data model** — shared models such as `Report`, `Command`, and templates that are
   serialized to/from JSON and exchanged over MQTT.
2. **General utility methods** — JSON helpers, extension methods, and epoch/date conversions.
3. **Reusable program logic** — services, interfaces, enums, delegates, and MQTT topic
   conventions consumed by the rest of the solution.

## Project Layout

| Path | Responsibility |
| --- | --- |
| `Constants.cs` | MQTT topic templates, API endpoint URLs, and helpers to build/parse topics. |
| `Enums.cs` | Shared enumerations (`ValueType`, `MqttTopic`, `DashboardComponentType`, etc.). |
| `Delegates.cs` | Shared delegate definitions. |
| `Extensions.cs` | Extension methods for JSON, dictionaries, epoch/date conversions, and arrays. |
| `Abstracts/` | Base implementations for devices, device services, and node configuration/package install. |
| `Interfaces/` | Contracts such as `ITemplate`, `IReport`, `ICommand`, `IMessage`. |
| `Interfaces/Services/` | Service contracts such as `ICodeProviderService`. |
| `Models/` | Data model types (`Report`, `Command`, `ValueModel`, `MqttConfiguration`, templates, etc.). |
| `Models/Matter/` | Matter endpoint, attribute, command-binding, and scaling descriptors. |
| `Services/` | Reusable implementations such as `CodeProviderService`, `NodeMqttService`, and schedulers. |
| `Utils/` | Utility helpers such as `Json`, `JsonEntity`, `Web`, and `MqttClient`. |

## Key Dependencies

- `System.Text.Json` and `Newtonsoft.Json` — serialization (the model layer prefers
  `System.Text.Json` via the `Json`/`ValueModel` helpers).
- `MQTTnet` and `MQTTnet.Extensions.ManagedClient` — MQTT messaging.
- `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Logging` — hosting and logging
  abstractions.
- `Quartz` — scheduling.

Workflow evaluation belongs to Elsa 3 (`RIoT2.Elsa`), not Core. The retired internal rule engine,
its function catalog, and its models must not be reintroduced.

## Core Concepts

### Messages (`Report` and `Command`)

Both implement `IMessage` and are the primary payloads exchanged over MQTT. They provide static
`Create(string json)` factory methods and JSON serialization helpers.

### `ValueModel`

`ValueModel` is a JSON-backed value container that abstracts primitive values, arrays, and
entities. It exposes the value's `ValueType`, supports path-based access, merging, and mapping.

### MQTT Topics (`Constants`)

Topic strings are built from templates using `Constants.Get`, and the target id can be extracted
with `Constants.GetTopicId`.

Defined topics:

- `riot2/node/{id}/online` (`MqttTopic.NodeOnline`) carries `NodeOnlineMessage`.
- `riot2/orchestrator/online` (`MqttTopic.OrchestratorOnline`) carries orchestrator presence.
- `riot2/node/{id}/configuration` (`MqttTopic.Configuration`) carries `ConfigurationCommand`.
- `riot2/node/{id}/command` (`MqttTopic.Command`) carries `Command`.
- `riot2/node/{id}/report` (`MqttTopic.Report`) carries `Report`.

MQTT payloads use camelCase JSON. `Report` shape is
`{ "id": "...", "timeStamp": 1790253852, "filter": "...", "value": <ValueModel> }`.
`Command` shape is `{ "id": "...", "value": <ValueModel> }`.
`ConfigurationCommand` shape is `{ "apiBaseUrl": "..." }`.
`NodeOnlineMessage` shape is `{ "name": "...", "isOnline": true, "nodeBaseUrl": "...", "grpcBaseUrl": "...", "nodeType": 1, "manifest": {...}, "pluginManifest": {...} }`.
`timeStamp` values are Unix epoch seconds in UTC.

### Extension Methods (`Extensions`)

Common helpers for JSON conversion and epoch/date handling. `ToEpoch()` converts the supplied
`DateTime` to UTC before calculating Unix seconds.

### `CodeProviderService`

An in-memory service (contract: `ICodeProviderService`) that issues time- and usage-limited codes,
typically used for device onboarding.

## Conventions

- **JSON:** Serialization uses camelCase property naming and generally ignores null values. Prefer
  the `Json` utility and `ValueModel` over ad-hoc serialization.
- **Namespaces:** Match the folder structure under the `RIoT2.Core` root namespace (e.g. types in
  `Models/` live in `RIoT2.Core.Models`).
- **Framework compatibility:** This library targets `.NET Standard 2.0`, so only APIs available on
  that surface may be used to preserve compatibility with all consuming projects.
- **Factory pattern for messages:** Prefer the static `Create(string json)` factories on message
  models over manual deserialization.

## Building

Because this is a shared library, changes here can affect every project in the RIoT2 solution.
Keep the public API stable and additive whenever possible.

From the workspace root:

```powershell
dotnet build .\RIoT2.Core\RIoT2.Core.csproj
dotnet test .\RIoT2.Tests\RIoT2.Tests.csproj
```

`RIoT2.Tests` also builds sibling repos through project references. Do not edit those repos from
Core-only work unless the task explicitly expands scope. `NodeConfigurationServiceBase` installs
downloaded plugin zip files under the application `Plugins` folder and rejects zip entries outside
that destination.

## Documentation quality

Keep `README.md` valid, readable, and Markdown-renderable. Use correctly closed fenced code blocks, accurate language identifiers, and valid example code where snippets are intended to demonstrate API usage.
