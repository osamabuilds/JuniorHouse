# Product media

- **Module:** [`catalog`](../README.md) · **Feature:** `product-media` · **Status:** Implemented

Images with ordering and mandatory, quality-checked alt text at publish.

## Specification
- [spec.md](spec.md) · [plan.md](plan.md) · [tasks.md](tasks.md) · module context: [../README.md](../README.md)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Catalog/*/ProductMedia` |
| API tests | `src/api/tests/Romp.Modules.Catalog.Tests/ProductMedia`, `src/api/tests/Romp.Api.IntegrationTests/Catalog/` |
| Web | `src/web/projects/admin/src/app/features/catalog/products/components/images-panel` (the Images tab); e2e `src/web/e2e/catalog/product-media.e2e.spec.ts` |

See the [traceability](../traceability.md) for the test behind each acceptance criterion.
