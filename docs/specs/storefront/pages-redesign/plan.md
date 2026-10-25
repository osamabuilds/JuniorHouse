# SCRUM-xxx: Technical plan, pages-redesign

- **Spec:** [spec.md](spec.md)
- **Module(s):** Storefront (web `src/web/projects/storefront`; API `Modules/Storefront`, one additive field)

Mostly presentation. No new module, table, event or ADR. Design values come from `docs/design-system/components/bundle.css` and the component READMEs, mapped onto the live tokens (never from the stale handoff SCSS, Q-PR-1).

## Domain model
None. The discount rules (compare-at above price, server percentage, lowest matching variant) are already in the read model and `SearchListingQuery`; this feature only displays them.

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `SearchListingQuery` (changed) | unchanged | `ListingTileDto` gains `Sizes: IReadOnlyList<ListingTileSizeDto>` (`Code`, `Name`, `SoldOut`) | PR-8 |

`ListingTileSizeDto(string Code, string Name, bool SoldOut)`. Sizes are loaded for the whole page in **one** extra query after the page of product ids is known, the same pattern as `ImagesAsync`: `db.Variants` where `ProductId in (ids)` and `IsSellable`, grouped by size, ordered by `SizeSeq`. A size is sold out when no sellable variant of that size (any colour) has `AvailableQuantity > 0`. The query runs against the unfiltered variants (a card shows every size the product comes in, not only the filtered ones). Page size is already capped; the new query is bounded by it. No N+1.

## API endpoints
| Method | Route | Auth / role | Ownership check (SEC-05) |
|---|---|---|---|
| GET | `/api/storefront/listings` (existing, response gains `sizes` per tile) | anonymous, public data | n/a: no resource ID, no owner data |

Additive JSON field; older clients ignore it. `IPublicStorefrontResponse` caching headers unchanged.

## Events
None.

## Data
No schema, index or migration change. The new query uses the existing `STFR.PRDT_VRNT` index on product id (confirm in task 3; add an index only if the plan test shows a scan).

## NFR / security design
- **SEC-09:** the web shows `price`, `compareAtPrice` and `savingPercent` exactly as sent; no arithmetic on money in the browser. The "Save Rs. X" figure is currently the difference of two server figures; to keep the rule literal, task 4 adds `savingAmount` (decimal, server-computed) to the tile and to the product price DTO rather than subtracting in the browser.
- **SEO-01 / NFR-01:** badge, prices and size line are in the server-rendered HTML. No extra request per card. Images and fetch priority unchanged.
- **NFR-17 / NFR-19:** target size 44px on any new control; sold-out sizes use strike-through plus "sold out" in the accessible name; discount is signalled by minus sign or "Save", never colour alone; `--sale` passes 4.5:1 on all light surfaces (contrast test).
- **Performance budget (PR-18):** CSS added is a few hundred bytes per piece; Lighthouse CI stays the gate.

## Frontend
All under `src/web/projects/storefront/src`.

| Area | Change | Covers |
|---|---|---|
| `styles/_tokens.scss`, `tokens.spec.ts` | Add `--sale: #a8321f`; contrast test: white on `--sale`, `--sale` on `surface`, `surface-raised`, `surface-sunken` all ≥ 4.5:1 | PR-6 |
| `styles/_components.scss` (+ `style-rules.spec.ts`) | Add from `bundle.css`: `rp-tag--sale`, `rp-price--sale` (re-point to `--sale`), `rp-price--lg`, `rp-save`, `rp-off`, `rp-avail`, `rp-notfound*`, `rp-prose`, `rp-sidenav`, `rp-fact`, `rp-callout`, `rp-acc` if missing. Tokens only, no raw colours | PR-1, 7, 8, 10..13 |
| `listings/components/product-tile` | Sale badge uses `rp-tag rp-tag--sale` and is hidden when the product has sold-out sizes only as the existing rule says (PR-4 check: hidden when every size is sold out); add "Save Rs. X"; add the size line (`rp-avail`) from `tile.sizes`; no add-to-bag control (PR-9) | PR-1, 3, 4, 8, 9 |
| `listings/models/listing.model.ts` | `ListingTile.sizes`, `savingAmount` | PR-2, 8 |
| `product-pages/components/product-price` | Use `rp-price--sale`/`rp-price--lg`, `rp-off`, "You save Rs. X"; keep "From" | PR-1, 2, 5 |
| `page/containers/page` (404, 503, removed page) | `NotFoundPage` layout: decorative `404` (`aria-hidden`), `<h1>`, sentence, top-level links, `noindex` meta. No search field, no similar-pieces row | PR-10, 12 |
| `product-pages/containers/product-page` (410) | Gone state: "This piece has left the collection", shop link, same layout; 301 handling untouched | PR-11 |
| `page/components/page-block`, `page/containers/page` (ok) | `ContentPage` layout: eyebrow, `<h1>`, `rp-sidenav` (links from settings footer pages, above the article on phones), `rp-prose` article; blocks as `<dl>` facts, callout, `<table>` with `<th scope>`, lists, `<details>` accordions. Content and facts still from settings | PR-13, 14, 15 |
| Tests | Unit/component specs per component; Playwright axe + overflow at 390/768/1280 on listing, product (sale and non-sale), 404, 410, help page (CI Linux) | PR-17 |

