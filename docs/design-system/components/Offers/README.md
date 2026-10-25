Discounts are shown the same way on every surface, so a shopper recognises one on sight: a brick-red **minus-percent badge** on the picture, a **bold sale price in the same red** with the old price struck through in muted grey, and the **saving in plain words**.

**Amends the ProductCard rule.** `ProductCard` said "No Sale, Bestseller or percentage badges on images". By owner request (discounts must be visible and catchy) a discount badge is now allowed, and only that badge: one per image, top-left, replacing the `New` / `Low stock` tag. Bestseller and other badges stay out.

## Colour
- One discount colour, `--sale`, an alias of `--danger-text` (a muted brick, 4.5:1 or better on every light surface). It is used for the badge fill, the sale price, the saving line and the savings rows in the bag. Nothing else is red.
- Marigold (`--action`) stays reserved for the one main button per screen (Add to bag, Checkout). The preview shows the three options side by side: **C brick (recommended)**, A ink only (too quiet), B marigold badge (competes with the button).
- Never colour alone: every discount also carries a minus sign or the word "Save", and the struck price is announced as "Was" (`sr-only`), the current one as "Now".

## Pieces
- `rp-tag rp-tag--sale`: the image badge, "−30%". 13px, weight 600, white on `--sale`.
- `rp-price rp-price--sale` (+ `rp-price--lg` on the product page): sale price bold in `--sale`; `<s>` the old price, muted, weight 400. Use `<s>` with a visually hidden "Was".
- `rp-save`: "Save Rs. 1,500" under the price on cards, "You save Rs. 1,500" on the product page.
- `rp-off`: the larger pill beside the product-page price. When sizes differ in price: "From Rs. 2,990" with "Up to −33%".
- `rp-saved`: the summary line "You are saving Rs. 3,746 on this order" with a check, on `surface-sunken`.
- `rp-sumrow--sale`: savings rows in the bag summary ("Sale savings", "Promo code …"), shown as negative amounts, on separate lines from the subtotal at full price.
- `rp-promo`, `rp-promo-chip`: the promo field and its applied chip; states are default, applied (success text with a check) and invalid (error text with an icon and an instruction).
- `rp-progress`: the free-delivery bar (2px in the drawer, 4px in the page summary), with a sentence: "Add Rs. 1,540 more for free delivery" or "Free delivery unlocked".
- Sale banner: the dark band with `rp-offer__big` ("Up to 40% off") and a **light** button, so marigold is not spent on a banner.
- Listing filter chips by range: All sale, Up to 20%, 20–40%, 40% or more.

## Rules
- **Every figure is the server's.** Prices, struck prices, percentages and totals are never calculated in the browser (SEC-09). The percentage is the server value, rounded; the rupee saving is the difference.
- A struck price appears only when the server sent a compare-at price above the price (Catalog D4). No fake "was" prices, no countdown clocks, no urgency copy.
- The badge shows the saving of the price displayed. For a product with several sizes, the tile shows the lowest price and the biggest saving.
- Sold-out items keep their sold-out state; the badge is hidden on them.
- Contrast: white on `--sale` and `--sale` on white both pass 4.5:1; focus rings and 44px targets are unchanged.

## Open points
- `--sale` is a proposed alias and is not yet in the live `src/styles/_tokens.scss`; adding it needs the tokens spec's contrast test to cover it (see the story that builds this). It is added to `tokens.json` as a proposal.
- The sample promo code, amounts and the Rs. 8,000 free-delivery threshold are placeholders; the threshold and promo rules are business settings.
- Not drawn: how a promo with a minimum spend or an expiry is explained, and the stacking rule between a promo code and sale prices (decision for the business).
