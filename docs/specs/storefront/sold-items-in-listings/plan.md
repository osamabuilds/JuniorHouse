# Sold items in listings: technical plan

- **Spec:** [spec.md](spec.md) (draft, not yet approved)
- **Module(s):** Storefront. No new module. One data change (read model rebuild), no schema change proposed.

## Domain model

- `ProductVisibility.Evaluate` today returns `IsListable = inStock && primaryImageReady`. Change: `IsListable = primaryImageReady` (published and picture ready), and keep `IsInStock` as its own flag. The read model already stores both flags, so **no column is added**; existing rows are corrected by the rebuild (SLD-13).
- `IsListable` keeps its name but its meaning becomes "fit to appear in a listing". The summary comment and the PCT rules are updated with it.

## Application layer

| Change | Where | Covers |
|---|---|---|
| `InScope` no longer needs stock; sort adds `OrderBy(p => !p.IsInStock)` ahead of the chosen sort | `ListingQueryBuilder` | SLD-1, 2 |
| `ListingFilter` gains `InStockOnly` (`stock=in`); parsed, validated, in the canonical query | `ListingFilter`, `ListingQueryBuilder` | SLD-4 |
| Size, colour and price match any variant unless `InStockOnly`, then in-stock variants only | `MatchingVariants` | SLD-8 |
| Tiles carry `SoldOut` (product level) and each size already carries availability | `ListingTiles`, `ListingTileDto` | SLD-5 |
| Counts and option counts use the same query as the page | `ListingOptionsAndEmptyState` | SLD-3, 9 |
| Search includes sold products, ordered after in-stock matches | `SearchProductsQuery`, `SuggestQuery` (suggestions keep in-stock only) | SLD-7 |
| New scope `CollectionCategory(collection, category)` | `ListingScope`, scope resolver | SLD-10 |
| Home, menu tile, menu flags keep an explicit `IsInStock && IsListable` condition | `GetHomeQuery`, `GetMenuQuery` | SLD-11 |
| Collections index counts all listable pieces | `GetCollectionsQuery` | SLD-12 |
| Product page `availability` already comes from variants; structured data reads it | product page DTO and web SEO builder | SLD-6 |

## API endpoints

No new route. `GET /api/storefront/listings` gains `stock=in` and the scope `collection` + `category`; the options and empty endpoints share the scope rules. Unknown or hostile values stay 400/404 as in LST-11.

## Web

- Product card: `soldOut` input, dimmed picture, "Sold out" text label, no quick add, accessible name "<name>, sold out".
- Filter bar: an "In stock only" switch (a checkbox with a label), carried in the URL.
- New route `collections/:collection/:category`, same container as the listings; breadcrumb of four levels.
- Listing state (NgRx) carries the new filter; no new store.
- Design system: use the existing `rp-` card and disabled styles; if the design system has no sold-out card, raise it before inventing one.

## Data

- Rebuild of the read model after deploy (the existing rebuild endpoint and reconcile job).
- Demo data: a Development-only seeder (extend the existing demo seeding) that creates winter and summer ranges, a dozen products per category, a third of them with no stock, with generated placeholder pictures.
- Launch navigation: the built-in launch set gets the Season > categories shape with our own labels; the live menu is entered in the admin settings.

## NFR / SEC / SEO

| Item | How met |
|---|---|
| LST-13, no N+1 | query-count test extended with a sold-out mix |
| PCT-3 no stock counts | DTO test: no quantity field |
| SEO-06 | canonical for `stock=in` variants points to the clean listing |
| SEO-03 | `OutOfStock` in `Offer` |
| NFR-19 | axe test on a listing with sold-out cards |
| SEC-09 | nothing price-related changes; sold-out cards cannot reach add to cart, the cart still checks stock server-side |

## Security & coupling review

| Question | Answer |
|---|---|
| New endpoint or parameter taking an id? | No; one new filter flag and one new scope, validated like the others |
| Cross-module reads? | None; the read model only |
| Logs of personal data? | None |

## Risks

- Changing the meaning of `IsListable` touches every query that uses it: the plan lists each one (search, suggest, menu, home, collections, options). A test per call site proves which meaning it uses.
- Counts rise on live pages after the rebuild; expected.
