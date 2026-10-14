# Inspections

- **Module:** [`quality`](../README.md) · **Feature:** `inspections` · **Status:** Implemented

AQL sampling (or 100% for small lots), defect logging and inspection rounds, including re-inspection after rework.

## Specification
- Sprint spec: [receiving-and-inspection](../_sprint-specs/receiving-and-inspection/spec.md) (plan, tasks, traceability alongside)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Quality/Romp.Modules.Quality.{Domain,Application,Infrastructure}/Inspections/` |
| API tests | `src/api/tests/Romp.Modules.Quality.Tests/Inspections/` |
| Web | hosted by `quality/batches` (inspection form) |
