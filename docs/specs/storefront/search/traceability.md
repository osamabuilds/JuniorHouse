# Search: acceptance criteria and the tests that prove them

C# tests are tagged `[Trait("Spec", "<ID>")]`; web and end-to-end tests name the ID in the test title. A cell lists the test files, with how many tests name the ID. Backend tests: `src/api/tests/Romp.Modules.Storefront.Tests/Search`, `Romp.Api.IntegrationTests/Storefront`; storefront web specs sit beside their component under `features/storefront/search`; the admin screens' specs under `projects/admin/.../features/storefront/search`; end-to-end: `src/web/e2e/storefront/search.e2e.spec.ts`.

| ID | Proven by |
|---|---|
| SRC-1 | `SearchProductsTests.cs` (4); `SuggestTests.cs` (3); `SearchEndpointsTests.cs` (1); `search-api.service.spec.ts` (2); `search-suggest.store.spec.ts` (1); `search-page.container.spec.ts` (1); `search.e2e.spec.ts` (3) |
| SRC-2 | `SearchProductsTests.cs` (1); `SearchDomainTests.cs` (1); `SearchEndpointsTests.cs` (1); `search-api.service.spec.ts` (2); `search-page.container.spec.ts` (4); `search.e2e.spec.ts` (2) |
| SRC-3 | `SearchDomainTests.cs` (8); `SearchProductsTests.cs` (2); `SuggestTests.cs` (1); `LookupSkuTests.cs` (1); `SearchEndpointsTests.cs` (4); `search-query.spec.ts` (7); `search-api.service.spec.ts` (1); `search-suggest.store.spec.ts` (1); `search-page.container.spec.ts` (1); `search.e2e.spec.ts` (3) |
| SRC-4 | `LookupSkuTests.cs` (3); `SearchDataTests.cs` (1); `SearchPerformanceTests.cs` (1); `SearchEndpointsTests.cs` (1); `search-query.spec.ts` (1); `search-api.service.spec.ts` (2); `search-page.container.spec.ts` (1); `search.e2e.spec.ts` (3) |
| SRC-5 | `LookupSkuTests.cs` (1); `SearchDomainTests.cs` (1); `search.e2e.spec.ts` (1) |
| SRC-6 | `LookupSkuTests.cs` (1) |
| SRC-7 | `SearchPerformanceTests.cs` (2); `SuggestTests.cs` (1) |
| SRC-8 | `SearchDataTests.cs` (4); `SearchProductsTests.cs` (3); `search.e2e.spec.ts` (1) |
| SRC-9 | `search-page.container.spec.ts` (2); `search.e2e.spec.ts` (3) |
| SRC-10 | **Deferred with the `seo` feature** (the SSR limiter is shared with seo task 12); no test yet |
| SRC-11 | `search-query.spec.ts` (1); `search-page.container.spec.ts` (1); `search.e2e.spec.ts` (1) |
| SRC-12 | `SearchDomainTests.cs` (4); `SearchDataTests.cs` (1); `SearchProductsTests.cs` (1); `SuggestTests.cs` (1); `SearchAdminTests.cs` (8); `StorefrontAdminSecurityTests.cs` (1); admin `search.store.spec.ts` (7), `synonyms-page.container.spec.ts` (1), `synonym.form.spec.ts` (1); `search.e2e.spec.ts` (1) |
| SRC-13 | `SearchDomainTests.cs` (2); `SearchMissTests.cs` (8); `SearchAdminTests.cs` (1); admin `search.store.spec.ts` (2), `misses-page.container.spec.ts` (1) |
| SRC-14 | `SearchProductsTests.cs` (1); `SuggestTests.cs` (1); `LookupSkuTests.cs` (1) |
