# Product recommendations: tasks

Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md) · ADR: [0029](../../../adr/0029-product-recommendations-blended-score.md). Each step is test-first: the test named is written and seen failing before the code. Backend tests are tagged `[Trait("Spec", "REC-n")]`; .NET tests run in Docker or CI only (see CLAUDE.md environment notes).

## Before code

- [x] **P1** Owner approves spec, plan and ADR 0029; create the Jira task (label `REC`, key into the spec header); branch `<KEY>-product-recommendations` from main. *Proves: spec status changed to Approved.*
- [x] **P2** Two checks the plan left open. (a) `IProductSalesQueries` is implemented in the existing Orders feature folder `Orders` (Infrastructure/Orders), not a new folder, so `StructureSyncTests` needs no new spec card. (b) `.rp-rail` rules already exist in `_components.scss` (line 210); the arrow buttons use the existing `.rp-arrows`. *Proves: this note, no code.*
- [x] **P3** Glossary: add the new words `SMLR`, `SCOR`, `RANK`, `CLK`, `CALC`, `ADD` to `docs/db/naming.md`; `RECM` and `EVNT` already exist and are reused (table names `RECM_*`, `RECM_EVNT_DAY`). *Proves: the naming test in the same PR.*

## Pure scoring (Domain)

- [x] **R1** `SimilarityBlend`. *Tests (`SimilarityBlendTests`, REC-3, REC-13): equal weights average the parts; a null part is dropped and the other weights renormalise so the result stays in 0..1; all parts null gives 0; weights that are negative or all zero are rejected; the result is clamped to 0..1.*
- [x] **R2** `FieldSimilarity`. *Tests (REC-2 inputs, REC-13): same category scores higher than a different one; identical colours score 1 on colour and disjoint colours 0; equal prices score 1 on price and a tenfold gap scores 0; the total is the stated 0.5 / 0.3 / 0.2 mix.*
- [x] **R3** `SalesSignal.Normalise`. *Tests (REC-3): the best seller is 1, a product with no sales is 0, nobody sold gives all 0, the scale is logarithmic (twice the units is less than twice the score).*
- [x] **R4** Entities `SimilarProduct`, `ProductSaleDay`, `RecommendationRun`, `RecommendationDay` and the row lookup ids. *Tests: rank outside 1..12 and a product similar to itself are rejected; negative units are rejected.*

## Orders contract (units sold)

- [x] **O1** `IProductSalesQueries` and `ProductDayUnits` in `Orders.Contracts`; implementation in Orders.Infrastructure over `ORDR_LINE` and the order status. *Tests (Testcontainers, REC-4, CI only): units are summed per product per UTC day; cancelled orders are excluded; orders outside the range are excluded; only product, day and units are returned (no order id, customer or address).*
- [x] **O2** Register the interface in the Orders module and add the contract to the architecture rules. *Test: `Romp.ArchitectureTests` stays green; Storefront references only `Orders.Contracts`.*

## Data (Storefront)

- [x] **D1** EF configurations and migration `AddProductRecommendations`: the five tables, keys, unique and check constraints, indexed FKs, the lookup seeded with `SIMILAR` and `TRENDING`. Every table and column mapped explicitly. *Tests: the existing naming test passes; a Testcontainers test applies the migration, rejects a duplicate (product, rank), a duplicate (product, similar product), rank 13 and a self-pair (CI only).*

## Sales sync and refresh (Application and worker)

- [ ] **S1** `SyncProductSalesCommand`: pulls the last 14 days from `IProductSalesQueries`, upserts `PRDT_SALE_DAY`, deletes older days. *Tests (fake contract, REC-4): rows match the contract for the window; a day whose count dropped (a cancellation) is rewritten; days older than 14 are removed; running it twice gives the same rows.*
- [ ] **S2** `ITextSimilarityProvider`, `NoTextSimilarityProvider` (default) and `FakeTextSimilarityProvider`; weights in `StorefrontOptions` with startup validation. *Tests (REC-13): with the default provider the options validate and the meaning part is null; weights that are negative or all zero fail validation at startup.*
- [ ] **S3** `RefreshSimilarProductsCommand` for one group. *Tests (REC-2, REC-3, REC-5, REC-13): a girls toddler dress never gets a boys or other-age product; of two equally close products the better seller ranks first; at most 12 rows per product and never the product itself; with the fake provider switched on, a text-close product moves up; with it off the same group still gets rows; replacing a product's rows is one transaction (a failure mid-way leaves the old rows).*
- [ ] **S4** Choosing groups: changed since the last finished run, plus every group once a day; `RecommendationRun` written. *Tests: only a changed group is rescored; after a day all groups are; an unfinished run does not move the watermark.*
- [ ] **S5** `RecommendationRefreshWorker` (periodic, interval in options, one group at a time, per-run time budget) and registration in `StorefrontModule`. *Test: the worker runs one pass, writes a finished run and survives a failing group (logs it, goes on).*

