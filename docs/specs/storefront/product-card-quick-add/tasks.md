# Product card: second picture, tags and quick add: tasks

Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md). Each step is test-first; the test named is written and seen failing before the code. Server tests run on CI (Linux) because Application Control blocks local DLLs.

## API (Storefront, .NET)

- [x] **A1** Check `VariantRow` has the colour and price fields needed to find a tile's default colour; if the read model lacks one, stop and raise it (no schema change is planned). *Proves: a test that builds a tile for a two-colour product and reads the default colour.*
- [x] **A2** Extend `ListingTileSizeDto` with `VariantId` and `Availability` for the default colour, in the existing sizes query. *Tests (`SearchListingTests`-style, Testcontainers): `Tiles_GiveEachSizeAVariantOfTheDefaultColour`, `Tiles_MarkLowStockSizes` using `LowStockThreshold`, `Tiles_KeepSoldOutSizesInTheRow` (CRD-4, CRD-7).* Tag `[Trait("Spec", "CRD-x")]`.
- [x] **A3** `IsNew` and `HasLowStock` on the tile. *Tests: `Tile_IsNewInsideTheNewInWindow`, `Tile_IsNotNewOutsideIt` (same cutoff as the New In page), `Tile_HasLowStockWhenAnySizeIsLow` (CRD-3).*
- [x] **A4** `SecondImage` from the same two image queries. *Tests: `Tile_SecondImageIsTheNextPictureOfTheSameColour`, `Tile_HasNoSecondImageWhenOnlyOnePicture`, `Listing_RunsTheSameNumberOfQueries` (a query counter proves no N+1 as the page grows) (CRD-1, CRD-2).*
- [x] **A5** Wire the new fields through `/listings`, `/search` and `/home` responses; additive only. *Tests: endpoint tests show the fields and that a response without them still parses in the web model; existing listing, search and home tests unchanged.*
- [x] **A6** `StructureSyncTests` and `Romp.ArchitectureTests` stay green.

## Web (Angular storefront)

- [x] **W1** Extend `listing.model.ts` (and the search and home models that reuse it) with the optional fields. *Tests: model parsing specs.*
- [x] **W2** `card-tag.util.ts`. *Tests: sale beats low stock beats new beats none; at most one tag (CRD-3).*
- [x] **W3** `ProductTileComponent`: tag, second picture created on first `pointerenter` or focus only, crossfade class, touch strip with two dots, **+** button with `aria-expanded`. *Tests (component spec): the second `<img>` does not exist before interaction (CRD-1); a one-picture product has no strip or dots (CRD-2); **+** toggles `aria-expanded` (CRD-6); no **+** when every size is sold out (CRD-7); existing heart, size line and sale tests unchanged (CRD-15).*
- [x] **W4** `QuickAddBarComponent` (presentational): "Choose a size" with 44px chips, sold-out chips struck, `aria-disabled`, "sold out" in the accessible name; one-size or no-size shows **Add to bag**; added and sold-out states; close button; keyboard (Tab, Enter, Space, Esc). *Tests: each state's markup, keyboard activation emits `choose`, Esc emits `close`, disabled chips never emit (CRD-4, CRD-8, CRD-11, CRD-12).*
- [x] **W5** `QuickAddContainer`: dispatches `CartActions.Add({ variantId, quantity: 1 })`; "Added, size 4Y" (plus colour name when the product has more than one colour) and **View bag** (`CartActions.OpenDrawer`); disables while the cart is busy; on refusal shows "That size has just sold out" and dispatches the listing reload. *Tests (store specs): exactly one `Add` per press, never a price in the action (CRD-5, CRD-10); busy blocks a second press; a refused add reloads the current key and shows the message, nothing added (CRD-9); View bag opens the drawer.*
- [x] **W6** Notify me in the bar links to `<path>?size=<code>`. *Test: href is built from the tile path and the size code (CRD-7, Q-CRD-4).*
- [x] **W7** Styles: `rp-slide`, `rp-pagedots`, `rp-qa`, `rp-size` ported, tokens only, ink buttons (no marigold), reduced motion. *Proves: raw-colour audit; a test that the bar never carries the action button style (CRD-12).*

## End to end and close-out

- [x] **E1** Browser tests in `e2e/journeys/storefront-card-quick-add.e2e.spec.ts` (rebuilt container): desktop hover opens the bar, a size adds and the bag count changes, View bag opens the drawer; keyboard focus opens the bar and Esc closes it; at 390 px the **+** opens the bar and the strip swipes between pictures (CRD-1..6, 11).
- [x] **E2** Same file: second picture requested only after the first hover (network watch), no layout shift when the bar opens or the picture loads, no horizontal scroll at 390, 768 and 1280 px, axe clean (CRD-1, CRD-14); with scripting off the card still links to the product (CRD-13).
- [x] **E3** Rebuild the docker API and storefront, check by eye, then update `spec.md` ("As built", including the multi-colour rule), the storefront README row, the design audit (G2 fixed) and the memory hand-off.
- [ ] **E4** PR description links this folder and lists CRD-1..15, FR-02, FR-10, FR-28, FR-29, SEC-09, NFR-01, NFR-17, NFR-19.
