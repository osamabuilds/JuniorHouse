# Purchase orders

- **Module:** [`vendor`](../README.md) · **Feature:** `purchase-orders` · **Status:** Implemented

Purchase orders against a vendor with a size × colour breakdown, and the Draft → Sent → Acknowledged → In Production → Partially Delivered → Delivered → Closed lifecycle (or Cancelled). Also shows goods-receipt progress.

## Specification
- Sprint spec: [po-creation-and-master-data](../../vendor/_sprint-specs/po-creation-and-master-data/spec.md) (plan, tasks alongside)
- Sprint spec: [po-amendments-and-spec-files](../../vendor/_sprint-specs/po-amendments-and-spec-files/spec.md) (plan, tasks alongside)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Vendor/Romp.Modules.Vendor.{Domain,Application,Infrastructure}/PurchaseOrders/` |
| API tests | `src/api/tests/Romp.Modules.Vendor.Tests/PurchaseOrders/` |
| Web | `src/web/projects/admin/src/app/features/vendor/purchase-orders/` |
