# Quality control

Receive and inspect (BRD §5.11): goods receipt, AQL inspection, child-safety checks and batch outcomes.

**Schema:** `QC` · **Code:** `src/api/src/Modules/Quality/` · **Web:** `features/quality/` · **Rules:** [constitution](../../CONSTITUTION.md)

## Features

| Feature | What it does | Status |
|---|---|---|
| [inbound-deliveries](inbound-deliveries/README.md) | Receiving queue | Implemented |
| [receiving](receiving/README.md) | Goods receipt notes and reconciliation | Implemented |
| [batches](batches/README.md) | QC batches, sub-batches, rework returns | Implemented |
| [inspections](inspections/README.md) | AQL sampling, defects, rounds | Implemented |
| [outcomes](outcomes/README.md) | Accept / Concession / Reject / Rework | Implemented |
| [safety-checks](safety-checks/README.md) | Mandatory child-safety checks | Implemented |

## Module decisions

Decisions that apply to several features of this module are recorded here, or in the ADRs under [`docs/adr/`](../../adr/).

Back to the [spec index](../README.md).
