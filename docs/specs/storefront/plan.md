# Storefront (Sprint 9, Sell: browse): module design

- **Module README:** [README.md](README.md) (decisions P-1..P-5, B-1, D-1..D-7)
- **Features:** [published-catalog](published-catalog/plan.md) · [navigation](navigation/plan.md) · [listings](listings/plan.md) · [search](search/plan.md) · [product-pages](product-pages/plan.md) · [seo](seo/plan.md)
- **ADRs:** [0018 storefront module, read model and edge](../../adr/0018-storefront-module-read-model-and-edge.md) (proposed) · [0002 modular monolith](../../adr/0002-modular-monolith.md) · [0004 outbox](../../adr/0004-postgres-transactional-outbox.md) · [0008 shared storage](../../adr/0008-production-vendor-coordination-and-shared-storage.md) · [0011 security baseline](../../adr/0011-security-and-coupling-baseline.md) · [0013 URLs](../../adr/0013-product-url-convention.md) · [0014 variants](../../adr/0014-product-variants-from-labelling.md) · [0017 staging](../../adr/0017-staging-topology-and-deploy-pipeline.md)
- **Starts after:** Sprint 8 merged to `main` (D-7). The S9 branch is cut from `main`.

This file holds what the six features share. Each feature plan holds its use cases, endpoints, data and its own security review.

## Request path

```
Browser ──HTTPS──> Cloudflare (WAF, rate rules, 60 s HTML cache, Access on staging)
        ──> nginx (realip from CF ranges only; /media static; TLS)
        ──> Storefront SSR server (Angular 22, Node)  ── internal network ──> Romp.Api  /api/storefront/*  (anonymous, not published)
                                                                              │
                                                                              └─ Storefront module ── STFR read model (PostgreSQL)
```

Admin traffic keeps its own server block (`admin.<domain>`); the storefront takes the bare domain (ADR 0017). Only the SSR server calls `/api/storefront/*`; the proxy does not forward that path to the outside.

## Module layout (same names in every tree, per the constitution)

New projects `Romp.Modules.Storefront.{Domain,Application,Infrastructure}` (no `Contracts`: nobody consumes the module). Added to `ModuleNames` in `Romp.ArchitectureTests`, to the CONSTITUTION module table (`storefront` · Public shop read model · `STFR`), to `docs/db/naming.md` §4, and to the spec index (done).

| Feature | API folder (all layers) | Web (`src/web/projects/storefront/src/app/features/storefront/`) | e2e |
|---|---|---|---|
| published-catalog | `PublishedCatalog/` (projection, consumers, reconciler, rebuild, image worker) | none (hosted by no screen; its card says so) | `e2e/storefront/published-catalog.e2e.spec.ts` (publish → appears) |
| navigation | `Navigation/` | `navigation/` (header, mega-menu, mobile menu, breadcrumbs, home) | `navigation.e2e.spec.ts` |
| listings | `Listings/` | `listings/` | `listings.e2e.spec.ts` |
| search | `Search/` (+ admin endpoints for synonyms and misses) | `search/` (+ admin app `features/storefront/search`) | `search.e2e.spec.ts` |
| product-pages | `ProductPages/` | `product-pages/` | `product-pages.e2e.spec.ts` |
| seo | `Seo/` (sitemap entries) | `seo/` (SeoService, templates, JSON-LD, status, headers) | `seo.e2e.spec.ts`, Lighthouse CI |

