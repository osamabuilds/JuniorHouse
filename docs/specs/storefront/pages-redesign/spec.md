# SCRUM-xxx: Storefront pages and discounts to the design system (PR #52)

- **Status:** Draft (awaiting owner approval)
- **Jira:** SCRUM-xxx (to be created under SCRUM-203)
- **BRD sections:** §4.1 FEAT-CAT-01..04, FEAT-PDP-01..04, §14
- **Requirement IDs:** FR-01 (presentation), FR-02, FR-03, FR-07 (presentation), NFR-17, NFR-19, SEC-09, SEO-01, SEO-03, SEO-04, SEO-06
- **Design source:** `docs/design-system/` (`Offers`, `ProductCard`, `QuickAdd` size line, `NotFoundPage`, `ContentPage`, `CollectionsPage`). Storefront only; the admin is untouched. Amends [listings](../listings/spec.md), [product-pages](../product-pages/spec.md) and [storefront-settings](../storefront-settings/spec.md) presentation; it does not change their behaviour or data.
- **Branch:** `SCRUM-xxx-pages-redesign`, cut from `main`.

## Problem & intent
PR #52 added the discount treatment (`Offers`), the card size line (`QuickAdd`) and layouts for the 404/410 pages, the help/content pages and the collections index. The owner wants the shipped storefront to match. Only screens whose data the API **already serves** are in scope. Screens that need a cart, accounts, orders or new endpoints are listed under Out of scope and wait for their own features.

## The look is not changing
The live tokens (black and white, Manrope, marigold `--action`) already match `tokens.json` colours and the design-system README. The handoff SCSS and the font entries in `tokens.json` still show the older clay/serif look; they are **not** the target (see Q-PR-1). New CSS is taken from `components/bundle.css` and the component READMEs, mapped onto the live tokens.

## What changes
| Piece | Today | Design system | Change |
|---|---|---|---|
| Discounts on cards, listings and the product page | Compare-at price exists in the read model and DTOs; no sale styling | `Offers`: `−NN%` badge (`rp-tag--sale`), bold sale price with struck old price (`rp-price--sale`), "Save Rs. X" (`rp-save`), product-page pill (`rp-off`) | Render from the server's values; add the `--sale` token with a contrast test |
| Card size line | Sizes only on the product page | `QuickAdd`: always-visible size line (`rp-avail`), sold-out sizes struck through | Size line on cards from existing availability data. The add-to-bag bar and round **+** are **not built** (needs cart, Out of scope) |
| 404 / 410 / 503 | Real status codes, plain layout | `NotFoundPage` layouts: 404 with search field and links; 410 "left the collection" with a similar-pieces row; 503 message | Restyle; status codes unchanged |
| Footer / help pages | Settings content pages, generic layout | `ContentPage`: eyebrow, left help menu (`rp-sidenav`), `rp-prose` article, blocks (fact rows, callout, table, accordion) | Layout and block styles; content stays settings data |
| Collections index | Collection listings exist (`/collections/:code`) | `CollectionsPage` two-up tile grid | Needs a list of enabled, non-empty collections (Q-PR-3) |
| `ProductCard` | Tag text, no badge | Sale badge replaces `New` / `Low stock` tag when discounted; none on sold-out | Per `ProductCard` and `Offers` |

