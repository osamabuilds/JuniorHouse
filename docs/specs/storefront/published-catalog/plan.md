# SCRUM-203: Published catalog: technical plan

- **Spec:** [spec.md](spec.md) · **Module plan:** [../plan.md](../plan.md) · **ADR:** [0018](../../../adr/0018-storefront-module-read-model-and-edge.md)
- **Module:** Storefront (consumes Catalog, Warehouse, Reference contracts)

## Domain model
Pure types in `Storefront.Domain/PublishedCatalog`:
- `VersionedSnapshot` rule: a snapshot replaces stored state only if `Version > stored.Version` (equal or lower is a no-op).
- `AvailabilityClass` from quantity and the configured threshold: `InStock`, `LowStock`, `SoldOut`, `NotYetAvailable` (no stock event yet or variant not sellable, PCT-9). Quantity is never exposed.
- `ProductVisibility`: a product is **listable** when it has at least one in-stock sellable variant and its primary image is ready; **page-visible** whenever it is published (D-2).
- `PriceSummary`: lowest and highest available variant price, on-sale flag (any variant compare-at above price), biggest saving percentage.
- `DerivativeName`: `{contentHash}-{width}.{webp|jpg}`; the hash is of the original's bytes.

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `ApplyProductSnapshotCommand` | product id (from event, reconcile or rebuild) | upserts product, variants, images, composition, care, collections; or removes the product if the snapshot is null; queues derivative rows | PCT-1, 2, 4, 5 |
| `ApplyAvailabilityCommand` | variant id | upserts availability with version rule; recomputes the product's flags (`IN_STK_IND`, `LSTB_IND`) | PCT-3, 5, 9, 11 |
| `ApplySizeChartCommand` | category id, gender id | upserts the chart | PCT-1 (size chart) |
| `ReconcileCommand` | none (job, every 10 min) | pages `(id, version)` from Catalog and Warehouse, refetches missing/older, removes unknown; refreshes lookup copies | PCT-5, 6 |
| `RebuildStorefrontCommand` | run id | pages every snapshot, upserts with the run id, sweeps rows of other runs, writes a `RBLD_RUN` row | PCT-6 |
| `ProcessDerivativeJob` | none (worker loop) | claims `PEND` derivative rows, reads the original from `IFileStorage`, resizes via `IImageResizer`, writes hashed files, marks `DONE`/`FAIL` (attempt count, backoff), then re-evaluates the product's `IMG_RDY_IND` | PCT-7, 10 |
| `SweepDerivativesJob` | none (daily) | deletes derivative files no row references after the cache lifetime | PCT-10 |

Event consumers (in `Storefront.Application` via mappers, ADR 0011): `ProductPublished/Changed/Unpublished/ArchivedEvent` → `ApplyProductSnapshot` (the snapshot is null for unpublished and archived, which removes it); `SizeChartChangedEvent` → `ApplySizeChart`; `StockLevelChangedEvent`, `StockPutawayConfirmedEvent` → `ApplyAvailability`. Each handler is idempotent by version and recorded in the inbox.

## API endpoints
| Method | Route | Auth / role | Notes |
|---|---|---|---|
| POST | `/api/admin/storefront/rebuild` | permission `storefront.operate` (Owner), added to Sprint 8's permission lookups | starts a rebuild run, returns the run id; 202 |
| GET | `/api/admin/storefront/rebuild/{runId}` | same | status and counts |
| n/a | operator CLI mode `rebuild-storefront` | operator on the server | same command, audit actor `operator-cli`; also run automatically when the stored read-model schema version differs from the code's |

Public read endpoints are in the other feature plans. Ownership check (SEC-05): n/a, no customer-owned resource.

## Events
| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|
| `Product*Event`, `SizeChartChangedEvent` (+ additive `Version`) | Catalog | Storefront | yes (existing) |
| `StockLevelChangedEvent`, `StockPutawayConfirmedEvent` (+ additive `Version`) | Warehouse | Storefront | yes (existing) |
| none | Storefront | n/a | Storefront publishes no events in S9 |

