# Vendors

- **Module:** [`vendor`](../README.md) · **Feature:** `vendors` · **Status:** Implemented

Vendor records (contact, city, specialisation, payment terms) and the running scorecard: on-time %, on-quantity % and defect rate, fed by delivery and QC facts.

## Specification
- Sprint spec: [po-creation-and-master-data](../../vendor/_sprint-specs/po-creation-and-master-data/spec.md) (plan, tasks alongside)
- Scorecard clause: [receiving-and-inspection](../../quality/_sprint-specs/receiving-and-inspection/spec.md)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Vendor/Romp.Modules.Vendor.{Domain,Application,Infrastructure}/Vendors/` |
| API tests | `src/api/tests/Romp.Modules.Vendor.Tests/Vendors/` |
| Web | `src/web/projects/admin/src/app/features/vendor/vendors/` |
