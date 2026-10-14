# Delivery notes

- **Module:** [`production`](../README.md) · **Feature:** `deliveries` · **Status:** Implemented

Vendor delivery notes against a PO, including partial deliveries, with on-time and on-quantity facts judged and published for QC and the vendor scorecard.

## Specification
- Sprint spec: [milestones-and-deliveries](../_sprint-specs/milestones-and-deliveries/spec.md) (plan, tasks, traceability alongside)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Production/Romp.Modules.Production.{Domain,Application,Infrastructure}/Deliveries/` |
| API tests | `src/api/tests/Romp.Modules.Production.Tests/Deliveries/` |
| Web | hosted by `production/production-runs` (delivery-note form and list) |
