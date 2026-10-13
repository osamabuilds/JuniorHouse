# PO revisions (amendments)

- **Module:** [`vendor`](../README.md) · **Feature:** `revisions` · **Status:** Implemented

Versioned PO amendments (PO-v2, v3, …) that never overwrite the originally agreed terms (FR-SC-03, ADR 0007).

## Specification
- Sprint spec: [po-amendments-and-spec-files](../../vendor/_sprint-specs/po-amendments-and-spec-files/spec.md) (plan, tasks alongside)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Vendor/Romp.Modules.Vendor.{Domain,Application,Infrastructure}/Revisions/` |
| API tests | `src/api/tests/Romp.Modules.Vendor.Tests/Revisions/` |
| Web | hosted by `vendor/purchase-orders` (amend form, revision history) |
