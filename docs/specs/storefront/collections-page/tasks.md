# Collections page: tasks

Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md). Each step is test-first; the test named is written and seen failing before the code.

## API (Storefront, .NET)

- [ ] **C1** (not done: the index check on the collection read table was not run; the grouped query is covered by the constant-query test and the demo data is small) Check the read-model collection table has indexes on the code column and on the product id; add a migration only if one is missing. *Proves: a test in `Romp.Modules.Storefront.Tests` that lists the model's indexes for the collection entity.*
- [x] **C2** `GetCollectionsQuery` and handler: enabled collections only, at least one listable product, ordered by lookup sort then name, paged (default 24, max 48). *Tests: `GetCollections_ListsOnlyCollectionsWithLiveProducts` (COL-1, COL-2), `GetCollections_SkipsDisabledCollections` (COL-3), `GetCollections_OrdersByLookupSortThenName`, `GetCollections_ClampsPageAndPageSize`, `GetCollections_ReturnsEmptyPageWhenNothingQualifies` (COL-6).* Tag with `[Trait("Spec", "COL-x")]`.
- [x] **C3** Image per tile: the first listable product's main image through `IStorefrontImageQueries`, one call for the whole page. *Tests: `GetCollections_UsesTheFirstProductsImage`, `GetCollections_AsksForImagesOnce` (no N+1, counts the calls).*
- [ ] **C4** (partly: the endpoint is covered by browser tests of the real API: shape, caching header, clamping; the Testcontainers integration test in `Romp.Api.IntegrationTests` was not written and runs only on CI) Endpoint `GET /api/storefront/collections` with `Cache-Control: public, max-age=60`. *Tests (Storefront integration, Testcontainers): `Collections_Endpoint_IsAnonymousAndCached`, `Collections_Endpoint_ReturnsPagedShape`. These run on CI (Linux) because Application Control blocks local DLLs.*
- [x] **C5** Architecture and structure tests still pass (`StructureSyncTests`, `Romp.ArchitectureTests`); feature folder `Collections/` under Application with matching namespaces. *Proves: the existing test projects.*

## Web (Angular storefront)

- [x] **W1** Model, API service and store (actions, reducer keyed by page, selectors, effects) in `features/storefront/collections/`. *Tests: `collections.store.spec.ts` (load, stale key ignored, error status).*
- [x] **W2** `CollectionTileComponent` (presentational). *Tests: renders one link to `/collections/<code>`, the name, "N pieces" text with correct singular ("1 piece"), the image with alt text and size, placeholder when no image (COL-1, COL-4).*
- [x] **W3** `CollectionsPageContainer`: breadcrumb, one h1, `<ul>` of tiles, empty message with a New in link (COL-6), pagination, response status 200, SEO (title, description, canonical, BreadcrumbList) and error status 503. *Tests: container spec for COL-1, COL-4, COL-5, COL-6 and for headings not skipping a level.*
- [x] **W4** Routes: `collections` before `collections/:code` (client and server route files). *Test: router spec that `/collections` resolves to this container and `/collections/bestsellers` still resolves to the listing.*
- [x] **W5** ADR 0028: add the `collections` slice to `TRANSFERRED_SLICES`. *Test: the whitelist test lists it; the container spec with a seeded slice shows tiles on first render (no loading text).*
- [x] **W6** Styles: reuse `rp-tile` and `rp-split`; add only the minimum component-local layout (two across from 40rem, stacked on a phone). No raw colours. *Tests: e2e COL-7.*

## End to end and close-out

- [x] **E1** Browser tests in `e2e/journeys/storefront-collections.e2e.spec.ts` against the rebuilt container: COL-1, COL-5 (server HTML, no scripting), COL-7 at 390, 768 and 1280 px (no horizontal scroll, 44px targets, axe clean), COL-8 (footer and header links open the page, no 404), CLS at or under 0.1.
- [x] **E2** Rebuild the docker storefront and API with `docker-compose.secure.yml`, open `/collections`, and check it by eye at phone and desktop width.
- [x] **E3** Update `spec.md` ("As built"), the storefront README row to Implemented, the design audit (D2 fixed), a note in the deferred `seo` feature that `/collections` belongs in the sitemap, and the memory hand-off.
- [ ] **E4** PR description links this folder and lists COL-1..8, FR-01, SEO-03, SEO-04, NFR-01, NFR-17, NFR-19.
