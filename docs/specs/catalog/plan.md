# Catalog (Sprint 7, Publish): module design

- **Module README:** [README.md](README.md) (decisions D1-D6, shared open questions)
- **Features:** [products](products/plan.md) · [product-media](product-media/plan.md) · [size-charts](size-charts/plan.md) · [composition-care](composition-care/plan.md) · [urls](urls/plan.md)
- **ADRs:** [0013 URL convention](../../adr/0013-product-url-convention.md) (accepted) · [0014 variants from Labelling](../../adr/0014-product-variants-from-labelling.md) (accepted) · [0002 modular monolith](../../adr/0002-modular-monolith.md) · [0004 outbox](../../adr/0004-postgres-transactional-outbox.md) · [0008 shared storage](../../adr/0008-production-vendor-coordination-and-shared-storage.md) · [0011 security baseline](../../adr/0011-security-and-coupling-baseline.md)

This file holds what the five features share. Each feature plan holds its domain model, use cases, endpoints and its own security review.

## Module layout

The existing `Romp.Modules.Catalog.{Domain,Application,Infrastructure,Contracts}` projects grow; no new module. Schema `CTLG` and `CatalogDbContext` stay. New API feature folders (namespaces match folders) beside `Styles/`:

| Feature (same name in every tree) | API folder | Web feature |
|---|---|---|
| products | `Products/` | `catalog/products` |
| product-media | `ProductMedia/` | `catalog/products` (Images tab) |
| size-charts | `SizeCharts/` | `catalog/size-charts` |
| composition-care | `CompositionCare/` | `catalog/products` (Composition and care tab) |
| urls | `Urls/` | `catalog/products` (URL tab) |

Web: one `catalog/products` feature owns the product editor (tabs), because images, composition/care and URL are parts of one product page; `catalog/size-charts` is its own screen. The media, composition-care and URL tabs are presentational components inside the product container using the products store slices (NgRx, per CONVENTIONS.md).

**One-way dependencies inside Catalog:** `Urls` (pure rules + history) ← `Products` ← (`ProductMedia`, `CompositionCare`, `SizeCharts` expose a *gate reader* each: `IMediaGate`, `ICompositionGate`, `IChartGate`); `Products.Publish` calls the gates, the gates never call `Products`. Enforced by a new `CatalogFeatureDependencyTests` like `WarehouseFeatureDependencyTests`.

## Cross-module contracts

| Direction | Contract | Use |
|---|---|---|
| Catalog → Reference | `IApparelCodeQueries` (exists) plus a new `ICatalogLookupQueries`: gender, category, collection, fibre, care codes and ids, one query per lookup (cached 5 min) | validate ids; build paths from codes; no cross-schema FK |
| Catalog → Labelling | `Labelling.Contracts`: `VariantGtinAssignedEvent`, `GtinVoidedEvent` (exist) and new `IVariantQueries.GetByStyleAsync(styleId)` (ADR 0014) | local copy `VRNT_LBL`, backfill |
| Storefront (S9) → Catalog | `Catalog.Contracts`: `IProductPathQueries` (resolve a path), later `IPublishedProductQueries` | S9 will define its read model; S7 ships path resolution only |
| Catalog → others | events: `ProductPublishedEvent`, `ProductUnpublishedEvent`, `ProductChangedEvent`, `ProductArchivedEvent`, `SizeChartChangedEvent` (ids and the path only; schema version 1) | storefront cache refresh, sitemap (S9), search |

Architecture tests: `ModuleNames` already has Catalog and Labelling; the boundary test must allow `Catalog.Infrastructure → Labelling.Contracts` and keep the Contracts projects free of each other and of module internals. Reference gains three lookup tables, so its `ModuleBoundaryTests` entry is unchanged.

## Data (schema `CTLG`, names per `docs/db/naming.md`)

All transactional tables: `ID` bigint identity, audit columns `INSR_DTE/INSR_BY/UPDT_DTE/UPDT_BY`, concurrency token. Every FK indexed. Ids that point into other modules (gender, category, colour, size, age bracket, collection, fibre, care) are plain `smallint` columns validated through the Reference contract (no cross-schema FK).

