# Product card: second picture, tags and quick add

- **Status:** Draft, awaiting owner approval
- **Jira:** none yet
- **BRD sections:** FR-02 (listing), FR-10 (add to bag), FR-28, FR-29 (atomic stock decrement), FEAT-CAT-02..04; design source `docs/design-system/components/ProductCard/README.md`, `QuickAdd/README.md`, `Offers/README.md`
- **Requirement IDs:** FR-02, FR-10, FR-28, FR-29, SEC-09, NFR-01, NFR-17, NFR-19

## Problem & intent

The design's card is image-first with a second view of the garment (crossfade on desktop hover, a two-picture swipe with dots on touch), one small tag, and a way to add to the bag from the picture: a size bar that slides up on desktop and a round **+** on touch. The live card has one picture, the wishlist heart, the sale badge, the price and the compact size line with sold-out sizes struck (design audit G2). The bag, wishlist and notify-me features now exist, which the design's QuickAdd note said it was waiting for.

QuickAdd replaces the older hover "Quick view" bar (the QuickAdd README says so), so the Quick view modal is not part of this spec.

## Requirements (from the design system)

> **ProductCard:** "Two images per product: the front and a second view. Hover (desktop, 64rem+): the second image crossfades in over `dur-slow` and the `QuickAdd` bar slides up from the bottom of the image; no zoom on cards. Keyboard focus inside the card does the same. Fetch the second image on first `pointerenter`/focus, not on load. Touch (under 64rem): the two images are a swipeable `scroll-snap` carousel with two quiet dots (`rp-pagedots`); a round **+** on the image opens the same `QuickAdd` bar; the rest of the card opens the product page. Tag: one tiny text tag at most, top-left: `New` or `Low stock`. A discounted product carries one brick `−NN%` badge (`rp-tag--sale`) top-left. No Bestseller badges. The wishlist heart (44px target, `aria-pressed`) is top-right."
>
> **QuickAdd:** "Desktop: hovering or focusing a card slides a bar up from the bottom of the image: 'Choose a size' and a row of size chips (44px). Pressing an available size adds it at once; the bar then says 'Added, size 4Y' with **View bag**. Phone and tablet: a round **+** (44px, ink) bottom-right on the image opens the same bar; the **+** is a button with `aria-expanded`; the bar has a close button. Sold-out sizes stay in the row, struck through and `aria-disabled`, with 'sold out' in the accessible name. If every size is sold out there is no **+**; the bar offers **Notify me** instead. A product with one size or no size shows **Add to bag** directly. Stock, price and the added line come from the server; the add is checked and decremented atomically on the server (FR-28, FR-29, SEC-09). If a size sells out between page load and the tap, the bar says so and refreshes the chips. Keyboard: the bar opens on focus-within; Tab moves through sizes, Enter or Space adds, Esc closes. The marigold is not used on cards; the bar's actions are ink."

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| CRD-1 | Given a product with two pictures and a desktop pointer, when the pointer first enters (or focus enters) the card, then the second picture is requested then (not at page load) and crossfades in; leaving restores the first | ProductCard |
| CRD-2 | Given touch (under 64rem), then the two pictures are a swipe strip with two dots, the first dot active; a product with one picture shows no strip and no dots | ProductCard |
| CRD-3 | Given a card, then at most one text tag shows top-left: the sale badge when discounted, otherwise `Low stock` when any size is low, otherwise `New` for a new arrival, otherwise nothing | ProductCard, Offers |
| CRD-4 | Given desktop, when a card is hovered or focused, then a bar slides up with "Choose a size" and 44px size chips; sold-out sizes are struck through, `aria-disabled`, and "sold out" is in their accessible name | QuickAdd |
| CRD-5 | Given an available size, when it is pressed, then that size is added through the existing add-to-bag action, the bag count updates, and the bar says "Added, size 4Y" with a **View bag** link that opens the bag | QuickAdd, FR-10 |
| CRD-6 | Given touch, then a 44px round **+** sits bottom-right on the picture, is a button with `aria-expanded`, and opens the same bar with a close button | QuickAdd |
| CRD-7 | Given every size sold out, then the card has no **+** and the bar offers **Notify me** | QuickAdd |
| CRD-8 | Given a product with one size or none, then the bar shows **Add to bag** directly, with no chips | QuickAdd |
| CRD-9 | Given a size that sold out after the page loaded, when it is pressed, then the server refuses, the bar says so, and its chips refresh from the server; nothing is added | QuickAdd, FR-29 |
| CRD-10 | Given the add, then price and availability come from the server; the browser sends the variant, never a price (the same call the product page uses) | SEC-09 |
| CRD-11 | Given the keyboard, then focus inside the card opens the bar, Tab moves through the sizes, Enter or Space adds, Esc closes and returns focus to the **+** or the card | QuickAdd, NFR-19 |
| CRD-12 | Given the bar's buttons, then they use the ink style, never the marigold action colour | QuickAdd |
| CRD-13 | Given the card without scripting, then it still shows the first picture, name, price and size line and links to the product page (the bar and carousel are enhancements) | SEO-01 |
| CRD-14 | Given the grid at 390, 768 and 1280 px, then no layout shift appears when a second picture loads or the bar opens, no horizontal scroll, and axe reports no violations | NFR-01, NFR-19 |
| CRD-15 | Given the existing wishlist heart, size line and sale treatment, then they are unchanged | existing |

## Non-functional constraints

NFR-01: the second picture is lazy and absolutely positioned inside the 4:5 box, so nothing moves. SEC-09 and FR-29: stock and price decisions stay on the server. NFR-27/29 do not apply (no child data). Listing payloads stay paged and need no per-card requests (no N+1).

## Out of scope

The Quick view modal (replaced by QuickAdd), colour dots on cards, "Bestseller" badges (the design forbids them), recommendation rows (separate spec).

## Open questions

- [ ] **Q-CRD-1** The listing API gives each tile its sizes as `code`, `name`, `soldOut` but no variant identifier, and only one picture. Recommendation: extend the tile with `variantId` per size (needed to add) and an optional `secondImage` (the product's second picture for the displayed colour), both additive fields, paged as today.
- [ ] **Q-CRD-2** `Low stock`: reuse the product page's existing low-stock rule for a size (so the card and the page never disagree). Recommendation: reuse it, and tag the card when any size is low and none is hidden.
- [ ] **Q-CRD-3** `New`: reuse the new-arrivals definition already used by the New in page. Recommendation: reuse it.
- [ ] **Q-CRD-4** Notify me from the bar (CRD-7): the existing form needs an email address. Recommendation: the bar's **Notify me** opens the product page at that size (the existing form there), which avoids a second form on every card.
- [ ] **Q-CRD-5** The design README said the bag flow needed its own spec. The cart exists and has specs; confirm that this spec may reuse the cart's add-to-bag action and the "View bag" drawer without changes. Recommendation: yes.
