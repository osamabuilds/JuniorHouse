# Listings: acceptance criteria and the tests that prove them

C# tests are tagged `[Trait("Spec", "<ID>")]`; web and end-to-end tests name the ID in the test title. A cell lists the test files, with how many tests name the ID. Backend tests: `src/api/tests/Romp.Modules.Storefront.Tests`, `Romp.Api.IntegrationTests/Storefront`, `Romp.ArchitectureTests`; web specs sit beside their component; end-to-end specs are in `src/web/e2e/storefront/` and `src/web/e2e/journeys/`.

| ID | Proven by |
|---|---|
| LST-1 | `StorefrontPublicEndpointsTests.cs` (1); `ListingFilterTests.cs` (2); `SearchListingTests.cs` (3); `listings.e2e.spec.ts` (2) |
| LST-2 | `ListingFilterTests.cs` (3); `SearchListingTests.cs` (1); `PublishedCatalogRuleTests.cs` (1); `listing-query.spec.ts` (1) |
| LST-3 | `listings.e2e.spec.ts` (1); `listing-page.container.spec.ts` (1) |
| LST-4 | `SearchListingTests.cs` (2); `listings.e2e.spec.ts` (1); `listing-page.container.spec.ts` (1); `listing-query.spec.ts` (1) |
| LST-5 | `SearchListingTests.cs` (2); `listings.e2e.spec.ts` (1) |
| LST-6 | `SearchListingTests.cs` (1); `listing-page.container.spec.ts` (1) |
| LST-7 | `SearchListingTests.cs` (1) |
| LST-8 | `SearchListingTests.cs` (1) |
| LST-9 | `SearchListingTests.cs` (2) |
| LST-10 | `ListingFilterTests.cs` (1); `listings.e2e.spec.ts` (1); `listing-page.container.spec.ts` (1); `listing-query.spec.ts` (1) |
| LST-11 | `StorefrontPublicEndpointsTests.cs` (5); `ListingFilterTests.cs` (8); `SearchListingTests.cs` (1); `listings.e2e.spec.ts` (1); `listing-page.container.spec.ts` (2); `listing-query.spec.ts` (3) |
| LST-12 | `SearchListingTests.cs` (1); `listings.e2e.spec.ts` (3); `listing-page.container.spec.ts` (4) |
| LST-13 | `ListingPerformanceTests.cs` (3); `SearchListingTests.cs` (1) |
| LST-14 | `StorefrontPublicEndpointsTests.cs` (1); `ListingFilterTests.cs` (1); `SearchListingTests.cs` (1); `listings.e2e.spec.ts` (2) |
| LST-15 | `SearchListingTests.cs` (1) |
| LST-16 | `ListingFilterTests.cs` (1); `SearchListingTests.cs` (1); `listing-query.spec.ts` (1) |
