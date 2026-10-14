# Putaway

- **Module:** [`warehouse`](../README.md) · **Feature:** `putaway` · **Status:** Implemented

Receiving queue of accepted stock, putaway by scanning the GTIN and the bin, and the moment stock becomes available.

## Specification
- [traceability](../traceability.md) · [spec.md](spec.md) · [plan.md](plan.md) · module context: [../README.md](../README.md)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Warehouse/*/Putaway` |
| API tests | `src/api/tests/Romp.Modules.Warehouse.Tests/Putaway`, `src/api/tests/Romp.Api.IntegrationTests/Warehouse/` |
| Web | `src/web/projects/admin/src/app/features/warehouse/putaway`; e2e `src/web/e2e/warehouse/putaway.e2e.spec.ts` |
