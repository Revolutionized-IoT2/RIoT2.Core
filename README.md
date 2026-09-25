# RIoT2.Core
Shared and core services for RIoT platform.

## Overview

`RIoT2.Core` is the foundational **class library** for the RIoT2 platform. It provides a unified
data model, general-purpose utility methods, and reusable program logic shared across every
project in the RIoT2 solution (nodes, orchestrator, UI, and more).

- **Type:** Library (no entry point)
- **Target framework:** `.NET Standard 2.0`
- **Root namespace:** `RIoT2.Core`

## Purpose

The Core project centralizes shared building blocks to keep the rest of the solution consistent and
free of duplication:

1. **Unified data model** - shared models such as `Report`, `Command`, templates, and
   configuration types that are serialized to/from JSON and exchanged over MQTT.
2. **General utility methods** - JSON helpers, extension methods, and epoch/date conversions.
3. **Reusable program logic** - services, interfaces, enums, delegates, and MQTT
   topic conventions consumed across the solution.

## Project Structure

| Path | Responsibility |
| --- | --- |
| `Constants.cs` | MQTT topic templates, API endpoint URLs, and topic build/parse helpers. |
| `Enums.cs` | Shared enumerations (`ValueType`, `MqttTopic`, `DashboardComponentType`, etc.). |
| `Delegates.cs` | Shared delegate definitions. |
| `Extensions.cs` | Extension methods for JSON, dictionaries, epoch/date conversions, and arrays. |
| `Abstracts/` | Base classes such as `NodeConfigurationServiceBase`. |
| `Interfaces/` | Contracts such as `ITemplate`, `IReport`, `ICommand`, `IMessage`. |
| `Interfaces/Services/` | Service contracts such as `IConfiguration` and `ICodeProviderService`. |
| `Models/` | Data model types (`Report`, `Command`, `ValueModel`, `MqttConfiguration`, `DocumentMetadata`, templates, etc.). |
| `Models/Matter/` | Descriptor types (`MatterEndpointTemplate` and its bindings) used by `IMatterDevice`. |
| `Services/` | Reusable implementations such as `CodeProviderService`. |
| `Utils/` | Utility helpers such as `Json` and `JsonEntity`. |

## Matter device declarations

A device plugin opts in to being bridged to a Matter ecosystem (for example Google Home) by
implementing `IMatterDevice` in addition to `IDeviceWithConfiguration`. The device returns one
`MatterEndpointTemplate` per endpoint it wants exposed, describing the Matter device type and how its
report and command templates map onto Matter cluster attributes:

```csharp
public IEnumerable<MatterEndpointTemplate> GetMatterEndpoints(DeviceConfiguration configuration)
{
    var report = configuration.ReportTemplates[0];
    var command = configuration.CommandTemplates[0];

    yield return new MatterEndpointTemplate
    {
        Id = $"{configuration.Id}:lamp",
        Name = "Living Room Lamp",
        DeviceType = MatterDeviceType.DimmableLight,
        Attributes =
        {
            new MatterAttributeBinding { Attribute = MatterAttribute.OnOff, ReportTemplateId = report.Id, ValuePath = "on" },
            new MatterAttributeBinding { Attribute = MatterAttribute.CurrentLevel, ReportTemplateId = report.Id, ValuePath = "brightness", Scale = MatterValueScale.Percent0To100ToLevel0To254 }
        },
        Commands =
        {
            new MatterCommandBinding { Attribute = MatterAttribute.OnOff, CommandTemplateId = command.Id, ValuePath = "on" },
            new MatterCommandBinding { Attribute = MatterAttribute.CurrentLevel, CommandTemplateId = command.Id, ValuePath = "brightness", Scale = MatterValueScale.Percent0To100ToLevel0To254 }
        }
    };
}
```

The declaration must be built against the `DeviceConfiguration` instance passed in, because devices
that mint template ids per call would otherwise reference ids that are never persisted. Scales are
declared in the RIoT-to-Matter direction; the bridge applies the inverse on the command path.

The templates travel to the orchestrator on `DeviceConfiguration.MatterEndpoints`, through the
existing configuration-template path, and the orchestrator's Matter bridge turns them into bridged
endpoints. `RIoT2.Core` itself contains no Matter protocol code.

## Key Dependencies

- `System.Text.Json` and `Newtonsoft.Json` - JSON serialization.
- `MQTTnet` and `MQTTnet.Extensions.ManagedClient` - MQTT messaging.
- `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Logging` - hosting and logging.
- `Quartz` - scheduling.

## Automation

Elsa 3 (`RIoT2.Elsa`) is the workflow engine. Core no longer includes the internal rule evaluator,
rule/function models, or NCalc dependency. Device refresh scheduling still uses Quartz.
The orchestrator executes device commands through `IOrchestratorMqttService.ExecuteCommand(Command)`,
independently of workflow evaluation.

The rule-engine removal was a breaking Core API change. The orchestrator currently consumes
`0.1.41`; network plugins require `0.1.42` for the additive async contracts below, and the updated
node requires `0.1.43` for bounded MQTT command dispatch.

Version `0.1.40` also preserves large integer and JSON-looking text values when deserializing
messages. `NodeOnlineMessage.GrpcBaseUrl` is an optional, additive field: workflow nodes advertise
their dedicated gRPC endpoint separately from the web UI's `NodeBaseUrl`.