## Read side (Application)

- [ ] **Q1** `GetTilesByIdsAsync` on `IStorefrontCatalogQueries`, reusing `ListingTiles`. *Tests (REC-6, REC-7, REC-8): the tile has the same sizes, `VariantId` and `SoldOut` as the listing tile for the same product; all sizes sold out gives `SoldOut` true; one bulk query however many ids (query-count assertion).*
- [ ] **Q2** `GetProductRecommendationsQuery`: similar part. *Tests (REC-1, REC-2, REC-5): up to 4; never the product itself; an unpublished or fully sold-out product in the precomputed rows is left out at once and the next one fills the place; fewer than 4 returns fewer; none returns an empty row.*
- [ ] **Q3** Trending part. *Tests (REC-4): ranked by units in the window; same gender and age group first; fewer than 4 fills from the whole shop; excludes the current product, anything in the similar four, unpublished and sold-out; a cancelled order does not count (via the sales table).*
- [ ] **Q4** Failure isolation. *Test (REC-10): when the similar read or the trending read throws, the query returns empty rows and logs; it never throws to the caller; with no precomputed rows at all only Trending is returned.*
- [ ] **Q5** Speed. *Test (REC-11): on the seeded catalogue the query makes at most three database round trips and the endpoint answers within 150 ms (timed Testcontainers test, CI only).*

## Endpoints

- [ ] **E1** `GET /api/storefront/products/recommendations?path=` in feature folder `ProductRecommendations`. *Tests (integration, CI only): 200 with both rows for a published product; 404 problem details for an unknown or unpublished path with nothing about unpublished products leaked; a malformed path is a 400; no auth needed (REC-1, REC-9 data, SEC-05 n/a recorded).*
- [ ] **E2** `POST /api/storefront/products/recommendations/events` and `RecordRecommendationEventCommand`. *Tests (REC-12): `click` and `add` increment the day counter atomically (two concurrent calls give +2); an unknown row code or action, a non-positive product id and a body over 1 KB are rejected with problem details; no personal data is stored.*

## Web (storefront, `product-pages` feature)

- [ ] **W1** `product-recommendations-api.service.ts` and models; errors map to empty rows. *Tests: a 500 or a network error returns empty rows, never an exception (REC-10); the request is made once per product.*
- [ ] **W2** State-transfer whitelist entry for the recommendations (ADR 0028). *Test: the server render writes the rows and the browser store starts with them, with no second request on hydration.*
- [ ] **W3** `ProductRecommendationsComponent` on the design's `ProductRail`: a `<section aria-labelledby>`, an `h2`, existing `ProductTileComponent`s, Previous and Next buttons, a keyboard-scrollable list. *Tests (component spec, REC-6, REC-9, REC-14): server-rendered markup has the section, the `h2` and one `<a href>` per card; nothing is rendered for an empty list (no empty heading); the arrows scroll by one card and have accessible names; the list is focusable and has an `aria-label`.*
- [ ] **W4** Place the two rows in the product-page container after the composition and care block, loaded without holding the product. *Tests: the page renders and is complete when the recommendations call is slow or fails; headings run `h1` then `h2` with no skipped level.*
- [ ] **W5** Quick add from a card: uses the cart `Add` with `quiet`, then the event call with `add`. *Tests (REC-7, REC-8): picking a size and pressing add sends that `VariantId`; a card with every size sold out offers no add and says so; the bag count updates.*
- [ ] **W6** `recommendation-event.util.ts` and the click beacon (browser only). *Tests (REC-12): the body has only the row code, product id and action; nothing is sent on the server render; `sendBeacon` is used with a fetch fallback.*
- [ ] **W7** Port any missing `ProductRail` rules into `_components.scss`, tokens only. *Proves: the raw-colour audit and the e2e check below.*

## End to end and close-out

- [ ] **X1** Browser tests in `e2e/journeys/storefront-recommendations.e2e.spec.ts` (rebuilt containers, seeded catalogue): both rows are in the server HTML with working links and no script (REC-9); at 390, 768 and 1280 px there is no horizontal page scroll and no layout shift when the rows appear (REC-11); keyboard-only: tab to a card, choose a size, add it, scroll the rail with the arrow buttons (REC-14); axe is clean; a product with no similar rows still shows Trending now.
- [ ] **X2** Add the demo data the e2e needs (several products per gender and age group, some orders in the last 14 days, one cancelled) to the demo seeder. *Test: the seeder test drops the 60-second lookup cache before it reads (known flake).*
- [ ] **X3** Rebuild docker (`docker compose -f docker-compose.yml -f docker-compose.secure.yml up -d --build`), run the worker once, and check a product page by eye on phone and desktop width. Then update `spec.md` ("As built"), the storefront README row and card, and the memory hand-off.
- [ ] **X4** PR description links this folder and lists REC-1..14, SEO-01, SEO-07, NFR-01, NFR-19, NFR-FT-08. Wait for the owner before merging.