## Data
Schema `STFR` (as built, Sprint 9a task 9; names follow the glossary, so several differ from the first draft of this plan, noted below). Keys that come from a producer are never generated here; child rows have identity keys. Every table has `INSR_DTE/INSR_BY`, `UPDT_DTE/UPDT_BY` and the `xmin` concurrency token. Version columns carry the producer's own name: `CNTT_VER` (Catalog's content version) and `STCK_VER` (Warehouse's stock version).

| Table | Purpose | Key columns |
|---|---|---|
| `PRDT_MAIN` | the product | `ID` (Catalog product id), `CNTT_VER`, `PATH` varchar(200) unique, `NAME`, `DSCR`, `STYL_CODE`, `GNDR_CODE/NAME`, `CATG_CODE/NAME`, `AGE_CODE/NAME`, `PUBL_DTE`, `CARE_NOTE`, `MIN_PRIC`, `MAX_PRIC` numeric(12,2) null, `SALE_IND`, `MAX_DISC_PCT` numeric(5,2), `AVLB_IND` (some variant can be bought), `VSBL_IND` (listable), `IMG_RDY_IND`, `RBLD_RUN_ID` (last rebuild run that touched the row); indexes on (`GNDR_CODE`,`CATG_CODE`,`PUBL_DTE` desc), (`VSBL_IND`,`PUBL_DTE` desc), `MIN_PRIC`, `RBLD_RUN_ID`; checks on price order and discount range |
| `PRDT_VRNT` | variant | `ID` (Catalog variant id), `PRDT_ID`, `CLR_ID/CODE/NAME`, `SIZE_ID/CODE/NAME`, `SIZE_SEQ`, `SKU_CODE`, `SKU_NORM` (lower, trimmed; unique where not null), `PRIC`, `CMPR_PRIC`, `SELL_IND`, `AVLB_QTY` (internal, never published; null until a stock row arrives), `STCK_VER`; indexes (`PRDT_ID`), (`SIZE_CODE`,`CLR_CODE`,`PRDT_ID`) where `AVLB_QTY` > 0; checks: price above 0, compare-at above price, quantity not negative |
| `PRDT_IMG` | product/colour images | `ID` (Catalog image id), `PRDT_ID`, `CLR_ID` null, `FILE_KEY` (the original), `HASH` (set by the image worker), `WDTH`, `HGHT`, `ALT`, `SORT_SEQ`, `MAIN_IND` |
| `IMG_DRV` | derivatives, also the job queue | `ID`, `IMG_ID`, `WDTH`, `DRV_FMT_ID` (`DRV_FMT_LKP`: WEBP, JPG), `DRV_STS_ID` (`DRV_STS_LKP`: Pending, Done, Failed), `FILE_KEY`, `ATMP_QTY`, `NXT_ATMP_DTE`, `ERR_CODE`; unique (`IMG_ID`,`WDTH`,`DRV_FMT_ID`); index (`DRV_STS_ID`,`NXT_ATMP_DTE`) |
| `PRDT_CMPS`, `PRDT_CARE`, `PRDT_COLN_MAP` | composition (fibre, share), care instructions (with symbol), collections | child rows of `PRDT_MAIN`, deleted with it |
| `CHRT_MAIN`, `CHRT_LINE` | size charts per (category, gender) | `ID` (Catalog chart id), `CNTT_VER`; unique (`CATG_CODE`,`GNDR_CODE`); lines as Catalog plus `SIZE_CODE/NAME/SEQ` |
| `AVLB_STK` | the stock Warehouse reported, kept apart because a stock message can arrive before its product | `ID` (Warehouse's variant id), `SKU_CODE` unique, `QTY`, `STCK_VER`. Joins to a variant **by SKU**: Catalog and Warehouse use different variant ids |
| `RBLD_RUN` | rebuild runs | `ID`, `RBLD_STS_ID` (`RBLD_STS_LKP`: Running, Completed, Failed), `STRT_DTE`, `END_DTE`, `PRDT_CNT`, `CHRT_CNT`, `STCK_CNT`, `ACTR` |
| `MODL_META` | the read model's own metadata: its schema version (a rebuild runs when it differs from the code's; 0 = never built) and when the lookup copies were last refreshed | one row, enforced by a check |
| `INBX` | consumed events | standard |

**Differences from the first draft:** no colour swatch column (see the module plan); `VER`/`VRSN` became `CNTT_VER`/`STCK_VER`; `IN_STK_IND`/`LSTB_IND`/`PRMR_IND`/`SRC_KEY`/`NEXT_ATMP_DTE` became `AVLB_IND`/`VSBL_IND`/`MAIN_IND`/`FILE_KEY`/`NXT_ATMP_DTE` (glossary words); the derivative format is a lookup (`DRV_FMT_LKP`) rather than free text, per the database rules; **the search vector and trigram columns and the `pg_trgm` extension move to the search feature's own migration (S9b)**, because the text-search configuration is a search decision.

Migration `AddPublishedCatalogReadModel` (tables, indexes, checks, lookup seeds, the `MODL_META` row).

## NFR / security design
Constant query counts (one product row + children by `Contains`); listing reads touch `PRDT_MAIN` and at most one `EXISTS` on `PRDT_VRNT`; `IMG_DRV` queue claims use `FOR UPDATE SKIP LOCKED` with a visibility window; files are written to a temporary key then renamed, so a half-written file is never served; the worker never runs inside a request (PCT-7); decode refuses images above `MaxImagePixels`; metadata stripped; failures retry with backoff and surface in the readiness check as a count (not a failure).

## Frontend
No screens. The SSR media route `/media/{hash}-{width}.{ext}` is served by nginx from the storage volume with immutable caching; the page components pick a `srcset` from `IMG_DRV` rows.

## Risks & alternatives considered
Rebuild-by-sweep is chosen over truncate-and-reload because the old rows keep serving. The version rule needs the additive producer fields (module plan). A read model in Redis was rejected (Q-PCT-4 default): it cannot be filtered and searched without a second store.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | Rebuild endpoints require `storefront.operate` (Owner); no public endpoint writes anything; operator CLI needs server access |
| Input validation | Event payloads are ids and versions only; snapshots are typed DTOs from the contract; file keys come from the snapshot and are never built from client input; derivative names are generated |
| Output encoding | Stored text is plain; encoded at render (SSR); file paths built only from hash + width + format |
| Outbound calls | None; storage via `IFileStorage` |
| Resource limits | Page size 200 for rebuild, worker concurrency 2, pixel cap, derivative count capped by `ImageWidths` × 2, attempt cap with backoff |
| Concurrency | Version-wins `INSERT … ON CONFLICT DO UPDATE … WHERE excluded.VER > PRDT_MAIN.VER`; availability likewise; rebuild stamps `RBLD_ID` |
| Idempotency | Inbox plus version rule; the same event 10 times gives one state; worker claim by `SKIP LOCKED` |
| Audit | Rebuild runs recorded with actor; derived data not audited |
| Data protection | No personal data; prices decimal; exact stock quantity stored but never returned by any public endpoint (test) |
| Secrets and dangerous configuration | None; image limits validated at start-up |
| Error responses | Jobs log structured errors without payloads; admin endpoints ProblemDetails |
| New dependencies | SkiaSharp + native assets (MIT), behind `IImageResizer`; recorded in `Directory.Packages.props` |
| Coupling | Reads Catalog, Warehouse, Reference only via `*.Contracts`; consumers use mappers; fakes for storage, resizer, clock |
| Abuse cases | A staff-uploaded image bomb (pixel cap, header-sniffed type); event storm after a bulk price change (version rule, batched upserts); lost event after a crash (reconciler); forged event (events are internal, outbox only) |