| Table | Purpose | Key columns |
|---|---|---|
| `PRDT_MAIN` | the product | `STYL_ID` (unique, FK `STYL_MAIN`), `NAME`, `DSCR` varchar(2000), `GNDR_ID`, `CATG_ID`, `AGE_BRKT_ID`, `PRDT_STS_ID` (lookup), `SLUG` varchar(80), `CARE_NOTE` varchar(500), `PUBL_DTE`; unique (`GNDR_ID`,`CATG_ID`,`SLUG`) |
| `PRDT_STS_LKP` | Draft, Published, Unpublished, Archived | standard lookup shape |
| `PRDT_VRNT` | the planned size × colour and its price | `PRDT_ID`, `CLR_ID`, `SIZE_ID`, `PRIC` numeric(12,2) null, `CMPR_PRIC` numeric(12,2) null; unique (`PRDT_ID`,`CLR_ID`,`SIZE_ID`); check `CMPR_PRIC` null or > `PRIC` and `PRIC` > 0 |
| `PRDT_PRIC_HIST` | append-only price history (DB trigger refuses update/delete, as the stock ledger) | `PRDT_VRNT_ID`, `OLD_PRIC`, `NEW_PRIC`, `OLD_CMPR_PRIC`, `NEW_CMPR_PRIC`, `INSR_*` |
| `VRNT_LBL` | read-side copy of Labelling facts (ADR 0014) | `LBL_VRNT_ID` (unique), `STYL_ID`, `CLR_ID`, `SIZE_ID`, `SKU_CODE`, `GTIN`, `GTIN_VOID_IND` |
| `PRDT_COLN_MAP` | product ↔ collection | `PRDT_ID`, `COLN_ID`; unique pair |
| `PRDT_IMG` | image of a product or one colour | `PRDT_ID`, `CLR_ID` null, `FILE_KEY` (storage key), `CNTT_TYP`, `FILE_BYT`, `WDTH`, `HGHT`, `ALT` varchar(125) null, `SORT_SEQ` |
| `PRDT_CMPS` | composition rows | `PRDT_ID`, `FIBR_ID`, `PCT` numeric(4,1); unique (`PRDT_ID`,`FIBR_ID`); check 0 < `PCT` ≤ 100 |
| `PRDT_CARE_MAP` | care instructions of a product | `PRDT_ID`, `CARE_ID`; unique pair |
| `CHRT_MAIN` | a size chart | `CATG_ID`, `GNDR_ID` (Unisex = the fallback), `NAME`; unique (`CATG_ID`,`GNDR_ID`) |
| `CHRT_LINE` | a row of a chart | `CHRT_ID`, `SIZE_ID`, `AGE_MIN_MON`, `AGE_MAX_MON`, `HGHT_MIN_CM`, `HGHT_MAX_CM`, `WGHT_MIN_KG`, `WGHT_MAX_KG`, `CHST_CM`, `WAIS_CM`, `LEN_CM` (all optional numeric); unique (`CHRT_ID`,`SIZE_ID`) |
| `PRDT_SLUG_HIST` | every path a product has had | `PRDT_ID`, `GNDR_ID`, `CATG_ID`, `SLUG`; unique triple (ADR 0013) |
| `OUTB_MSG`, `INBX` | outbox and inbox (exist for Catalog or added: Catalog now has a consumer) | standard |

**Reference additions (schema `REF`, seeded by migration):** `COLN_LKP` (collections: New Arrivals, Essentials, Sale, …; names for the owner), `FIBR_LKP` (fibres), `CARE_LKP` (care instructions: `CODE`, `NAME`, `SYMB_CODE` = the ISO 3758 symbol key, `SORT_SEQ`, `ACT_IND`), and new rows in `SIZE_LKP` for 8-9Y … 11-12Y once the labels are confirmed (Q-CHT-1). The existing age-bracket rows are kept; Newborn and Infant are deactivated (they are outside 1-12Y) and Tween is renamed to Junior only when the owner confirms the band names (Q-AGE-2). Lookups are seeded through migrations; no code edits for new values.

**Denormalisation:** none in the transactional tables. The storefront read model (S9) denormalises on its own side.

**Growth:** products ≈ hundreds, variants ≈ thousands, images ≈ tens of thousands: no partitioning needed; the path lookup is a single indexed read on the unique triple.

**Migration:** one EF migration `AddProducts` (Catalog), one `AddCatalogLookups` (Reference), plus raw SQL (quoted identifiers) for the price-history immutability trigger and the published-product alt-text trigger.

## Publish gate (shared)

`PublishProduct` runs the gates in one place and returns the **complete** list of failures (PRD-4), each with a stable code, a plain-words message, and a pointer to the tab that fixes it:

| Gate | Rule | Source |
|---|---|---|
| Content | name, description, gender, category, age bracket present | FR-53 |
| Variants | at least one sellable variant (issued GTIN, not voided, priced) | D1, Q-PRD-7 |
| Price | every sellable variant priced > 0; compare-at, when set, higher than price | D4 |
| Images | at least one image; every image has valid alt text | SEO-07 |
| Size chart | none required to publish (CHT-3); the product reports "no chart" | SCRUM-28 AC-4 |
| Composition | rows total exactly 100% | SCRUM-29 AC-2 |
| Care | at least one care instruction | SCRUM-29 |
| URL | slug valid and unique, path not reserved | SEO-02 |

Publish writes the status, `PUBL_DTE`, a `PRDT_SLUG_HIST` row (if new) and `ProductPublishedEvent` in one transaction. Unpublish and archive write their events the same way. Concurrency: the product row carries the token; publishing with a stale token returns 409.

## Settings (`Catalog:` section, validated at startup)

`MaxImageBytes` (5 MiB), `MaxImagesPerProduct` (10), `AllowedImageTypes` (jpeg, png, webp), `MaxSlugLength` (80), `ReservedSlugs`. Defaults live in code, bound from configuration, never hard-coded in handlers.

