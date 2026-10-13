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
| `messages/` | One `Report` and one `Command` per `ValueType` (`boolean`, `number`, `text`, `entity`, `textarray`), `NodeOnlineMessage` (`node-online`, and `node-offline`, the default last will), `ConfigurationCommand` | The serializer call each producer uses: `Report.ToJson()`, `Json.SerializeIgnoreNulls`, `Json.Serialize` |
| `http/` | Orchestrator responses: node configuration, report, command and variable values, dashboard | `Json.Serialize`, or System.Text.Json with the controller settings for the variable value |
| `stored/` | Real `StoredObjects` files from a development installation, credentials scrubbed. `Rule/` is a legacy type that the orchestrator no longer loads; it is kept for the M2 step 4 loader. | Older orchestrator versions (input) |
| `stored-saved/` | The same objects after the orchestrator loads and saves them again | `StoredObjectService` through the production save paths |

Ids follow the file name: `golden-report-{type}` and `golden-command-{type}`.

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