SSR/SEO: no route or meta change for listings and product; 404/410/503 keep their status codes via the existing `RESPONSE_INIT`, and `noindex` is set on them; help pages keep title, description, canonical and `BreadcrumbList` (PR-15).

## Risks & alternatives considered
- **Money in the browser (SEC-09).** Alternative: subtract in the template, as the card would otherwise need. Rejected; a server `savingAmount` is a three-line additive change and keeps the rule literal.
- **Sizes in the tile vs on demand.** Alternative: fetch sizes when a card is hovered. Rejected: the size line is always visible and must be in the server HTML.
- **Sale on sold-out tiles.** The existing tile shows the badge unless sizes are sold out only partly; PR-4 says hidden when the product is sold out entirely (no size in stock). Task 5 pins this with a test.
- **No ADR needed:** no architectural choice beyond existing patterns.

## Security & coupling review (required; a plan without it is not approved)
| Area | How this feature meets it |
|---|---|
| Authorization (policy per endpoint, ownership SEC-05, roles SEC-08) | n/a: public, read-only, anonymous endpoint with no resource ID; no admin surface touched |
| Input validation (type, range, length, charset; DTO ≠ entity) | No new input. Existing listing filters and paging validation unchanged. New DTO is a separate record, not the entity |
| Output encoding (ZPL, PDF, SQL, HTML, file paths) | Names, sizes and prices are bound through Angular (no `innerHTML`, no `bypassSecurityTrust*`). Settings content blocks stay rendered by the existing safe block renderer. The new query is LINQ, parameterised |
| Outbound calls (config-only, allow-listed, timeouts) | n/a: none |
| Resource limits (sizes, counts, page size, queue length, rate limits) | One extra query bounded by the existing page size cap; sizes per product are bounded by the size lookup |
| Concurrency (constraints, conditional updates, concurrency tokens) | n/a: read-only. Stock may be a few seconds stale (P-1); the cart re-checks at add (Sprint 11) |
| Idempotency (inbox, retried commands) | n/a: GET only |
| Audit (who, when, why; append-only; log-injection safe) | n/a: no state change, nothing logged beyond existing request logs |
| Data protection (money as decimal, no personal data in events/logs/errors, child data) | Prices stay `decimal`; `savingAmount` is `decimal` (PKR, 2 dp). No personal or child data involved |
| Secrets and dangerous configuration | n/a: none added |
| Error responses (problem details only) | Unchanged; 404/410/503 pages keep their real status codes, no stack or internal detail shown |
| New dependencies (name, version, licence) | None |
| Coupling (contracts used, mapper for consumed events, ports and fakes, no cross-module internals) | Stays inside the Storefront module and its own read model (`STFR`); no Catalog or Warehouse internals, no new contract |
| Abuse cases considered (at least three: what would a hostile or careless user do?) | (1) Tampering with a price or percentage in the browser changes nothing: the display is not an input, and orders recalculate server-side (SEC-09). (2) A crafted `?size=`/`?q=` value is rendered only as text. (3) A scraper requesting very large pages is stopped by the existing page-size cap, so the extra size query stays bounded. (4) A stale cached page showing a size in stock is acceptable and re-checked at add (P-1) |
