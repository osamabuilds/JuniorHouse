# Goods receipts

- **Module:** [`quality`](../README.md) · **Feature:** `receiving` · **Status:** Implemented

Goods receipt notes: counted quantity per size × colour reconciled against what the vendor claimed (Matched, Short or Over), one GRN per delivery note (FR-SC-04).

## Specification
- Sprint spec: [receiving-and-inspection](../_sprint-specs/receiving-and-inspection/spec.md) (plan, tasks, traceability alongside)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Quality/Romp.Modules.Quality.{Domain,Application,Infrastructure}/Receiving/` |
| API tests | `src/api/tests/Romp.Modules.Quality.Tests/Receiving/` |
| Web | hosted by `quality/batches` (receive page, GRN form) |
