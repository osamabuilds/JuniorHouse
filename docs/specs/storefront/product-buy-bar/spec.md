# Product page buy bar and perks

- **Status:** Draft, awaiting owner approval (owner accepted building it on 2026-11-05; the details below are not yet approved)
- **Jira:** none yet
- **BRD sections:** §5 product page (FEAT-PDP-*); design source `docs/design-system/components/ProductDetail/README.md`
- **Requirement IDs:** FEAT-PDP-01..04 (existing), NFR-01, NFR-17, NFR-19, SEC-09

## Problem & intent

On a phone the product page has no always-reachable Add to bag: after the visitor scrolls to the size chart, composition or care text, the button is far above. The design requires a sticky bottom buy bar and three perks under the buy button (design audit, finding D4). Neither was in an approved spec.

## Requirements (from the design system, not the BRD)

> "Phone: a full-bleed swipe gallery, then title, price, swatches and sizes straight away; a sticky bottom buy bar (price and Add to bag) is always reachable. Keep `scroll-padding-bottom` equal to the bar height so focus is never hidden behind it (WCAG 2.4.11)."
>
> "Desktop: ... Add to bag plus wishlist, three perks (delivery, exchange, WhatsApp), accordions (Details, Fabric and care, Delivery and exchange)."

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| BUY-1 | Given a product page under 64rem, when the main Add to bag button is not visible, then a bar fixed to the bottom shows the current price (with the struck old price when discounted) and an Add to bag control | design |
| BUY-2 | Given the main Add to bag button is in view, then the bar is hidden so there are never two competing buttons | (new) |
| BUY-3 | Given no size is chosen, when Add to bag in the bar is pressed, then the page scrolls to the size selector and shows the same "Choose a size" message as the main button, and nothing is added | existing size rule (new for the bar) |
| BUY-4 | Given a size is chosen and in stock, when the bar button is pressed, then the item is added through the same action as the main button and the bag drawer opens | existing add-to-bag |
| BUY-5 | Given a sold-out or unavailable product or size, then the bar shows the same state as the main button (disabled, with Notify me where it exists) and never allows adding | existing availability rules |
| BUY-6 | Given the bar, then its price is the server-calculated price already on the page, never computed in the browser | SEC-09 |
| BUY-7 | Given the bar is shown, then `scroll-padding-bottom` equals its height, so a focused element is never hidden behind it, and it does not cover the footer's last line at the bottom of the page | WCAG 2.4.11 |
| BUY-8 | Given the bar, then it is keyboard operable with a 44px button, and no layout shift happens when it appears (it overlays; the page does not move) | NFR-17, NFR-19, NFR-01 |
| BUY-9 | Given 64rem or wider, then the bar is not rendered and the sticky buying panel is used | design |
| BUY-10 | Given the product page, then three perks appear under the buy button: delivery, exchange and WhatsApp, each as text from the settings (fee and window, exchange window, WhatsApp link); none is typed into a template | design, PR-14 |
| BUY-11 | Given the settings have no WhatsApp number or no delivery fact, then that perk is not shown rather than shown empty | (new) |

## Non-functional constraints

NFR-01 (no CLS when the bar appears: it is fixed-position), NFR-17 (mobile first), NFR-19 (WCAG 2.2 AA). SEO-01: the price and the main button already exist in the server HTML, so the bar adds no indexable content of its own.

## Out of scope

Accordions (the content model has no accordion block; see the PR-13 note), the per-product fabric band and the "Complete the look" rail (D5, waits for `product-recommendations`).

## Open questions

- [ ] **Q-BUY-1** Perks text: the design lists delivery, exchange and WhatsApp. Take the wording from settings (recommended) or fixed copy?
- [ ] **Q-BUY-2** Show the bar from tablet up to 64rem as well, or phone only (under 40rem)? Recommendation: under 64rem, because the design's two-column desktop layout starts at 64rem.
- [ ] **Q-BUY-3** On a product with several colours, should the bar show the chosen colour name next to the price? Recommendation: no, keep it to price and button.
