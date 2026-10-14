# Stock ledger

- **Module:** [`warehouse`](../README.md) · **Feature:** `stock-ledger` · **Status:** Implemented

The append-only record of every stock movement, stock by bin, the available quantity per variant, and the stock events other modules consume.

## Specification
- [traceability](../traceability.md) · [spec.md](spec.md) · [plan.md](plan.md) · module context: [../README.md](../README.md)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Warehouse/*/StockLedger` |
| API tests | `src/api/tests/Romp.Modules.Warehouse.Tests/StockLedger`, `src/api/tests/Romp.Api.IntegrationTests/Warehouse/` |
| Web | `src/web/projects/admin/src/app/features/warehouse/stock-ledger`; e2e `src/web/e2e/warehouse/stock-ledger.e2e.spec.ts` |
