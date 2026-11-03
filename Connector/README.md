# RIoT2.Connector.Sdk

The connector SDK ([design 7.3](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/design/connector-sdk.md),
[ADR 0009](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0009-connector-sdk.md)): everything a
connector needs except the write itself. Package `RIoT2.Connector.Sdk` (.NET 10), released with RIoT2.Core since
1.20.0. The InfluxDB connector is the reference implementation.

## Writing a connector

Implement `IConnectorSink` and host it:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddRIoT2Connector<MySink, MySettings>("My connector", MySettings.Read, options => options.Name = "MyConnector");
var app = builder.Build();
app.MapRIoT2Connector();
app.Run();
```

- `MySettings.Read(SettingsReader)` reads the connector's own `RIOT2_*` settings with the same reader as the shared
  ones (`RIOT2_CONNECTOR_ID`, `RIOT2_CONNECTOR_NAME`, `RIOT2_CONNECTOR_URL`, `RIOT2_HANDLE_COMMANDS`, the spool, MQTT,
  logging, telemetry, security), so a bad start lists every problem at once. `MySettings` is registered as a singleton
  for the sink. `options.Name` is the default name; `RIOT2_CONNECTOR_NAME` replaces it.
- `IConnectorSink.WriteAsync(batch, templates, cancellationToken)` gets `ConnectorMessage`s (kind, node id, the raw
  JSON, `ReceivedUtc`) in order, and maps them with `templates.Snapshot` (`FindReport`, `FindCommand`).
  - Throw `TransientSinkException` when the target is down or busy: the same batch is retried, 1 s doubling to 60 s.
    With the spool, the batch and what waits in memory move to disk, and new messages follow them there until they
    are written; without it, new messages wait in memory.
  - Throw `SinkRejectedException` when the target refuses the data itself: the batch goes to the dead-letter file
    (dropped without the spool). Any other exception is treated the same, so a bug can't stall the queue. Map each
    message on its own, so one bad message doesn't take its batch with it.
  - Call `ConnectorMetrics.RecordIgnored(message)` for a message there is nothing to write for.
  - Use `ReceivedUtc` for commands, which carry no time of their own.
  - Use `EntityFlattener.Flatten` for entity values, so every connector names entity fields the same way.
  - Override `CheckHealthAsync` to tell `/health` whether the target answers.
- Writing back into RIoT2 (2.1.0): inject `IRiot2CommandClient` and call `SendAsync(commandId, value, options)`. It
  calls the orchestrator's `POST /api/v2/commands`, so the command goes through the outbox with a time-to-live
  (`CommandSendOptions.TimeToLive`) and a result (`Wait`); it throws `CommandRejectedException` for a 400 or 404,
  `HttpRequestException` when the orchestrator doesn't answer, and `InvalidOperationException` before the first
  configuration command (`IsReady`). Never publish command topics from a connector.
- Reacting to new templates (2.1.0): register an `IConnectorTemplateObserver` singleton; it gets every complete set
  after it loads. `ConnectorTemplate` carries the value `Type`, `Address`, `DeviceId`, `NodeId` and the report
  `Parameters` (`unit`, `precision`) since 2.1.0.

## What the SDK does

| Type | What it does |
| --- | --- |
| `ConnectorHostExtensions` | `AddRIoT2Connector`: logging, settings, security seams, telemetry (meter `RIoT2.Connector`), the template client (`X-RIoT2-Key`, outbound URL policy), sink, queue and MQTT; `MapRIoT2Connector`: `/health` and `/healthz` |
| `ConnectorOptions` | The shared settings, and the queue's limits a connector may change in code: `QueueCapacity` (1000), `BatchSize` (100), `BatchInterval` (1 s), `MinRetryDelay`/`MaxRetryDelay` (1 s/60 s), `DrainTimeout` (10 s) |
| `ConnectorSpoolOptions` | `RIOT2_CONNECTOR_SPOOL` (on by default; off in options built in code), `RIOT2_CONNECTOR_SPOOL_MAX_MB` (100), `RIOT2_CONNECTOR_SPOOL_MAX_AGE_DAYS` (7); in code also `Path` (`Data/spool` next to the app), `SegmentBytes`, `DeadLetterMaxBytes` and `HighWaterMark` (0.8) |
| `ConnectorMqttService` | Presence on `riot2/node/{id}/online` as `nodeType` 4 with the name, `RIOT2_CONNECTOR_URL` and a manifest (versions), again when the orchestrator comes online; the templates on the configuration command, and every readable report (and command, with `RIOT2_HANDLE_COMMANDS=true`) queued with its receive time |
| `TemplateCatalog` | The report, command and variable templates from the orchestrator, replaced only as a complete set |
| `OrchestratorEndpoint`, `Riot2CommandClient` | The orchestrator URL of the last configuration command, and `IRiot2CommandClient` on it (`X-RIoT2-Key`, outbound URL policy; 2.1.0) |
| `ConnectorPipeline` | The memory queue and the disk spool (JSON-lines segments with a checkpoint, written in order, oldest segment dropped at the size or age limit), batches, retry, the dead-letter file, and a stop that finishes the write in flight and spools the rest (without the spool: writes what is queued for up to `DrainTimeout`) |
| `ConnectorHealthReport` | `/health`: MQTT, the sink's `CheckHealthAsync` (5 s) and the spool; `unhealthy` (503) without MQTT, `degraded` when the sink doesn't answer or messages are spooled |
| `ConnectorMetrics` | `riot2.connector.messages`, `.writes`, `.queue_depth`, `.spool_depth`, `.spool_bytes`, `.spool_dropped` ([metrics.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/metrics.md)) |

The UI lists connectors apart from the nodes, with their `/health` (through the orchestrator's
`GET /api/v2/connectors`). Connectors: [InfluxDB](https://github.com/Revolutionized-IoT2/RIoT2.Connector.InfluxDB) (writes,
with the spool), [Prometheus](https://github.com/Revolutionized-IoT2/RIoT2.Connector.Prometheus) (keeps the latest values,
no spool) and [Home Assistant](https://github.com/Revolutionized-IoT2/RIoT2.Connector.HomeAssistant) (MQTT discovery and
commands back through `IRiot2CommandClient`).

Tests: RIoT2.Tests `ConnectorSdkTests`, `ConnectorSpoolTests`, `ConnectorMetricsTests`, `ConnectorListTests`, the
connectors' `InfluxSinkTests`, `GoldenInfluxTests`, `PrometheusExporterTests`, `GoldenPrometheusTests` and
`HomeAssistantTests` (also the command client and the observers).
