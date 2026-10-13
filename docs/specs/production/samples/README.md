# PP samples

- **Module:** [`production`](../README.md) · **Feature:** `samples` · **Status:** Implemented

The pre-production sample approval gate: rounds of sampling, approve or reject, and the hard gate before bulk production.

## Specification
- Sprint spec: [milestones-and-deliveries](../_sprint-specs/milestones-and-deliveries/spec.md) (plan, tasks, traceability alongside)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Production/Romp.Modules.Production.{Domain,Application,Infrastructure}/Samples/` |
| API tests | `src/api/tests/Romp.Modules.Production.Tests/Samples/` |
| Web | hosted by `production/production-runs` (PP-round panel) |
