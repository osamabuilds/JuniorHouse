# Inbound deliveries (receiving queue)

- **Module:** [`quality`](../README.md) · **Feature:** `inbound-deliveries` · **Status:** Implemented

QC's own copy of each delivery note that Production publishes, shown as the receiving queue of deliveries waiting for a goods receipt.

## Specification
- Sprint spec: [receiving-and-inspection](../_sprint-specs/receiving-and-inspection/spec.md) (plan, tasks, traceability alongside)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Quality/Romp.Modules.Quality.{Domain,Application,Infrastructure}/InboundDeliveries/` |
| API tests | `src/api/tests/Romp.Modules.Quality.Tests/InboundDeliveries/` |
| Web | hosted by `quality/batches` (receiving queue page) |
