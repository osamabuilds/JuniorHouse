# SCRUM-22: Listings, filters and sorting: technical plan

- **Spec:** [spec.md](spec.md) · **Module plan:** [../plan.md](../plan.md)
- Open questions followed at their proposed default: Q-LST-2 (Sale = any in-stock product with a variant whose compare-at price is above its price), Q-LST-3 (30 days), Q-LST-4 (24 per page, past-the-end is 404), Q-LST-5 (min/max boxes, filter price = lowest available variant price), Q-LST-6 (swatch if present, else names), Q-LST-7 (see SEO Q-SEO-2), Q-LST-8 (URL scheme), Q-LST-9 (same-variant filtering).

## Domain model
- `ListingScope` (value): kind (`Audience`, `Category`, `NewIn`, `Sale`, `Collection`, `AllNewIn`, `AllSale`), gender code?, category code?, collection code?.
- `ListingFilter` (value, parsed and clamped): sizes[], colours[], age code?, price min/max (`decimal`, 0 ≤ min ≤ max ≤ 1,000,000), sort (`recent` default, `price-asc`, `price-desc`), page (≥ 1). Unknown keys are dropped; malformed values yield 400 for numbers out of format, otherwise ignored (LST-11). `ListingFilter.ToCanonicalQuery()` returns the normalised query string in a fixed key order, used by the SEO feature.
- Rule: a variant **matches** when it is in stock, and its size ∈ sizes, colour ∈ colours and price within range (same variant, Q-LST-9); a product is listed when listable and at least one variant matches (no filters: any in-stock variant).

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `SearchListingQuery` | scope, filter | page of tiles (path, name, primary image derivatives, price or "from" price, compare-at, saving %, sold-out sizes flag, colour count), total, page, filter options for this scope | LST-1..4, 7..9, 13, 15, 16 |
| `GetListingOptionsQuery` | scope | sizes, colours (with swatch), price min/max available in the scope (2 aggregate queries) | LST-1, 2 |
| `GetEmptyStateQuery` | scope, filter | suggestions: other categories of the audience with listable products, New Arrivals link | LST-5, 6 |

Sorting: `recent` = `PUBL_DTE` desc then id desc; `price-*` = `MIN_PRIC` (computed over in-stock variants when filters are absent, over matching variants when present) then id. All sorts end in the unique id so pages never repeat or skip. Pagination: `OFFSET/LIMIT` with `COUNT(*) OVER()`; offset paging is fine at this catalogue size and keeps back-button URLs simple. Past-the-last page returns 404.

Tiles are projected in SQL into `ListingTileDto`; the primary image's derivative rows come in one second query filtered by the page's product ids (`Contains`), so the page costs 3 queries + 2 for options regardless of size (LST-13, no N+1).

## API endpoints
| Method | Route | Auth | Notes |
|---|---|---|---|
| GET | `/api/storefront/listings?scope=&gender=&category=&collection=&size=&colour=&age=&priceMin=&priceMax=&sort=&page=&pageSize=` | `Storefront.Public` | `size` and `colour` repeatable; 400 on malformed numbers; 404 when the scope's gender/category/collection code is unknown |
| GET | `/api/storefront/listings/options?...scope` | `Storefront.Public` | filter options |

Ownership check: n/a.

## Events
None.

## Data
Uses `PRDT_MAIN` and `PRDT_VRNT` indexes from the published-catalog plan; adds none. A covering index on `PRDT_VRNT (SIZE_CODE, CLR_CODE, PRDT_ID) WHERE AVLB_QTY > 0` serves the same-variant `EXISTS`.

## NFR / security design
NFR-02: a listing is one indexed query; measured in the integration test at 1,000 products × 8 variants under 200 ms. Query-string cache busting is blocked: the SSR layer rebuilds the URL from the parsed filter and redirects (301) to the canonical key order if the incoming query is not already canonical or contains unknown keys.

## Frontend (`features/storefront/listings`)
- Route table (SSR, `RenderMode.Server`): `/new-in`, `/sale`, `/collections/:code`, `/:gender`, `/:gender/new-in`, `/:gender/sale`, `/:gender/:category`; the three-segment product route belongs to `product-pages`. Reserved single segments are matched first; unknown gender or category codes render the 404 page with a real 404.
- `containers/listing-page` (resolver loads data before render, TransferState), `components/product-tile` (`<article>` with `<a>`, `<picture>` with WebP and JPEG sources, `srcset`/`sizes`, explicit width/height; first row eager, rest lazy), `components/filter-panel` (a real `<form method="get">` with `<fieldset>/<legend>`, labelled checkboxes and number inputs; with scripting it applies on change and updates the URL; without scripting the Apply button submits), `components/sort-select` (labelled `<select>` inside the same form), `components/active-filters` (chips as links that remove one filter, "Clear filters"), `components/pagination` (`<nav aria-label="Pagination">` with real links, `rel="prev"`/`"next"` hints), `components/empty-state` (heading, explanation, links).
- `store/` (NgRx) keeps scope, filter, results, options, total, page; changing a filter resets to page 1; `<app-pagination>` from `@shared` is reused if it fits the storefront, otherwise a storefront component with the same inputs.
- Tile sizes and `sizes` attributes are fixed per breakpoint so no layout shift (CLS ≤ 0.1). Filter panel is a collapsible `<details>` on phones.

## Risks & alternatives considered
Keyset pagination was rejected (back-button and shareable page URLs matter more than the saving at this size). Faceted counts next to each option were left out (cost and not required by FR-03); the option lists show only values that exist in the scope.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | Anonymous GET only, explicit policy |
| Input validation | Typed parse with allow-lists (sort keys, codes from lookups), numeric ranges, `page` clamped, `pageSize` ≤ 48, repeated params capped at 20 values; unknown keys dropped |
| Output encoding | Angular encoding; echoed filter values come from the parsed object, not the raw query |
| Outbound calls | none |
| Resource limits | Page size cap, value-count cap, 404 beyond the last page, per-client rate limit at the edge and in SSR |
| Concurrency | n/a (read only) |
| Idempotency | n/a (GET) |
| Audit | n/a (no state change) |
| Data protection | No personal data, no cookies; exact stock quantity never returned |
| Secrets and dangerous configuration | none |
| Error responses | 400/404 as ProblemDetails; SSR renders safe pages |
| New dependencies | none |
| Coupling | Only `STFR`; the empty state uses the same read model; lookups via the cache |
| Abuse cases | `?size=…` with thousands of values (cap); price range `-1` or `1e309` (400); page `999999999` (404, no scan: `OFFSET` guarded by the count); a filter string with markup (never echoed raw); cache-busting random parameters (dropped and 301) |
