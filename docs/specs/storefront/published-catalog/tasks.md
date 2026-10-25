# Tasks: published-catalog (Sprint 9a)

Plan: [plan.md](plan.md) · Spec: [spec.md](spec.md). Each step names the test that proves it and leaves the build green. Tests carry `[Trait("Spec", "PCT-n")]`. API integration tests run in the Linux container/CI only (CLAUDE.md environment notes).

## A. Producer contracts (additive, no existing consumer changes)

1. [ ] Glossary: add `STFR`, `AVLB`, `DRV`, `SYNM`, `SRCH`, `MISS`, `VRSN`, `RBLD`, `HASH`, `QRY`, `RDY`, `SWCH`, `VSBL`, `RECN` to `docs/db/naming.md` §4 and the module table. Proof: naming test over the EF model.
2. [ ] Catalog: `PRDT_MAIN.CNTT_VRSN` column, bumped in the same transaction as every change that emits an event (images, composition, care, price, variant becoming sellable); migration backfills 1. Proof: Catalog handler tests (version increases on each change; PCT-5).
3. [ ] Catalog events gain an additive `Version`. Proof: event serialisation test, old payload still deserialises (ADR 0011).
4. [ ] `Catalog.Contracts`: `IPublishedProductQueries` (`GetSnapshotAsync`, `ListVersionsAsync`, `ListSnapshotsAsync`) and `PublishedProductSnapshot`; implementation in Catalog.Application, set-based (no N+1). Proof: query tests incl. unpublished returns null, paging by id, query-count test (PCT-1, 2, 8).
5. [ ] `Catalog.Contracts`: `ISizeChartQueries` (`ListChartsAsync`, `GetChartAsync` with Unisex fallback). Proof: query tests (PCT-1).
6. [ ] `Warehouse.Contracts`: `IStockAvailabilityQueries`; `StockLevelChangedEvent` and `StockPutawayConfirmedEvent` gain additive `Version` (ledger sequence). Proof: Warehouse query and event tests (PCT-3, 5).
7. [ ] `Reference.Contracts`: `ICatalogLookupQueries` list methods (genders, categories, age brackets, collections, sizes, colours; swatch if present). Proof: query tests; ordering by `SORT_SEQ`.

## B. Read model

8. [ ] Domain (`Storefront.Domain/PublishedCatalog`): `VersionedSnapshot`, `AvailabilityClass`, `ProductVisibility`, `PriceSummary`, `DerivativeName`. Proof: domain unit tests (PCT-3, 5, 9, 10).
9. [ ] Infrastructure: `IStorefrontReadDbContext`, EF configurations for every `STFR` table (explicit `ToTable`/`HasColumnName`), migration `AddStorefrontReadModel` (quoted raw SQL for `pg_trgm` and indexes), seed `DRV_STS_LKP`. Proof: naming test; migration test on Testcontainers.
10. [ ] `ApplyProductSnapshotCommand` with the version-wins upsert and removal on null snapshot. Proof: Testcontainers tests for duplicate, out-of-order and removal (PCT-1, 2, 4, 5).
11. [ ] `ApplyAvailabilityCommand` and `AVLB_STK` holding area for events that arrive before the product. Proof: tests incl. no stock event ever received gives `NotYetAvailable` (PCT-3, 9, 11).
12. [ ] `ApplySizeChartCommand`. Proof: handler test (PCT-1).
13. [ ] Event consumers via mappers with inbox idempotency (ADR 0011). Proof: same event delivered 10 times gives one state (PCT-5).
14. [ ] `ReconcileCommand` job (10 min): refetch missing/older, remove unknown, refresh lookup copies. Proof: lost-event test (PCT-5).
15. [ ] `RebuildStorefrontCommand` (sweep by run id), `RBLD_RUN`, endpoints `POST/GET /api/admin/storefront/rebuild`, operator CLI mode `rebuild-storefront`, auto-run on schema version mismatch. Proof: empty-model rebuild test, old rows keep serving during rebuild, 202 response (PCT-6). Permission policy is added when Sprint 8 lands; until then the route is local-only like other early endpoints.

## C. Images

16. [ ] Pin SkiaSharp in `Directory.Packages.props` (licence noted); `IImageResizer` port with fake. Proof: build with warnings as errors.
17. [ ] `ProcessDerivativeJob`: `SKIP LOCKED` claim, decode caps (`MaxImagePixels`), metadata stripped, WebP + JPEG at configured widths, temp key then rename, retry with backoff, then `IMG_RDY_IND`. Proof: worker tests with fake resizer and storage; one real-resizer test in the Linux container (PCT-7).
18. [ ] Changed image gets new hashed names; `SweepDerivativesJob` removes unreferenced files after the cache lifetime. Proof: tests (PCT-10).
19. [ ] Readiness check reports failed-derivative count without failing. Proof: health test.

## D. Hardening and close

20. [ ] Settings section `Storefront:` validated at start-up (`ImageWidths`, quality, `LowStockThreshold`, `ReconcileInterval`, etc.). Proof: options validation tests.
21. [ ] `StorefrontFeatureDependencyTests`; add Storefront to `ModuleNames` if not already there; check no cross-module table reads. Proof: architecture tests.
22. [ ] Public endpoints never return exact stock quantity. Proof: serialisation test over all storefront DTOs (PCT-3).
23. [ ] Constant query count on product and listing reads. Proof: query-count test (PCT-8).
24. [ ] End to end: publish a product in Catalog, see it in the read model within 5 s. Proof: `published-catalog.e2e.spec.ts` / integration test (PCT-1).
25. [ ] Write `traceability.md`, update the spec to what was built, README status.