Dependencies inside Storefront are one-way: `PublishedCatalog` (writes the model) ← `Navigation`, `Listings`, `Search`, `ProductPages`, `Seo` (read it through `IStorefrontReadDbContext`, never each other's internals). A new `StorefrontFeatureDependencyTests` enforces it like `WarehouseFeatureDependencyTests`. Web: the same direction; `seo` is imported by the page features, never the reverse of its models.

The storefront web app currently has no `features/` tree; the plan adds one by CONVENTIONS.md §1 (containers, components, models, services, NgRx store, utils, barrel). Whether `StructureSyncTests` rule 3 (web feature must match a backend feature) scans the storefront app is checked in task G1; if it only scans admin, the rule is extended to both apps in the same change.

## Contract additions in other modules (small, additive, no existing consumer changes)

| Module | Addition | Why |
|---|---|---|
| Catalog.Contracts | `IPublishedProductQueries`: `GetSnapshotAsync(productId)` (null when not published), `ListVersionsAsync(afterId, take)` → (id, version) for reconciliation, `ListSnapshotsAsync(afterId, take)` for rebuild. `PublishedProductSnapshot` carries version, path, name, description, gender/category/age/collections (ids and codes), style code, per-variant colour, size, SKU, GTIN-issued flag, price, compare-at price, images (storage key, size, alt, colour), composition, care, care note, publish date | the read model's only source |
| Catalog (internal) | `PRDT_MAIN.CNTT_VER bigint` bumped in the same transaction as any change that emits an event (also images, composition, care, price, variant becoming sellable); events gain an additive `Version` field (schema v1 allows additive fields, ADR 0011). A migration backfills 1 | version-wins rule (P-1) |
| Catalog.Contracts | `ISizeChartQueries`: `ListChartsAsync(afterId, take)` and `GetChartAsync(categoryId, genderId)` (with the Unisex fallback already resolved by Catalog) | size chart on the page |
| Warehouse.Contracts | `IStockAvailabilityQueries`: `ListAvailabilityAsync(afterVariantId, take)` → (variantId, skuCode, available, version) and `GetAsync(variantId)`; `StockLevelChangedEvent` and `StockPutawayConfirmedEvent` gain additive `Version` (the ledger sequence of the variant's latest entry) | availability, version-wins |
| Reference.Contracts | `ICatalogLookupQueries` gains `ListGendersAsync`, `ListCategoriesAsync`, `ListAgeBracketsAsync`, `ListCollectionsAsync`, `ListSizesAsync`, `ListColoursAsync` (active first, with `SORT_SEQ`); colour swatch value if the owner confirms (Q-LST-6) | menu and filter options |

Reference has no events: the lookup copies on product rows are refreshed by the 10-minute reconciler (a lookup name change reaches the pages within ten minutes) and the menu reads the lookups through a 60 s cache.

## Data overview (schema `STFR`, names per `docs/db/naming.md`; details in each feature plan)

All rows are projections, so the audit columns `INSR_*`/`UPDT_*` and `xmin` (concurrency token, per naming.md) exist, but the keys are the producers' ids (not identity) and the writer is the projection only. Denormalised on purpose (CLAUDE.md allows it in read models; reason recorded here): names, codes, prices and availability are copied onto product and variant rows so a listing is one indexed query. No partitioning: ≈ hundreds of products, thousands of variants, tens of thousands of images; the only growing table is `SRCH_MISS`, one row per distinct query per day, purged after 180 days.

Glossary words to add in the implementing PR (checked against the glossary 2026-10-25): `AVLB` (available), `DRV` (derivative), `SYNM` (synonym), `MISS` (a zero-result query), `RBLD` (rebuild), `QRY` (query), `RDY` (ready), `SWCH` (swatch), `MODL` (model, as in the `MODL_META` table). Already in the glossary and reused: `STFR` schema, `SRCH`, `HASH`, `VER` (version, so no `VRSN`), `VSBL`, `RCNL` (reconciliation, so no `RECN`), `PRDT`, `VRNT`, `IMG`, `CMPS`, `CARE`, `CHRT`, `COLN`, `CATG`, `GNDR`, `AGE`, `SKU`, `PRIC`, `CMPR`.

## API (host `Romp.Api`, route group `/api/storefront`)

Every endpoint is explicitly anonymous (a named `Storefront.Public` policy, so the host's "every endpoint declares a policy" check passes), read-only (GET), returns `ProblemDetails` for errors, caps `pageSize` at 48 and query length at 100, and sets `Cache-Control: no-store` (the SSR server decides edge caching). Endpoint tables are in the feature plans. Operator and staff endpoints (rebuild, synonyms, misses) are separate (`/api/admin/storefront/*`) with permission policies from Sprint 8.

## Settings (`Storefront:` section, validated at start-up; defaults in code, bound from configuration)

`ImageWidths` (320, 640, 960, 1280), `JpegQuality`/`WebpQuality`, `MaxImagePixels`, `ImageWorkerConcurrency` (2), `LowStockThreshold` (5), `NewArrivalsDays` (30), `PageSize` (24, max 48), `SuggestLimit` (8), `QueryMaxLength` (100), `ReconcileInterval` (10 min), `MissRetentionDays` (180), `MenuCacheSeconds` (60). SSR-server settings (environment): `ROMP_PUBLIC_HOST` (→ `Site:PublicBaseUrl`), `BRAND_*`, `STOREFRONT_API_URL`, `EDGE_CACHE_SECONDS` (60), `TRUSTED_PROXY`, `NOINDEX` (true on staging), and the rate-limit numbers.

## Build, test and deploy

- **Web:** `ng build storefront` produces a Node server bundle; `Dockerfile.storefront` already exists and is updated to run it; the compose files gain a `storefront` service (ADR 0017); nginx gets a bare-domain server block (storefront) and a `/media` location.
- **Tests:** domain unit tests (query normalisation, availability classes, canonical rule, meta templates, JSON-LD escaping); handler tests against PostgreSQL via Testcontainers (projection with duplicate/out-of-order/lost events, rebuild with a sweep, listing filters with same-variant rule, FTS and trigram, SKU lookup); HTTP integration tests per endpoint (anonymous allowed, bad input 400, never a stack trace); architecture tests (`StorefrontFeatureDependencyTests`, naming of `STFR`, no reference to other modules' internals, Contracts-only); web unit tests (stores, templates, SeoService); Playwright e2e per feature plus the seven-viewport layout suite for the new pages; a no-JavaScript crawler test (fetch HTML, assert content/tags/JSON-LD); Lighthouse CI. Every test carries `[Trait("Spec", …)]` IDs; `traceability.md` is written during implementation.
- **Machine note:** API integration tests and the native image library run only in the Linux container/CI (Windows Application Control, CLAUDE.md); results will say so.

## Risks

| Risk | Mitigation |
|---|---|
| S9 starts from `main` only after S7 and S8 merge | specs and plans are written now; no code before the merge (D-7) |
| Version fields touch Catalog and Warehouse | additive; backfill migration; consumers already tolerate additive fields (ADR 0011) |
| Lighthouse numbers on a shared CI runner vary | three runs per page, median; budgets with a small documented tolerance only for TBT/INP lab noise; fixture is small and deterministic |
| Native image library behaviour differs on Linux vs Windows | all image tests run in the Linux container; port + fake for every other test |
| Edge cache serves a sold-out size for up to a minute | accepted (P-1/P-2); the cart re-checks (S11); copy never says "reserved" (PDP-17) |
| SEO-06 pagination wording | canonical rule is one function behind one setting (Q-SEO-2) |

## Security & coupling review (sprint level)

Per-feature tables are in each feature plan; this one covers what is shared.

| Area | How the module meets it |
|---|---|
| Authorization | Public read endpoints are explicitly anonymous and GET only; staff endpoints use Sprint 8 permission policies; the API is not published outside the internal network, so anonymous API access is only through the SSR server |
| Input validation | Query strings parsed into typed filter objects with allow-lists (sort keys, codes from lookups, numeric ranges, clamped page); unknown values ignored or 400; text normalised and capped; DTOs, never entities |
| Output encoding | Angular template encoding for HTML; JSON-LD serialised with `<`, `>`, `&`, U+2028/2029 escaped; meta via the `Meta` service; XML via an XML writer; SQL parameterised only (FTS query built from sanitised tokens as a parameter) |
| Outbound calls | None from the API. The SSR server calls only `STOREFRONT_API_URL` (config, internal). The image worker reads and writes `IFileStorage` only |
| Resource limits | Page size ≤ 48, query ≤ 100 chars, ≤ 8 search tokens, ≤ 8 suggestions, pixel cap on decode, worker concurrency, SSR rate limit per real client IP, Cloudflare rules in front |
| Concurrency | Version-wins upserts (`WHERE excluded.VER > current.VER`), unique keys, rebuild stamps by run id, reconciler and consumers cannot regress a row |
| Idempotency | Inbox for consumed events; snapshots are idempotent by version; worker rows claimed with `SKIP LOCKED`, derivative names are content hashes |
| Audit | The read model is derived and not audited; staff edits of synonyms go through the Sprint 8 audit trail; rebuild runs are recorded with actor `operator-cli` |
| Data protection | No personal data in this module; search misses hold query text only (no IP, account or session), redacted if they look like an email or phone number; prices are `numeric(12,2)` / `decimal` |
| Secrets and dangerous configuration | No secrets added; `NOINDEX`, `TRUSTED_PROXY` and brand settings validated at start-up; production start refuses `NOINDEX=true` with a production host configuration (and staging refuses to start without the Access/basic-auth switch recorded) |
| Error responses | ProblemDetails only; SSR error pages show no internals; 404/410 pages are real status codes |
| New dependencies | SkiaSharp + native assets (MIT) in Infrastructure; `@lhci/cli` (Apache-2.0, dev only); no runtime npm dependency for rate limiting or sitemaps. Versions pinned in `Directory.Packages.props` / `package.json` at implementation, licences recorded |
| Coupling | Producers' `*.Contracts` only; events consumed through mappers in `Storefront.Application`; ports with fakes for storage, resizer, clock; no cross-module table reads (architecture tests) |
| Abuse cases | Scraping and search flooding (Cloudflare + SSR limits, capped pages and queries); query-string cache busting (only known parameters, others dropped before the cache key and a canonical to the clean URL); oversized or hostile image uploaded by staff earlier (decode caps, no request-time work); forged `X-Forwarded-For`/`CF-Connecting-IP` (trusted only from the proxy); product enumeration of unpublished items (unpublished reads as 404, SKU lookup excludes them) |
