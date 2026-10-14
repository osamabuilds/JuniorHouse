# Child-safety checks

- **Module:** [`quality`](../README.md) · **Feature:** `safety-checks` · **Status:** Implemented

The mandatory choking-hazard and drawstring-safety checks; nothing is accepted without them.

## Specification
- Sprint spec: [receiving-and-inspection](../_sprint-specs/receiving-and-inspection/spec.md) (plan, tasks, traceability alongside)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Quality/Romp.Modules.Quality.{Domain,Application,Infrastructure}/SafetyChecks/` |
| API tests | `src/api/tests/Romp.Modules.Quality.Tests/SafetyChecks/` |
| Web | hosted by `quality/batches` (safety-checks panel) |
