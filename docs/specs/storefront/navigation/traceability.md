# Navigation: acceptance criteria and the tests that prove them

C# tests are tagged `[Trait("Spec", "<ID>")]`; web and end-to-end tests name the ID in the test title. A cell lists the test files, with how many tests name the ID. Backend tests: `src/api/tests/Romp.Modules.Storefront.Tests`, `Romp.Api.IntegrationTests/Storefront`, `Romp.ArchitectureTests`; web specs sit beside their component; end-to-end specs are in `src/web/e2e/storefront/` and `src/web/e2e/journeys/`.

| ID | Proven by |
|---|---|
| NAV-1 | `StorefrontPublicEndpointsTests.cs` (1); `GetMenuQueryTests.cs` (5); `MenuBuilderTests.cs` (5); `navigation.e2e.spec.ts` (1); `menu-mapper.spec.ts` (2) |
| NAV-2 | `navigation.e2e.spec.ts` (2); `menu-mapper.spec.ts` (1); `navigation.store.spec.ts` (3) |
| NAV-3 | `GetMenuQueryTests.cs` (2); `MenuBuilderTests.cs` (3); `menu-mapper.spec.ts` (2); `site-header.component.spec.ts` (1) |
| NAV-4 | `MenuBuilderTests.cs` (6) |
| NAV-5 | `CatalogLookupQueriesTests.cs` (1); `GetMenuQueryTests.cs` (1); `MenuBuilderTests.cs` (1); `menu-mapper.spec.ts` (1); `navigation.store.spec.ts` (1); `site-header.component.spec.ts` (1) |
| NAV-6 | `navigation.e2e.spec.ts` (1) |
| NAV-7 | `navigation.e2e.spec.ts` (1) |
| NAV-8 | `navigation.e2e.spec.ts` (1); `listing-query.spec.ts` (1); `menu-mapper.spec.ts` (1); `site-header.component.spec.ts` (1) |
| NAV-9 | `CatalogLookupQueriesTests.cs` (1); `GetMenuQueryTests.cs` (1); `MenuBuilderTests.cs` (1) |
| NAV-10 | `StorefrontPublicEndpointsTests.cs` (1); `GetHomeQueryTests.cs` (5); `navigation.e2e.spec.ts` (2); `catalog-pages.spec.ts` (2); `seo.service.spec.ts` (1) |
| NAV-11 | `seo.service.spec.ts` (1) |