Version `0.1.41` adds MQTT `ConnectedAsync` notifications on initial connection and reconnection.
Register message/connection handlers before `Start`; use the connection callback to republish node
presence. State/history reads are detached snapshots, safe to enumerate while reports arrive.
Scheduler reloads and shutdown remove their owned device refresh subscriptions.

`ValueModel` owns borrowed JSON elements. Updates support dotted property paths and indexed
properties such as `items[0].value`, including existing null values. Adding a final property is
allowed only when its parent object exists; missing/incompatible parents and invalid array indexes
throw `ArgumentException` rather than replacing or modifying an unrelated value.

### Opt-in asynchronous devices (0.1.42)

`IAsyncDevice`, `IAsyncCommandDevice`, and `IAsyncRefreshableReportDevice` add cancellation-aware
Task-returning operations without changing the existing device interfaces. `AsyncDeviceBase` provides
state transitions and synchronous compatibility entry points for migrated plugins. New network
drivers should override its async methods rather than implement `async void` lifecycle methods.

`DeviceServiceBase` owns one serialized operation adapter per device. Configuration replacement
cancels old work, waits for it to finish, stops the old generation, then initializes/starts the new one.
Queued commands from the cancelled generation cannot execute against new configuration; removed
devices remain stopped. Failed shutdown prevents configuration replacement. `ReportService` suppresses
reports from inactive devices, and Quartz awaits refresh operations through the same adapter.
Use `DisposeAsync` (or `await using`) to await service cleanup without blocking a thread.
The synchronous disposal entry point remains available for older hosts.

Existing synchronous plugins remain supported. Their synchronous calls run as tracked tasks and are
awaited, but cannot be forcibly interrupted. Plugins that return before their own background work has
finished (including `async void` implementations) must be migrated to obtain full cancellation and
shutdown guarantees. Never abandon such work with a timeout and immediately reuse the device.

`MqttClient.MessageReceivedAsync` is an additive awaited event; the legacy event is unchanged.
The node uses the awaited event for message admission and configuration loading, and owns command
completion separately as described below.

### Responsive node command dispatch (0.1.43)

`NodeMqttService` tracks up to 64 outstanding commands, including commands queued behind device I/O.
The receive callback starts dispatch without awaiting the entire operation, so a configuration
notification can cancel a stalled command instead of waiting for its deadline. Native async dispatch
captures the current device generation before returning; queued commands cannot migrate to a
replacement configuration. Completion failures are logged, and stop cancels and awaits all admitted
work. Overflow/stopping rejects new commands with a warning; no automatic retry or durable queue is
provided. MQTT acknowledgement does not imply successful device execution.
Custom legacy `ICommandService` implementations remain serialized; a running synchronous call is
awaited during shutdown, while queued calls are cancelled.

The existing constructor is unchanged; an additional client-factory overload supports isolated
transports, including loopback integration tests.

## MQTT contract summary

Core defines these MQTT topics in `Constants`:

| Topic enum | Template | Producer/consumer intent |
| --- | --- | --- |
| `MqttTopic.NodeOnline` | `riot2/node/{id}/online` | Node or connector retained presence (`NodeOnlineMessage`). |
| `MqttTopic.OrchestratorOnline` | `riot2/orchestrator/online` | Orchestrator retained presence; nodes/connectors republish presence after seeing it. |
| `MqttTopic.Configuration` | `riot2/node/{id}/configuration` | Orchestrator asks a node or connector to reload configuration (`ConfigurationCommand`). |
| `MqttTopic.Command` | `riot2/node/{id}/command` | Orchestrator publishes device commands (`Command`). |
| `MqttTopic.Report` | `riot2/node/{id}/report` | Nodes/connectors publish device reports (`Report`). |

Payloads are serialized with camelCase property names and usually omit nulls:

```json
{ "id": "temperature", "timeStamp": 1790253852, "filter": "room", "value": 21.5 }
{ "id": "setRelay", "value": true }
{ "apiBaseUrl": "https://orchestrator.example" }
{ "name": "Node A", "isOnline": true, "nodeBaseUrl": "http://node", "grpcBaseUrl": "http://node:5001", "nodeType": 1 }
```

`Report.value` and `Command.value` use `ValueModel`, so valid JSON primitives, arrays, and objects
are preserved. `Report.timeStamp` is Unix epoch seconds in UTC; use `ToEpoch()`/`FromEpoch()` for
conversion.

## Build and test

From the multi-repo workspace root on Windows PowerShell:

```powershell
dotnet build .\RIoT2.Core\RIoT2.Core.csproj
dotnet test .\RIoT2.Tests\RIoT2.Tests.csproj
```

`RIoT2.Tests` also builds sibling repositories (`RIoT2.Net.Orchestrator` and
`RIoT2.Connector.InfluxDB`) through project references. If those repositories reference an older
published `RIoT2.Core` package, update their package reference before expecting a clean full test
build.

Plugin packages downloaded by `NodeConfigurationServiceBase` are installed only under the
application `Plugins` folder; zip entries that try to escape that folder are rejected.

## Contributing

This library is shared across the entire RIoT2 solution, so changes here can affect every consuming
project. Keep the public API stable and additive whenever possible, and preserve `.NET Standard 2.0`
compatibility.
