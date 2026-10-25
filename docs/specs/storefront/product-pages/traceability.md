# Product pages: acceptance criteria and the tests that prove them

C# tests are tagged `[Trait("Spec", "<ID>")]`; web and end-to-end tests name the ID in the test title. A cell lists the test files, with how many tests name the ID. Backend tests: `src/api/tests/Romp.Modules.Storefront.Tests/ProductPages` and `Romp.Api.IntegrationTests/Storefront`; web specs sit beside their component or container; the end-to-end spec is `src/web/e2e/storefront/product-pages.e2e.spec.ts`.

| ID | Proven by |
|---|---|
| PDP-1 | `GetProductPageTests.cs` (2); `product-page.container.spec.ts` (2); `product-pages-api.service.spec.ts` (1); `product-pages.store.spec.ts` (1); `product-pages.e2e.spec.ts` (1) |
| PDP-2 | `ProductPageRulesTests.cs` (7); `GetProductPageTests.cs` (1); `product-page.container.spec.ts` (1); `product-pages-api.service.spec.ts` (1); `product-pages.store.spec.ts` (1); `product-pages.e2e.spec.ts` (1) |
| PDP-3 | `ProductPageRulesTests.cs` (2); `GetProductPageTests.cs` (1); `product-pages.e2e.spec.ts` (1) |
| PDP-4 | `GetProductPageTests.cs` (1); `product-page.container.spec.ts` (1); `product-pages.e2e.spec.ts` (1) |
| PDP-5 | `GetProductPageTests.cs` (1) |
| PDP-6 | `GetProductPageTests.cs` (1); `size-chart.format.spec.ts` (3); `product-page.container.spec.ts` (1); `product-pages.e2e.spec.ts` (1) |
| PDP-7 | `GetProductPageTests.cs` (1); `product-page.container.spec.ts` (1) |
| PDP-8 | `product-page.container.spec.ts` (1); `product-pages.e2e.spec.ts` (1) |
| PDP-9 | `ProductPageRulesTests.cs` (1); `product-page.container.spec.ts` (1) |
| PDP-10 | `product-page.container.spec.ts` (1); `product-pages.e2e.spec.ts` (1) |
| PDP-11 | `product-page.container.spec.ts` (1); `product-pages.e2e.spec.ts` (2) |
| PDP-12 | `ProductPagePerformanceTests.cs` (2); `product-pages.e2e.spec.ts` (2) |
| PDP-13 | `StorefrontPublicEndpointsTests.cs` (3); `GetProductPageTests.cs` (3); `ProductPageRulesTests.cs` (3); `app.routes.spec.ts` (2); `product-page.container.spec.ts` (2); `product-pages-api.service.spec.ts` (1); `product-pages.e2e.spec.ts` (2) |
| PDP-14 | Not in this feature: the structured data is built by `seo` |
| PDP-15 | `ProductPageRulesTests.cs` (3); `product-page.container.spec.ts` (1); `product-pages.e2e.spec.ts` (1) |
| PDP-16 | `GetProductPageTests.cs` (1); `product-page.container.spec.ts` (1); `product-pages.e2e.spec.ts` (1) |
| PDP-17 | `GetProductPageTests.cs` (1); `product-page.container.spec.ts` (1) |

The end-to-end spec runs against the Docker stack and its one demo product, so the 301 and 410 outcomes are proved by the query and container tests only; the e2e covers the 404s. The LCP check uses the local stack and is a guard, not a field measurement.
