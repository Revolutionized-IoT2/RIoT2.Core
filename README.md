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

1. **Unified data model** � shared models such as `Report`, `Command`, templates, and
   configuration types that are serialized to/from JSON and exchanged over MQTT.
2. **General utility methods** � JSON helpers, extension methods, and epoch/date conversions.
3. **Reusable program logic** � services, interfaces, enums, delegates, and MQTT
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

- `System.Text.Json` and `Newtonsoft.Json` � JSON serialization.
- `MQTTnet` and `MQTTnet.Extensions.ManagedClient` � MQTT messaging.
- `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Logging` � hosting and logging.
- `Quartz` � scheduling.

## Automation

Elsa 3 (`RIoT2.Elsa`) is the workflow engine. Core no longer includes the internal rule evaluator,
rule/function models, or NCalc dependency. Device refresh scheduling still uses Quartz.
The orchestrator executes device commands through `IOrchestratorMqttService.ExecuteCommand(Command)`,
independently of workflow evaluation.

This is a breaking Core API change. Publish Core as `0.1.41` before building or deploying the updated
orchestrator, which consumes that package version from the private feed.

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

## Contributing

This library is shared across the entire RIoT2 solution, so changes here can affect every consuming
project. Keep the public API stable and additive whenever possible, and preserve `.NET Standard 2.0`
compatibility.