# Stock alerts

- **Module:** [`warehouse`](../README.md) · **Feature:** `stock-alerts` · **Status:** Implemented

Low-stock and stock-out alerts per variant (FR-54), with thresholds and acknowledgement.

## Specification
- [traceability](../traceability.md) · [spec.md](spec.md) · [plan.md](plan.md) · module context: [../README.md](../README.md)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Warehouse/*/StockAlerts` |
| API tests | `src/api/tests/Romp.Modules.Warehouse.Tests/StockAlerts`, `src/api/tests/Romp.Api.IntegrationTests/Warehouse/` |
| Web | `src/web/projects/admin/src/app/features/warehouse/stock-alerts`; e2e `src/web/e2e/warehouse/stock-alerts.e2e.spec.ts` |
