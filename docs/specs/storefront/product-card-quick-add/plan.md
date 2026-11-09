# Product card: second picture, tags and quick add: technical plan

- **Spec:** [spec.md](spec.md) (approved by the owner on 2026-11-05 with the recommendations for Q-CRD-1..5)
- **Module(s):** Storefront (API read side and web). Uses the Cart module only through its existing web action; no cross-module server change.

## Decisions taken from the open questions

- **Q-CRD-1:** the listing tile gets additive fields: `variantId` per size and an optional `secondImage`. Both come from data the tile query already loads or from one extra column on queries that already run.
- **Q-CRD-2:** `Low stock` reuses the existing rule: `Availability.Classify(isSellable, availableQuantity, LowStockThreshold)` returning `LowStock`, the same classifier the product page uses.
- **Q-CRD-3:** `New` reuses the New In rule: `PublishedAt >= now - NewArrivalsDays` (`IListingSettings`).
- **Q-CRD-4:** the bar's **Notify me** links to the product page with that size chosen (`<path>?size=<code>`), where the existing notify-me form lives.
- **Q-CRD-5:** reuse `CartActions.Add({ variantId, quantity: 1 })` and the existing bag drawer (`CartActions.OpenDrawer`) unchanged.

## Multi-colour rule (confirmed by the owner on 2026-11-05)

A product with several colours has several variants per size. The tile advertises the price of its cheapest matching variant, so the bar adds **that colour** and names it in the confirmation ("Added, size 4Y, Rose"). A "More colours" link goes to the product page.

## Domain model

Read side only. `ListingTileDto` gains: `bool IsNew`, `bool HasLowStock`, `ListingImageDto? SecondImage`, and each `ListingTileSizeDto` gains `long VariantId` and `string Availability` (`InStock`, `LowStock`, `SoldOut`) for the tile's default colour. A size's `SoldOut` flag stays as today (no colour of that size can be bought); `Availability` and `VariantId` refer to the default colour so the bar never offers a variant that cannot be bought.

Default colour of a tile = the colour of the variant that gives the tile its price; if several tie, the lowest colour sort order.

## Application layer

| Query change | Input | Output | Covers |
|---|---|---|---|
| `ListingTiles.SizesAsync` | page product ids (already one query) | per size: code, name, sold out, `VariantId`, `Availability` for the default colour | CRD-4, CRD-5, CRD-7, CRD-8 |
| `ListingTiles.ImagesAsync` | page product ids (already two queries) | main image plus `SecondImage`: lowest `SortSeq` non-main image whose `ColourId` is null or equals the main image's colour, fetched in the same two queries | CRD-1, CRD-2 |
| `ListingTiles.Project` | existing | adds `IsNew` from `PublishedAt` and the cutoff; `HasLowStock` from the sizes result | CRD-3 |

Queries per listing request stay constant (no N+1): the existing tile query, one variants query, two image queries.

## API endpoints

No new endpoint. `GET /api/storefront/listings...`, `/search` and `/home` return the extended tile (the home page and the search results reuse the same tile builder). All additions are optional on the wire, so older cached pages keep working.

## Events / Data

No event, no schema change, no migration. `ImageRow.SortSeq`, `ImageRow.ColourId` and variant availability already exist in the read model.

## NFR / security design

- **FR-28, FR-29, SEC-09:** the browser sends only `variantId` and quantity 1; the Cart module prices the line and decrements stock atomically as it does for the product page. The tile's `Availability` is a hint; the server decides. A refusal returns the existing out-of-stock cart message.
- **CRD-9 (sold out between load and tap):** on a refused add the container shows "That size has just sold out" and dispatches a reload of the current listing key (the existing `load` action), so the chips refresh from the server.
- **NFR-01:** the second picture is lazy and absolutely positioned inside the 4:5 box; the carousel and bar overlay the picture. On desktop the second `<img>` element is created only after the first `pointerenter` or focus (CRD-1). On touch both pictures are in a snap strip but the second is `loading="lazy"`.
- **NFR-19:** the bar opens on focus-within, Esc closes it and returns focus to the **+** or the card, sold-out chips are `aria-disabled` with "sold out" in the name, **+** is a 44px button with `aria-expanded`, the added message is `role="status"`. All bar actions use the ink button style, never the marigold (CRD-12).
- **SEO-01:** the card's link, first picture, name, price and size line are unchanged in the server HTML; the bar, **+**, dots and second picture are browser-side.
- **Resource limits:** the tile payload grows by one image reference and a few fields per card (24 per page, existing cap).

## Frontend

`features/storefront/listings/`:
- `components/product-tile/` (extended): tag logic, second picture, dots, hosts the bar; stays presentational except for the existing wishlist heart.
- `components/quick-add-bar/` (new, presentational): inputs sizes, colour name, state (`idle`, `choosing`, `added`, `soldOut`, `error`), outputs `choose(variantId, sizeName)`, `close`, `viewBag`.
- `containers/quick-add/quick-add.container.{ts,html}` (new, store-aware): dispatches `CartActions.Add`, reads `selectCartBusy` and the cart message, dispatches `CartActions.OpenDrawer` for View bag, dispatches the listing reload on a sold-out refusal.
- `utils/card-tag.util.ts` (pure): sale, else low stock, else new, else none.
- `models/listing.model.ts`: new optional fields.
- Styles: `rp-slide`, `rp-pagedots`, `rp-qa`, `rp-size` chip rules ported from the design's `_components.scss`, tokens only.

## Risks & alternatives considered

- **One request per card for sizes and second picture:** an N+1 on every listing. Rejected; the data rides on the existing page queries.
- **Quick view modal:** replaced by QuickAdd in the design; not built.
- **Showing the exact stock count:** forbidden (PCT-3, no count is ever shown).
- No architectural choice that sets precedent, so no ADR.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | listing is public; the add goes through the existing cart endpoint with its session, CSRF and ownership checks (SEC-05) |
| Input validation | the browser sends an integer `variantId` taken from the tile and quantity 1; the Cart API validates both as it does today |
| Output encoding | names, sizes and colour via Angular bindings |
| Outbound calls | same-origin API only |
| Resource limits | page size cap unchanged; one bar open per card; the add button is disabled while the cart is busy |
| Concurrency | the server decrements stock atomically (FR-29); a double press sends one request because the control is disabled while busy |
| Idempotency | the existing cart add behaviour (quantity merges on the same variant) |
| Audit | n/a |
| Data protection | no personal or child data in tiles or bar |
| Secrets and dangerous configuration | none |
| Error responses | refused or failed adds show a message in the bar; nothing is added silently |
| New dependencies | none |
| Coupling | web: the listings feature uses the cart's public barrel (`CartActions`, selectors); server: no Cart module reference from Storefront (only a variant id crosses to the browser, as on the product page) |
| Abuse cases | (1) tapping a sold-out chip: `aria-disabled` and no call; (2) replaying a stale `variantId` for a size that sold out: the server refuses and the card refreshes; (3) a forged price field in the request: the add API ignores it, the server prices the line |
