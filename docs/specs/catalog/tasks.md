# Catalog (Sprint 7, Publish): module tasks

Order of work for the whole sprint. Each feature's own steps are in `<feature>/tasks.md`; this file holds the shared steps and the order. Every step names the test that proves it. Test-first: write the failing test, then make it pass. Run .NET builds and tests in the Linux SDK container (Smart App Control blocks local DLLs).

- [x] **G1** Mark ADRs 0013 and 0014 Accepted and the plans Approved (already approved by the owner on 2026-10-14). *Proof:* n/a (doc).
- [x] **G2** Reference additions: `COLN_LKP`, `FIBR_LKP`, `CARE_LKP` (with `SYMB_CODE`) and `ICatalogLookupQueries`, seeded by migration `AddCatalogLookups`; lookup endpoints for the three new lookups. *Proof:* `ReferenceLookupTests` (seed rows present, codes unique, `SYMB_CODE` set for care), naming test passes.
- [x] **G3** Glossary: add the new words to `docs/db/naming.md` ("Added in Sprint 7"): image IMG, alt text ALT, slug SLUG, composition CMPS, fibre FIBR, care CARE, chart CHRT, instruction INST, width WDTH, height HGHT, weight WGHT, chest CHST, waist WAIS, length LEN, compare CMPR, original ORIG, planned PLAN, published PUBL, archived ARCH, redirect RDIR, measurement MEAS (only those actually used). *Proof:* `NamingConventionTests`.
- [x] **G4** `Policies.Catalogue` (RequireRole "Catalogue", "Admin") and its test. *Proof:* `CataloguePolicyTests` (same shape as `InventoryPolicyTests`).
- [x] **G5** Labelling read contract `IVariantQueries.GetByStyleAsync` in `Labelling.Contracts` with its implementation (one query, no N+1). *Proof:* `VariantQueriesTests.ReturnsEveryVariantOfTheStyle_InOneQuery`.
- [x] **G6** Architecture tests: add `CatalogFeatureDependencyTests` (one-way feature dependencies, gate readers), allow `Catalog.Infrastructure → Labelling.Contracts`, and keep Contracts free of each other. *Proof:* `ModuleBoundaryTests`, `CatalogFeatureDependencyTests`.
- [x] **G7** `Catalog` settings section (`MaxImageBytes`, `MaxImagesPerProduct`, `AllowedImageTypes`, `MaxSlugLength`, `ReservedSlugs`) with startup validation. *Proof:* `CatalogOptionsValidatorTests`.
- [x] **G8** Catalog gets an outbox/inbox registration for its consumer; `Catalog.Contracts` events (`ProductPublishedEvent`, `ProductUnpublishedEvent`, `ProductArchivedEvent`, `ProductChangedEvent`, `SizeChartChangedEvent`). *Proof:* `CatalogEventContractTests` (payload carries ids and path only, schema version 1).
- [x] **G9** Migration `AddProducts` (all `CTLG` tables, price-history trigger, published-product alt-text trigger) after the feature entities exist; one migration per feature group is acceptable. *Proof:* `CatalogMigrationTests` (applies on a clean database, triggers refuse the forbidden writes).
- [x] **G10** Admin shell: Catalog navigation (Products, Size charts), footer "Sprint 7 (Publish)", routes under `catalog`. *Proof:* `app.spec.ts` nav list.
- [x] **G11** Demo data (Development only): a girls-dresses size chart for the sample sizes (products are created in the walkthrough, not seeded). *Proof:* `DemoCatalogSeederTests` (seeds twice without duplicates).
- [x] **G12** Close the loop: traceability file, cards Implemented with real paths, specs and plans updated where the build differed, README "Try Sprint 7", `docs/roadmap/sprint-plan.md` note. *Proof:* `StructureSyncTests`.

## Order

1. G1, G2, G3, G4 (shared groundwork) and G7.
2. [urls](urls/tasks.md): the pure rules need nothing else.
3. [products](products/tasks.md) with G5, G8: the aggregate, variants, prices, the Labelling consumer, the publish gate skeleton.
4. [size-charts](size-charts/tasks.md) and [composition-care](composition-care/tasks.md) (independent of each other).
5. [product-media](product-media/tasks.md).
6. Publish gate wired to all gates (last step of products), then G9 migration finalised, G6 architecture tests, G10 shell, G11 demo data.
7. Web screens per feature (each feature's tasks), e2e journeys and layout suite, then G12.

Full verification before the PR: `dotnet test Romp.slnx` in the container, `npx ng test admin --watch=false`, `npx ng build admin`, the new Playwright journeys against the rebuilt docker stack, and the seven-viewport layout suite.