## Acceptance criteria
| ID | Given / When / Then | Source |
|---|---|---|
| PR-1 | Given a product whose server price is below its compare-at price, then its card, listing tile and product page show the `−NN%` badge, the bold sale price in `--sale`, the old price in `<s>` preceded by visually hidden "Was" (current price "Now"), and "Save Rs. X" | Offers; Catalog D4 |
| PR-2 | Given the percentage and rupee saving, then they are the server's figures; the browser never calculates a price, percentage or total | SEC-09; Offers rules |
| PR-3 | Given a product with no compare-at price (or one not above the price), then no badge, strike or saving line is shown | Offers (no fake "was") |
| PR-4 | Given a sold-out product, then it keeps its sold-out state and shows no discount badge | Offers |
| PR-5 | Given a product with several sizes at different prices, then the tile shows the lowest price and the biggest saving; the product page shows "From Rs. X" and "Up to −NN%" | Offers |
| PR-6 | Given `--sale`, then white on `--sale` and `--sale` on every light surface pass 4.5:1 in the tokens contrast test, and the tokens and style-rules tests still pass (no raw colours in component SCSS) | Offers; design-foundation |
| PR-7 | Given a discount, then it is never signalled by colour alone (minus sign or the word "Save" is always present) | NFR-19 |
| PR-8 | Given a listing card, then a size line under the price lists every size of the product, none truncated, with sold-out sizes struck through and "sold out" in the accessible name | QuickAdd (size line) |
| PR-9 | Given a card, then there is no add-to-bag control, no round **+** and no hover bar until the cart exists | Out of scope (cart is Sprint 11) |
| PR-10 | Given an unknown address, then the response is 404 with the `NotFoundPage` layout (large decorative `404` as `aria-hidden`, one `<h1>`, a search field, a home link, top-level links) and `noindex` | NotFoundPage; SEO-01 |
| PR-11 | Given a permanently removed product, then the response is 410 with "This piece has left the collection", a shop link and a similar-pieces row; a moved product is still a 301 | NotFoundPage; product-pages |
| PR-12 | Given a temporary failure, then the response is 503 with the stated message in the same layout | NotFoundPage |
| PR-13 | Given a settings content page, then it renders with breadcrumb, eyebrow, one `<h1>`, the help menu beside the article (above it on a phone), and its blocks as semantic `<dl>`, callout, `<table>` with `<th scope>`, lists and `<details>` accordions | ContentPage; CLAUDE.md semantic HTML |
| PR-14 | Given a content page, then every fact (fee, window, WhatsApp, hours) still comes from settings, none is typed into a template, and a draft page is never linked or served | ContentPage; site-chrome SC-14 |
| PR-15 | Given a content page, then it keeps its unique title and description, self-referencing canonical and `BreadcrumbList` JSON-LD in the server HTML | SEO-03, SEO-04 |
| PR-16 | Given the collections index (if Q-PR-3 is approved), then only collections with at least one live product are listed, as tiles linking to `/collections/<code>`, indexable with title, description, canonical and `BreadcrumbList` | CollectionsPage; SEO-03, SEO-04 |
| PR-17 | Given every changed screen at 390, 768 and 1280 px, then there is no horizontal scroll, targets are at least 44px, focus is visible and not obscured, axe has no violations, and text contrast is 4.5:1 | NFR-17, NFR-19 |
| PR-18 | Given the diff, then it touches only the storefront project, its styles and tests (and the API only if Q-PR-3 needs it); no admin project, no formatting-only changes | owner instruction |
| PR-19 (new) | Given the home, category and product pages, then the Lighthouse budgets in CLAUDE.md still hold (SEO 100, Accessibility 95 or more, Performance 90 or more on mobile, LCP 2.5s or less, CLS 0.1 or less) | NFR-01, SEO-10 |

## Non-functional constraints
Server-rendered HTML contains the badge, prices and size line (SEO-01); discount figures arrive in the existing responses (no extra request per card, no N+1); NFR-17 mobile-first; NFR-19 WCAG 2.2 AA; SEC-09 server-calculated figures.

## Out of scope
- **Needs a cart (Sprint 11):** `QuickAdd` bar and **+**, `CartDrawer`, `CartPage`, `CartEmpty`, `CheckoutPage`, `OrderConfirmation`, bag savings rows, free-delivery bar, promo-code field.
- **Needs accounts (Sprint 10, FR-39 vs SEC-02 unresolved):** `AuthScreens`, `AccountPages`, `ReturnRequest`, signed-in wishlist merge.
- **Wishlist page and heart state** (storefront README puts it in S10).
- **`SearchPage`:** its design is applied when the approved [search](../search/spec.md) feature is built, not here.
- **`ContactPage` form:** needs a backend to store messages and spam protection (Q-PR-4). **`StoryPage`:** copy is owner-supplied (Q-PR-5). **`CookieBanner`:** legal decision (Q-PR-6).
- Newsletter, Account and Bag icons in the header (still deferred, as in chrome-redesign).
- Fit Finder and Virtual Try-On (descoped); the admin app.
- Promo codes, free-delivery threshold, stacking rules (business decisions, not drawn).

## Open questions
- [ ] **Q-PR-1 Stale design-system files.** `handoff/storefront/_tokens.scss`, `_components.scss` and the font entries of `tokens.json` still describe clay/serif, and `bundle.css`'s header says "Instrument Serif". Confirm the black/white Manrope look is final and that those files are to be refreshed by whoever maintains the design system. This spec takes values from `bundle.css` and the component READMEs and never copies the handoff SCSS over the live tokens. Blocks: the CSS task.
- [ ] **Q-PR-2 `--sale` colour.** The design system marks `--sale` (#A8321F, alias of `danger-text`) as PROPOSED, not yet in the live tokens. Approve it as a new token? It is the only red in the shop. Blocks: all discount criteria.
- [ ] **Q-PR-3 Collections index.** Nothing today lists enabled, non-empty collections. Add a small query and endpoint to the Storefront module (listings already knows collections), or leave the index out? Blocks: PR-16 and the plan's API tasks.
- [ ] **Q-PR-4 Contact page.** Build only the fact aside now (form deferred), or specify a contact-message feature (store, spam protection, no personal data in logs, SEC-25) as its own spec?
- [ ] **Q-PR-5 Story page.** Copy and photography come from the owner. Provide them, or defer the page?
- [ ] **Q-PR-6 Cookie banner.** Is a consent banner needed at all, and what wording? Nothing optional (analytics) loads today. Defer until a legal decision?
- [ ] **Q-PR-7 Sale filter chips.** `Offers` draws range chips on the listing (All sale, Up to 20%, 20–40%, 40% or more). The approved listings spec has a Sale view but no discount-range filter. Add it as a new filter (needs spec change and server support), or skip?
- [ ] **Q-PR-8 Jira key.** Which SCRUM key should this carry?
