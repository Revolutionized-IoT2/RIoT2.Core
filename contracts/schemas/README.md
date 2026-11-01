# Contract schema

Applies to: every RIoT2 repository that reads or writes the RIoT2 JSON. `riot2-contracts.schema.json`
is the JSON Schema (draft 2020-12) of the contract types in `RIoT2.Core.Contracts`, exported by
`ContractSchemas` with System.Text.Json's `JsonSchemaExporter`. Introduced by
[I28](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i28-narrow-references-and-generated-models-m)
(plan [M1](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m01-split-core-packages.md) step 5).

## What it describes

- One definition under `$defs` per contract type, named after the C# type: the MQTT messages
  (`Report`, `Command`, `CommandResult`, `ConfigurationCommand`, `NodeOnlineMessage`,
  `NodeStatusMessage`), the node configuration (`NodeDeviceConfiguration` and what it contains,
  including the Matter declarations), the template listings (`NodeReportTemplate`,
  `NodeCommandTemplate`), `Variable` as stored, `DashboardConfiguration`, `DeviceStatus` and the
  enums.
- What `RIoT2JsonOptions.Default` writes: camelCase names, enums as numbers (with their names in
  `x-enumNames`). `ValueModel` and `object` members are `JsonValue`, any JSON value.
- A member is `required` when it is always written. Members left out while `null`, and in the
  types also written without nulls (the MQTT messages, stored variables and dashboards) every
  member that can be `null`, are optional. Reference-typed members may be `null`.
- Objects are open (no `additionalProperties: false`): readers must accept members they don't
  know, which is how the contract grows ([ADR 0005](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0005-split-core-packages.md)).
- `x-contractVersion` is `Contract.Version`.

Orchestrator-only shapes (variables as `/api/v2/variables` serves them, Matter status, backups)
are in the orchestrator's OpenAPI document, not here.

## Who uses it

| Consumer | How |
|---|---|
| RIoT2.Tests | `ContractSchemaTests`: the committed file is current, and every golden file in `messages/`, `http/` and `stored-saved/` is valid against its definition |
| UI | `npm run build` generates `src/models/generated/contracts.ts` from it (`scripts/generate-models.mjs`); the UI's model classes implement those types |

## Changing it

Don't edit the file. Change the contract types, build `RIoT2.Core.sln` (the `tools/SchemaExport`
project rewrites the file), and commit the result; CI fails when the build changes it. Then
regenerate the UI models with `npm run generate` in RIoT2.UI. A schema change is a contract change:
follow [contracts/golden/README.md](../golden/README.md#changing-a-format).
