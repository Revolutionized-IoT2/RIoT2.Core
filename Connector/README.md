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
  ones (`RIOT2_CONNECTOR_ID`, `RIOT2_HANDLE_COMMANDS`, MQTT, logging, telemetry, security), so a bad start lists every
  problem at once. `MySettings` is registered as a singleton for the sink.
- `IConnectorSink.WriteAsync(batch, templates, cancellationToken)` gets `ConnectorMessage`s (kind, node id, the raw
  JSON, `ReceivedUtc`) in order, and maps them with `templates.Snapshot` (`FindReport`, `FindCommand`).
  - Throw `TransientSinkException` when the target is down or busy: the same batch is retried, 1 s doubling to 60 s,
    while new messages wait in the queue.
  - Throw `SinkRejectedException` when the target refuses the data itself: the batch is dropped. Any other exception
    is treated the same, so a bug can't stall the queue.
  - Call `ConnectorMetrics.RecordIgnored(message)` for a message there is nothing to write for.
  - Use `ReceivedUtc` for commands, which carry no time of their own.

## What the SDK does

| Type | What it does |
| --- | --- |
| `ConnectorHostExtensions` | `AddRIoT2Connector`: logging, settings, security seams, telemetry (meter `RIoT2.Connector`), the template client (`X-RIoT2-Key`, outbound URL policy), sink, queue and MQTT; `MapRIoT2Connector`: `/health` and `/healthz` |
| `ConnectorOptions` | The shared settings, and the queue's limits a connector may change in code: `QueueCapacity` (1000), `BatchSize` (100), `BatchInterval` (1 s), `MinRetryDelay`/`MaxRetryDelay` (1 s/60 s), `DrainTimeout` (10 s) |
| `ConnectorMqttService` | Presence on `riot2/node/{id}/online` (again when the orchestrator comes online), the templates on the configuration command, and every readable report (and command, with `RIOT2_HANDLE_COMMANDS=true`) queued with its receive time |
| `TemplateCatalog` | The report, command and variable templates from the orchestrator, replaced only as a complete set |
| `ConnectorPipeline` | The bounded queue, batches, retry, rejection, and a stop that writes what is queued for up to `DrainTimeout` |
| `ConnectorMetrics` | `riot2.connector.messages`, `riot2.connector.writes`, `riot2.connector.queue_depth` ([metrics.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/metrics.md)) |

Still to come (design 7.3 phases 2–4): the disk spool and dead-letter file, a `/health` that checks the sink,
`NodeType.Connector`, and `IRiot2CommandClient` for connectors that write back into RIoT2.

Tests: RIoT2.Tests `ConnectorSdkTests`, `ConnectorMetricsTests`, and the InfluxDB connector's `InfluxSinkTests` and
`GoldenInfluxTests`.