## Image handling

`IFileStorage` (ADR 0008) stores the original under a server-generated key (`catalog/products/{productId}/{guid}.{ext}`); the client file name is never used. Type is decided by the file's leading bytes (JPEG `FFD8FF`, PNG signature, WebP `RIFF…WEBP`), not by extension or the declared content type. Width and height are read from the header with a small parser in the Domain-free Infrastructure layer (no image library, so no new dependency or licence question). Files that fail sniffing, are larger than the limit, or whose dimensions cannot be read are refused. Derivatives wait for S9 (D5).

## Web (admin app)

Routes under `catalog`: `products` (paged list with status/category/search filters), `products/:id` (tabs: Content, Variants and prices, Images, Size chart, Composition and care, URL, Publish), `size-charts` (list and editor). All in `features/catalog/` with containers, components, NgRx store, models, services; semantic HTML (one `h1`, `table` with `th scope`, labelled controls, `role=alert`/`status` banners), mobile-first; the existing `AppShell` navigation gains **Catalog → Products / Size charts** and the footer reads "Sprint 7 (Publish)". The Publish tab lists each failed gate as a link to its tab.

## Testing

Domain unit tests (slug policy, path rules, composition total, alt-text validator, price rules, gate evaluator); handler tests against PostgreSQL via Testcontainers (publish gate with each failure, partial variants, concurrency, idempotent Labelling events delivered twice and out of order, history trigger, alt-text trigger); HTTP integration tests per feature; architecture tests (`CatalogFeatureDependencyTests`, naming of the new tables, boundary allowance for `Catalog.Infrastructure → Labelling.Contracts`); web unit tests for the stores and components; Playwright journeys (create product from a labelled style, fill the tabs, publish with a failure first, change a slug and see the redirect result); the new screens added to the seven-viewport layout suite. Every test is tagged with its AC ids and BRD ids in `traceability.md`.

## Security & coupling review (sprint level)

Per-feature rows are in each feature plan; this table covers what is shared.

| Area | How Sprint 7 meets it |
|---|---|
| Authorization (policy per endpoint, ownership SEC-05, roles SEC-08) | All product, media, chart and URL endpoints are staff endpoints created with `MapSecuredGroup` and a new `Policies.Catalogue` (RequireRole "Catalogue", "Admin"; capability-shaped, Admin-equivalent until roles exist in S8). No customer resource ids exist in this sprint. A policy test like `InventoryPolicyTests` pins it; `EndpointSecurityTests` covers the new groups. |
| Input validation | DTOs separate from entities; lengths and ranges from the spec; slugs and alt text validated by pure domain types; ids validated through the Reference contract; page size clamped to 100. |
| Output encoding | Alt text and descriptions are stored as plain text and HTML-escaped by Angular when rendered; JSON only from the API. No HTML accepted in descriptions (plain text with line breaks). File storage keys are generated, never from user input. |
| Outbound calls | None. The Labelling backfill is an in-process contract call. |
| Resource limits | Upload size and count settings above; request body limit set on the upload endpoint to `MaxImageBytes` + overhead; page size ≤ 100; strict rate-limit policy on upload and publish. |
| Concurrency | Concurrency token on the product; unique constraints on slugs and variant triples; conditional update on publish status; upserts for `VRNT_LBL` by unique Labelling id. |
| Idempotency | Labelling events handled through the inbox (`INBX`) and an idempotent upsert; creating a product for a style that already has one returns the existing product (409 with its id). |
| Audit | Audit columns on every table; price history append-only with a trigger; publish/unpublish/archive record who and when; the acting person is required on every write (same pattern as S5/S6 until authentication). |
| Data protection | Money is `numeric(12,2)` PKR and `decimal` in code; no personal data and no child data in products, events or logs; images are product photos only (child photos are Phase 2 and not accepted here). |
| Secrets and dangerous configuration | None new. The settings above are validated at startup (bad values stop the host). |
| Error responses | Problem details only; gate failures use a typed list, not stack traces; storage errors map to a generic 500 with a trace id. |
| New dependencies | None (image header parsing is ~100 lines; no ImageSharp). |
| Coupling | Contracts only (table above); consumed Labelling events are mapped to Catalog's own record at the edge (`VRNT_LBL`); the Reference lookups are read through a contract with a fake for tests; the storage port has a local implementation and an in-memory fake. |
| Abuse cases | (1) A renamed `.exe` or oversized/zip-bomb-like file uploaded as an image: refused by byte sniffing and size limits. (2) A slug crafted to shadow `/cart` or `/admin`, or containing `../` or Unicode look-alikes: normalised to ASCII, reserved words refused. (3) Two editors publishing/unpublishing at once, or a replayed Labelling event after a void: concurrency token and idempotent upsert keep one consistent state. (4) A script setting a negative or absurd price or a compare-at below price: database check constraints and domain rules refuse it. (5) Alt text stuffed with keywords or a script tag: quality rules reject repeats; Angular escapes on render. |
