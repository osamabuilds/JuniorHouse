# Product recommendations: technical plan

- **Spec:** [spec.md](spec.md) (REC-1..14)
- **ADR:** [0029](../../../adr/0029-product-recommendations-blended-score.md) (precomputed blended score, meaning provider as an optional port)
- **Module(s):** Storefront (API feature `ProductRecommendations`, web feature `product-pages`), plus one read-only contract in Orders
- **Jira:** SCRUM-218
- **Owner decisions applied (2026-11-08):** blend field, meaning and sales scores, with the meaning part off until a provider is named (Q-REC-1, Q-REC-4 deferred); hot sellers = units in non-cancelled orders over 14 days (Q-REC-2); same gender and age group come from the read model (Q-REC-3).

## Domain model

All in `Romp.Modules.Storefront.Domain/ProductRecommendations`. Pure, no EF or ASP.NET.

- **`SimilarityBlend`** (static, pure): `Blend(parts, weights)`. Each part is a nullable 0..1 score; a null part is dropped and the remaining weights are renormalised so the result stays in 0..1. Invariant: weights are non-negative and at least one is positive; the result is clamped to 0..1.
- **`FieldSimilarity`** (static, pure): `Score(a, b)` = 0.5 × same category + 0.3 × colour overlap (Jaccard over colour codes) + 0.2 × price closeness (`1 − min(1, |ln(pa / pb)|)`, using each product's lowest price). Inputs are the small `SimilarityProfile` record (category code, colour codes, lowest price).
- **`SalesSignal`** (static, pure): `Normalise(unitsByProduct)` gives `log(1 + units) / log(1 + max)` within the candidate group, 0 when nobody sold.
- **`SimilarProduct`** (entity, `STFR.PRDT_SMLR`): the precomputed row: product, similar product, rank (1 = closest), score, computed-at. Invariants: rank 1..12, no row where product = similar product, one row per (product, rank) and per (product, similar product).
- **`ProductSaleDay`** (entity, `STFR.PRDT_SALE_DAY`): units sold of a product on one day (UTC date). One row per (product, day).
- **`RecommendationRun`** (entity, `STFR.RCMD_RUN`): one worker pass (started, ended, groups scored, products written). A run with no end date is unfinished; the worker uses the latest finished run as its watermark.
- **`RecommendationRow`** lookup (`STFR.RCMD_ROW_LKP`, codes `SIMILAR`, `TRENDING`): which row a tracked event came from (REC-12).
- **`RecommendationDay`** (entity, `STFR.RCMD_EVT_DAY`): clicks and adds per row, product and day; counters only, no person (REC-12).

Port (Application/Abstractions): **`ITextSimilarityProvider`** with `IsAvailable` and `ScoreAsync(productId, candidateIds) → IReadOnlyDictionary<long, double>?` (null = unavailable). Implementations: `NoTextSimilarityProvider` (default everywhere), `FakeTextSimilarityProvider` (tests). A real provider arrives with its own ADR (ADR 0029, point 4).

## Application layer

Folder `Romp.Modules.Storefront.Application/ProductRecommendations`.

| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `GetProductRecommendationsQuery` | product path | `ProductRecommendationsDto { Similar[], Trending[] }`, each a `ListingTileDto` (existing: sizes, `VariantId`, `SoldOut`, price, compare-at) | REC-1, 2, 4, 5, 6, 8, 10 |
| `RefreshSimilarProductsCommand` | none (worker) | `RecommendationRun`; scores each changed gender+age group | REC-2, 3, 5, 13 |
| `SyncProductSalesCommand` | none (worker) | rewrites the last 14 days of `PRDT_SALE_DAY` from `IProductSalesQueries` | REC-3, 4 |
| `RecordRecommendationEventCommand` | row code, product id, `click` or `add` | increments the day counter | REC-12 |

`GetProductRecommendationsQuery` steps, all bulk (no N+1, CONVENTIONS section 2):
1. Resolve the product by path (published, else the usual not-found).
2. Read its precomputed rows (at most 12) and keep those whose product is published and has at least one buyable variant (a join on the read model and `AVLB_STK`); take 4. Fewer than 4 is fine (REC-10); none means the row is omitted.
3. Trending: sum `PRDT_SALE_DAY` units for the last 14 days grouped by product, for products of the same gender and age group; exclude the current product, anything in the similar four, unpublished and sold-out; take 4, and if fewer than 4 fill from the whole shop by the same rule (REC-4).
4. Fetch both tile sets with **one** bulk tile query (a `GetTilesByIdsAsync` on `IStorefrontCatalogQueries` reusing `ListingTiles`, so sizes and `VariantId` for quick add are the same as in listings, REC-6, 7, 8).
5. Failures in step 2 or 3 are caught and logged without rethrowing; the answer then has empty rows (REC-10).

`RefreshSimilarProductsCommand` per group (same gender and age group):
1. Load the group's published products and their profiles (category, colours, lowest price) in one query.
2. Units sold per product from `PRDT_SALE_DAY`, normalised by `SalesSignal`.
3. If `ITextSimilarityProvider.IsAvailable`, ask it for meaning scores; otherwise the part is null.
4. For each product, score every other product in the group with `SimilarityBlend`, keep the top 12, and replace that product's rows in one transaction (delete then insert), so a page never sees a half-written list.
5. Which groups run: those with any product changed since the last finished run (by `UPDT_DTE`), plus every group once a day to pick up sales. A group is at most a few hundred products, so a pass is quadratic in a small number.

## API endpoints

| Method | Route | Auth / role | Ownership check (SEC-05) |
|---|---|---|---|
| GET | `/api/storefront/products/recommendations?path=` | Public (`Policies.Public`, like `by-path`) | n/a: public catalogue data, nothing owned by a caller |
| POST | `/api/storefront/products/recommendations/events` | Public, body size limit, rate limited | n/a: increments a counter, reads nothing |

Responses are problem details on error. `GET` has no cache beyond the browser's, because REC-5 needs sold-out removal at once and the queries are two indexed reads.

## Events

None. No new outbox events and no event consumers. (Units sold are pulled through a contract, not pushed through events, ADR 0029 point 5.)

Orders contract (new, read-only): `Romp.Modules.Orders.Contracts.IProductSalesQueries.UnitsSoldByProductAndDayAsync(DateOnly from, DateOnly to, ct) → IReadOnlyList<ProductDayUnits(long ProductId, DateOnly Day, int Units)>`. Implemented in Orders.Infrastructure over `ORDR_LINE` joined to the order, counting only orders whose status is not cancelled. Registered by the Orders module; Storefront takes only the interface.

## Data

Schema `STFR` (Storefront's own). New words for the glossary (`docs/db/naming.md`) added in the same PR if missing: similar `SMLR`, recommendation `RCMD`, score `SCOR`, event `EVT`, rank `RANK`, click `CLK`.

| Table | Columns (besides `ID` bigint identity and the audit columns `INSR_DTE`, `INSR_BY`, `UPDT_DTE`, `UPDT_BY`, concurrency token) | Keys and indexes |
|---|---|---|
| `STFR.PRDT_SMLR` | `PRDT_ID` bigint, `SMLR_PRDT_ID` bigint, `RANK_NO` smallint, `SCOR_VAL` numeric(7,6), `CALC_DTE` timestamptz | unique (`PRDT_ID`, `RANK_NO`); unique (`PRDT_ID`, `SMLR_PRDT_ID`); FKs to `PRDT_MAIN` on both columns, each indexed; check rank 1..12; check ids differ |
| `STFR.PRDT_SALE_DAY` | `PRDT_ID`, `SALE_DTE` date, `UNIT_QTY` int | unique (`PRDT_ID`, `SALE_DTE`); index on `SALE_DTE`; FK to `PRDT_MAIN`; check `UNIT_QTY` ≥ 0 |
| `STFR.RCMD_RUN` | `STRT_DTE`, `END_DTE` null, `GRP_CNT`, `PRDT_CNT` | index on `END_DTE` |
| `STFR.RCMD_ROW_LKP` | standard lookup shape (`ID` smallint, `CODE`, `NAME`, `DSCR`, `SORT_SEQ`, `ACT_IND`), seeded `SIMILAR`, `TRENDING` | unique `CODE` |
| `STFR.RCMD_EVT_DAY` | `ROW_ID` smallint, `PRDT_ID`, `EVT_DTE` date, `CLK_CNT` int, `ADD_CNT` int | unique (`ROW_ID`, `PRDT_ID`, `EVT_DTE`); FKs indexed |

- Normalisation: 3NF; the precomputed rows are a derived read table, not a transactional one (the reason a product's similar list is stored as rows, not a delimited column). Rows hold ids, never names; names, prices and sizes come from the live read model at read time, so they cannot go stale.
- Growth: `PRDT_SMLR` is at most 12 rows per product and `PRDT_SALE_DAY` at most 14 live days per product (older days are deleted by the sync), so neither needs partitioning. `RCMD_EVT_DAY` grows by at most rows × products per day; its partitioning key (`EVT_DTE`) is noted for later and nothing reads it yet.
- Concurrency: the refresh replaces a product's rows in one transaction; two overlapping runs end with the later one's rows because of the unique keys and delete-then-insert. Counters use `UPDATE ... SET CLK_CNT = CLK_CNT + 1` (atomic), with insert-or-update on the unique key.
- Migration: one EF migration `AddProductRecommendations` in the Storefront context; every table and column mapped explicitly (`ToTable`, `HasColumnName`), and the lookup seeded in the migration. The naming test covers it. No data migration: the first worker pass fills the tables.

## NFR / security design

| Constraint | How it is met |
|---|---|
| REC-9, SEO-01 | Both rows come from the server render; links are `<a href>` to the product paths; each row is a `<section aria-labelledby>` with an `h2`; descriptive link text is the product name |
| REC-10, NFR-FT-08 | Page request reads precomputed rows only; the meaning provider is never called at request time; every failure in the recommendation path is caught and yields absent rows, never an error on the product page |
| REC-11, NFR-01 | Two indexed reads and one bulk tile query; target p95 under 150 ms measured by a timed integration test on the seeded catalogue; cards render at the design's 4:5 size so nothing shifts |
| REC-5 | Read-time join to the live published and stock state, so removal is immediate; the worker only reorders |
| REC-13 | `SimilarityBlend` drops a missing meaning part and renormalises; the default provider reports unavailable, so the first release runs on fields and sales |
| SEC-09, FR-10 | Cards never carry an authoritative price into the cart: quick add posts the `VariantId`, and the cart recalculates price and checks stock on the server (existing behaviour, spec REC-7) |
| NFR-27..30 | Only catalogue ids and order *totals by product and day* cross the Orders contract; no order id, no customer, no child data |
| REC-12 | The event carries a row code, a product id and `click` or `add`; no cookie, IP or user id is stored; counters are not used for ranking |

## Frontend

Feature `src/web/projects/storefront/src/app/features/storefront/product-pages` (storefront conventions, CONVENTIONS section 1b; design from `docs/design-system/components/ProductRail`, `ProductCard`, `QuickAdd`).

- `components/product-recommendations/product-recommendations.component.{ts,html}`: presentational. Inputs `title`, `rowCode`, `tiles`; output `tileInteraction`. Renders `<section aria-labelledby>` → `h2` → the design's `ProductRail` list of existing `ProductTileComponent`s (imported from the `listings` barrel, which already exports it), with the Previous and Next arrow buttons and a keyboard-scrollable list (REC-14). Renders nothing when `tiles` is empty (no empty heading).
- Container `containers/product-page` adds the two sections under the details, after the composition and care block: "You may also like" then "Trending now" (so the page's `h1` and `h2` order is unbroken).
- `services/product-recommendations-api.service.ts`: `GET` the rows, errors mapped to empty rows. Loaded on the server render and carried to the browser through the existing state-transfer whitelist (ADR 0028) so hydration shows the same cards without a second request or a shift. Loaded **in parallel** with nothing the page needs: the product itself does not wait on it.
- Quick add: the tile already posts through the cart `Add` action with `quiet` (card quick add feature); a recommendation card passes its `rowCode` so the container also calls the events endpoint with `add`; the link click sends `click` with `navigator.sendBeacon`, browser only.
- `utils/recommendation-event.util.ts` (pure): builds the beacon body.
- Styles: tokens and `rp-` classes only; if the design's rail rules are not yet in `_components.scss`, port them from `ProductRail/README.md` and its preview, not new design.
- SEO: `JSON-LD` and the existing breadcrumb are unchanged. Cards add internal links between products, which is the §14.2 cross-linking the rules ask for.

## Risks & alternatives considered

| Risk | Answer |
|---|---|
| With no meaning provider the "AI powered" wish is not met in release 1 | Owner accepted starting with fields and sales (2026-11-08); the port and the blend are in place so adding a provider is a configuration change |
| Weights look wrong on real products | Weights are configuration; the refresh run records counts so a result can be inspected; tuning needs no code change |
| A small catalogue gives few candidates in a group | A product may have fewer than 4 similar rows; the row shows what exists; if there are none it is omitted; trending still fills from the whole shop |
| Orders contract adds coupling | One read-only interface in `Orders.Contracts` (the sanctioned route, CLAUDE.md); no table access across schemas |
| Cancelled orders change past days | The sync rewrites the full 14-day window each run, so a cancellation is picked up |
| Event counters can be inflated by a script | They feed no ranking and no money; rate limited and body capped; accepted |

Alternatives rejected are in ADR 0029 (request-time scoring, order events, pgvector now).

## Security & coupling review (required; a plan without it is not approved)
Rules and rationale: [ADR 0011](../../../adr/0011-security-and-coupling-baseline.md) and constitution section 6.

| Area | How this feature meets it |
|---|---|
| Authorization (policy per endpoint, ownership SEC-05, roles SEC-08) | Both endpoints are public read or counter-only (`Policies.Public`); no resource belongs to a caller, so there is no ownership check. No admin endpoint is added. |
| Input validation (type, range, length, charset; DTO ≠ entity) | `path` uses the existing slug validation (lowercase, hyphen, length cap); the event body is a closed set (`SIMILAR` or `TRENDING`, `click` or `add`) plus a positive product id; unknown values are a 400 problem detail. DTOs are separate from entities. |
| Output encoding (ZPL, PDF, SQL, HTML, file paths) | Tiles are plain DTOs rendered by Angular templates (escaped by default). Any raw SQL quotes every identifier (CLAUDE.md database rules). No file paths involved. |
| Outbound calls (config-only, allow-listed, timeouts) | None in release 1: `NoTextSimilarityProvider` makes no call. A real provider's URL, key, timeout and allow-list will be configuration-only and in its own ADR. |
| Resource limits (sizes, counts, page size, queue length, rate limits) | Response is at most 8 tiles; stored rows at most 12 per product; event body capped at 1 KB and the endpoint rate limited by the public policy; the worker processes one group at a time with a per-run time budget. |
| Concurrency (constraints, conditional updates, concurrency tokens) | Unique keys on every table; refresh replaces a product's rows in one transaction; counters increment atomically in SQL; entities carry the concurrency token. |
| Idempotency (inbox, retried commands) | The sales sync rewrites its window (same result when repeated); the refresh is deterministic for the same inputs; event increments are deliberately not idempotent (counters, not records). |
| Audit (who, when, why; append-only; log-injection safe) | Audit columns on every table; `RCMD_RUN` is an append-only log of passes. Log lines carry numbers and ids only, never request text. |
| Data protection (money as decimal, no personal data in events/logs/errors, child data) | Prices are `decimal`. The Orders contract returns product and day totals only. No customer, order id or child data in the new tables, logs or errors (SEC-25, NFR-27..30). |
| Secrets and dangerous configuration | None now. A future provider key comes from secrets and the host refuses to start if a provider is switched on without one. Weights are validated at startup (non-negative, not all zero). |
| Error responses (problem details only) | Yes. A recommendation failure on the product page is swallowed on the server and shown as absent rows, never as a stack trace or a failed page. |
| New dependencies (name, version, licence) | None. No NuGet or npm package is added. |
| Coupling (contracts used, mapper for consumed events, ports and fakes, no cross-module internals) | Storefront reads Orders only through `IProductSalesQueries` in `Orders.Contracts`; no events consumed; the meaning provider is a port with a fake; `Romp.ArchitectureTests` stays green (the Domain project has no EF or ASP.NET reference). |
| Abuse cases considered (at least three: what would a hostile or careless user do?) | (1) Hammer the recommendations endpoint: public rate limit, two indexed reads, no cache poisoning because the key is the path. (2) Flood the events endpoint to skew counters: capped body, rate limited, counters are not used for ranking. (3) Request recommendations for an unpublished or invented path: same not-found as the product page, nothing leaked about unpublished products. (4) A product becomes sold out between worker passes: read-time join removes it at once. (5) Try to get child or customer data through the sales contract: the contract only returns product, day and units. |
