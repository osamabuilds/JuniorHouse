# Production tracking

Produce (BRD §5.10): runs, milestones, the PP-sample gate and vendor delivery notes.

**Schema:** `PROD` · **Code:** `src/api/src/Modules/Production/` · **Web:** `features/production/` · **Rules:** [constitution](../../CONSTITUTION.md)

## Features

| Feature | What it does | Status |
|---|---|---|
| [production-runs](production-runs/README.md) | A run per sent PO, flags and expected completion | Implemented |
| [milestones](milestones/README.md) | Milestone progress against a PO | Implemented |
| [samples](samples/README.md) | PP-sample approval gate | Implemented |
| [deliveries](deliveries/README.md) | Vendor delivery notes, partial deliveries | Implemented |

## Module decisions

Decisions that apply to several features of this module are recorded here, or in the ADRs under [`docs/adr/`](../../adr/).

Back to the [spec index](../README.md).
