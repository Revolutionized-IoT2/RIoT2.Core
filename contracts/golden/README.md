# Golden files

Applies to: every RIoT2 repository that reads or writes these formats. The golden files define
the JSON on MQTT, in the orchestrator's HTTP responses and in its `StoredObjects` folder. A change
that breaks one of them fails the tests of every consumer. Introduced by
[I05](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-0-1.md#i05-golden-files-and-route-contract-m)
(plans [M2](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m02-system-text-json-persistence.md)
step 1 and [M7](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m07-contract-integration-tests.md)
step 3).

## Layout

| Folder | Content | Written by |
|---|---|---|
| `messages/` | One `Report` and one `Command` per `ValueType` (`boolean`, `number`, `text`, `entity`, `textarray`), `NodeOnlineMessage` (`node-online`, and `node-offline`, the default last will), `ConfigurationCommand`. From Core 1.6.0 also the additive shapes: `correlated-command`, `result-executed`, `result-failed`, `node-status-applied`, `node-status-failed`, `node-online-capabilities`, `configuration-command-hash`; from 1.19.0 `node-status-plugin-rejected` (`coreVersion` and `sha256` in `pluginManifest`); from 1.22.0 `node-online-connector` (a connector's presence, `nodeType` 4) | The serializer call each producer uses: `Report.ToJson()`, `Json.SerializeIgnoreNulls`, `Json.Serialize`; `CommandResult.ToJson()` and `NodeStatusMessage.ToJson()` |
| `http/` | Orchestrator responses: node configuration (with its real revision and hashes since Orchestrator 1.5.0; `node-configuration-hash` has fixed ones), report, command and variable values, dashboard. From Orchestrator 1.9.0 also the template listings `templates-report`, `templates-command` and `templates-variable` (from the `stored/` samples, sorted by node, device and id; variables are listed under node `Orchestrator`, device `Variable`). `device-templates` (Core 1.16.0) is a node's template listing with `parameterDefinitions`, built by `DeviceTemplates.Create`. `security-info` (Core 1.18.0) is `GET /api/security/info` with the mode off. From Core 2.2.0 `dashboard-configuration-cards` (page and card `id`, card `kind` and `settings`) and `templates-report-schema` (a report template listing with a `schema`). From Core 2.4.0 `device-status-authorization` (a node's `GET /api/device/status` with a device's account sign-in) and `device-template-authorization` (a template listing with `authorization`) | `Json.Serialize`, or System.Text.Json with the controller settings for the variable value and the template listings |
| `stored/` | Real `StoredObjects` files from a development installation, credentials scrubbed. `Rule/` is a legacy type: since Orchestrator 1.11.0 it is moved to `StoredObjects/.archive/` at startup (`TypedPersistenceTests` checks it with this sample). | Older orchestrator versions (input) |
| `stored-saved/` | The same objects after the orchestrator loads and saves them again. Since Orchestrator 1.21.0 (Core 2.2.0) saving gives every dashboard page, card and element without an id a new one, so the dashboard's page has `golden-generated-1` (the test replaces the GUIDs with fixed ids) | `StoredObjectService` through the production save paths (`StoredObjectJson`, System.Text.Json since Orchestrator 1.11.0; Newtonsoft wrote the same bytes before) |

Ids follow the file name: `golden-report-{type}` and `golden-command-{type}`. Files whose names
start with `command-` or `report-` are read as a `Command` or `Report` by the InfluxDB tests and
the UI, so name other shapes differently. The old files stay as they are: they prove that readers
still accept payloads without the newer optional fields.

## Who reads them

| Consumer | Test |
|---|---|
| Core, Orchestrator, InfluxDB connector | `RIoT2.Tests/Golden` (writes and verifies every file) |
| Elsa | `RIoT2.Elsa.Tests/GoldenMessageTests.cs` |
| UI | `RIoT2.UI/tests/contracts.test.cjs` |
| Firmware | `RIoT2.Ard.Shared/tests/test_golden_messages.py` |

Each consumer's CI clones this repository next to its own, like the local workspace.

## Changing a format

1. Make the change, then run the RIoT2.Tests suite with `RIOT2_UPDATE_GOLDEN=1` to rewrite the
   files (run it twice: the InfluxDB expectations read the message files).
2. Review the diff. Property order and number text are compared too.
3. Run every consumer's tests above. A format change that a consumer can't read needs a contract
   change: update [mqtt-topics.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/mqtt-topics.md)
   or [http-api.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/http-api.md),
   and release the producers and consumers together.

New stored samples must have every credential, token, IP address and MAC address replaced first.
