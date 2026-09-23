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
| `Interfaces/` | Contracts such as `ITemplate`, `IReport`, `ICommand`, `IMessage`. |
| `Interfaces/Services/` | Service contracts such as `ICodeProviderService`. |
| `Models/` | Data model types (`Report`, `Command`, `ValueModel`, templates, etc.). |
| `Services/` | Reusable implementations such as `CodeProviderService`. |
| `Utils/` | Utility helpers such as `Json` and `JsonEntity`. |

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

### Extension Methods (`Extensions`)

Common helpers for JSON conversion and epoch/date handling.

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

## Documentation quality

Keep `README.md` valid, readable, and Markdown-renderable. Use correctly closed fenced code blocks, accurate language identifiers, and valid example code where snippets are intended to demonstrate API usage.