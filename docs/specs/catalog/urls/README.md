# URL convention and slugs

- **Module:** [`catalog`](../README.md) · **Feature:** `urls` · **Status:** Implemented

The documented URL convention, slug generation, slug history and 301 resolution.

## Specification
- [spec.md](spec.md) · [plan.md](plan.md) · [tasks.md](tasks.md) · module context: [../README.md](../README.md)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Catalog/*/Urls` |
| API tests | `src/api/tests/Romp.Modules.Catalog.Tests/Urls`, `src/api/tests/Romp.Api.IntegrationTests/Catalog/` |
| Web | `src/web/projects/admin/src/app/features/catalog/products/components/url-panel` (the URL tab); e2e `src/web/e2e/catalog/urls.e2e.spec.ts` |

See the [traceability](../traceability.md) for the test behind each acceptance criterion.
