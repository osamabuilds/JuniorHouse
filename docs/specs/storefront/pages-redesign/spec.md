# SCRUM-xxx: Storefront pages and discounts to the design system (PR #52)

- **Status:** Implemented (2026-10-25); Lighthouse CI budget run (PR-18) pending
- **Jira:** SCRUM-xxx (to be created under SCRUM-203)
- **BRD sections:** §4.1 FEAT-CAT-01..04, FEAT-PDP-01..04, §14
- **Requirement IDs:** FR-01 (presentation), FR-02, FR-03, FR-07 (presentation), NFR-17, NFR-19, SEC-09, SEO-01, SEO-03, SEO-04, SEO-06
- **Design source:** `docs/design-system/` (`Offers`, `ProductCard`, `QuickAdd` size line, `NotFoundPage`, `ContentPage`, `CollectionsPage`). Storefront only; the admin is untouched. Amends [listings](../listings/spec.md), [product-pages](../product-pages/spec.md) and [storefront-settings](../storefront-settings/spec.md) presentation; it does not change their behaviour or data.
- **Branch:** `SCRUM-xxx-pages-redesign`, cut from `main`.

## Problem & intent
PR #52 added the discount treatment (`Offers`), the card size line (`QuickAdd`) and layouts for the 404/410 pages, the help/content pages and the collections index. The owner wants the shipped storefront to match. Only screens whose data the API **already serves** are in scope (one additive field on the listing tile is the single API change). Screens that need a cart, accounts, orders or new endpoints are listed under Out of scope and wait for their own features.

## The look is not changing
The live tokens (black and white, Manrope, marigold `--action`) already match `tokens.json` colours and the design-system README. The handoff SCSS and the font entries in `tokens.json` still show the older clay/serif look; they are **not** the target (see Q-PR-1). New CSS is taken from `components/bundle.css` and the component READMEs, mapped onto the live tokens.

## What changes
| Piece | Today | Design system | Change |
|---|---|---|---|
| Discounts on cards, listings and the product page | Badge text, "Now/Was" text and struck price already render; the badge uses the plain `rp-tag`, there is no `--sale` colour, no "Save Rs. X" line | `Offers`: `−NN%` badge (`rp-tag--sale`), bold sale price with struck old price (`rp-price--sale`), "Save Rs. X" (`rp-save`), product-page pill (`rp-off`) | Restyle to the `Offers` pieces from the server's values; add the `--sale` token with a contrast test |
| Card size line | Sizes only on the product page | `QuickAdd`: always-visible size line (`rp-avail`), sold-out sizes struck through | Size line on cards. The tile DTO has only `HasSoldOutSizes`, so one additive field (size code + sold-out flag per size) is added to `ListingTileDto`, filled by the existing listing query without extra queries. The add-to-bag bar and round **+** are **not built** (needs cart, Out of scope) |
| 404 / 410 / 503 | Real status codes, plain layout | `NotFoundPage` layouts: 404 with links; 410 "left the collection"; 503 message | Restyle; status codes unchanged. The search field (404) and the similar-pieces row (410) are left out: there is no search page and no source for similar pieces yet |
| Footer / help pages | Settings content pages, generic layout | `ContentPage`: eyebrow, left help menu (`rp-sidenav`), `rp-prose` article, blocks (fact rows, callout, table, accordion) | Layout and block styles; content stays settings data |
| Collections index | Collection listings exist (`/collections/:code`) | `CollectionsPage` two-up tile grid | **Not built** (no endpoint; deferred, Q-PR-3) |
| `ProductCard` | Tag text, no badge | Sale badge replaces `New` / `Low stock` tag when discounted; none on sold-out | Per `ProductCard` and `Offers` |

## Acceptance criteria
| ID | Given / When / Then | Source |
|---|---|---|
| PR-1 | Given a product whose server price is below its compare-at price, then its card, listing tile and product page show the `−NN%` badge, the bold sale price in `--sale`, the old price in `<s>` preceded by visually hidden "Was" (current price "Now"), and "Save Rs. X" | Offers; Catalog D4 |
| PR-2 | Given the percentage and rupee saving, then they are the server's figures; the browser never calculates a price, percentage or total | SEC-09; Offers rules |
| PR-3 | Given a product with no compare-at price (or one not above the price), then no badge, strike or saving line is shown | Offers (no fake "was") |
| PR-4 | Given a sold-out product, then it keeps its sold-out state and shows no discount badge | Offers |
| PR-5 | Given a product with several sizes at different prices, then the tile shows the lowest matching price with that variant's compare-at price and saving (LST-12, as shipped) and the product page shows "From Rs. X" (PDP-15, as shipped) | Offers; LST-12 |
| PR-6 | Given `--sale`, then white on `--sale` and `--sale` on every light surface pass 4.5:1 in the tokens contrast test, and the tokens and style-rules tests still pass (no raw colours in component SCSS) | Offers; design-foundation |
| PR-7 | Given a discount, then it is never signalled by colour alone (minus sign or the word "Save" is always present) | NFR-19 |
| PR-8 | Given a listing card, then a size line under the price lists every size of the product (from the tile's new per-size field, in the same listing query with no extra query per tile), none truncated, with sold-out sizes struck through and "sold out" in the accessible name | QuickAdd (size line) |
| PR-9 | Given a card, then there is no add-to-bag control, no round **+** and no hover bar until the cart exists | Out of scope (cart is Sprint 11) |
| PR-10 | Given an unknown address, then the response is 404 with the `NotFoundPage` layout (large decorative `404` as `aria-hidden`, one `<h1>`, a home link, top-level links) and `noindex` | NotFoundPage; SEO-01 |
| PR-11 | Given a permanently removed product, then the response is 410 with "This piece has left the collection" and a shop link; a moved product is still a 301 | NotFoundPage; product-pages |
| PR-12 | Given a temporary failure, then the response is 503 with the stated message in the same layout | NotFoundPage |
| PR-13 | Given a settings content page, then it renders with breadcrumb, eyebrow, one `<h1>`, the help menu beside the article (above it on a phone), and its blocks as semantic `<dl>`, callout, `<table>` with `<th scope>`, lists and `<details>` accordions | ContentPage; CLAUDE.md semantic HTML |
| PR-14 | Given a content page, then every fact (fee, window, WhatsApp, hours) still comes from settings, none is typed into a template, and a draft page is never linked or served | ContentPage; site-chrome SC-14 |
| PR-15 | Given a content page, then it keeps its unique title and description, self-referencing canonical and `BreadcrumbList` JSON-LD in the server HTML | SEO-03, SEO-04 |
| PR-16 | Given every changed screen at 390, 768 and 1280 px, then there is no horizontal scroll, targets are at least 44px, focus is visible and not obscured, axe has no violations, and text contrast is 4.5:1 | NFR-17, NFR-19 |
| PR-17 | Given the diff, then it touches only the storefront project, its styles and tests and the one additive Storefront listing-tile field; no admin project, no formatting-only changes | owner instruction |
| PR-18 (new) | Given the home, category and product pages, then the Lighthouse budgets in CLAUDE.md still hold (SEO 100, Accessibility 95 or more, Performance 90 or more on mobile, LCP 2.5s or less, CLS 0.1 or less) | NFR-01, SEO-10 |

## Non-functional constraints
Server-rendered HTML contains the badge, prices and size line (SEO-01); discount figures arrive in the existing responses (no extra request per card, no N+1); NFR-17 mobile-first; NFR-19 WCAG 2.2 AA; SEC-09 server-calculated figures.

## Out of scope
- **Needs a cart (Sprint 11):** `QuickAdd` bar and **+**, `CartDrawer`, `CartPage`, `CartEmpty`, `CheckoutPage`, `OrderConfirmation`, bag savings rows, free-delivery bar, promo-code field.
- **Collections index page** (Q-PR-3), **404 search field** (until the search page exists) and the **410 similar-pieces row** (no data source).
- **Needs accounts (Sprint 10, FR-39 vs SEC-02 unresolved):** `AuthScreens`, `AccountPages`, `ReturnRequest`, signed-in wishlist merge.
- **Wishlist page and heart state** (storefront README puts it in S10).
- **`SearchPage`:** its design is applied when the approved [search](../search/spec.md) feature is built, not here.
- **`ContactPage` form:** needs a backend to store messages and spam protection (Q-PR-4). **`StoryPage`:** copy is owner-supplied (Q-PR-5). **`CookieBanner`:** legal decision (Q-PR-6).
- Newsletter, Account and Bag icons in the header (still deferred, as in chrome-redesign).
- Fit Finder and Virtual Try-On (descoped); the admin app.
- Promo codes, free-delivery threshold, stacking rules (business decisions, not drawn).

## Open questions
- [x] **Q-PR-1 Stale design-system files.** Resolved: black/white Manrope is final. The handoff SCSS, `tokens.json` font entries and `bundle.css` header are stale and are not copied. Report the stale files to the design-system maintainer.
- [x] **Q-PR-2 `--sale` colour.** Resolved: approved (#A8321F, alias of `danger-text`), added to the live tokens with the contrast test.
- [x] **Q-PR-3 Collections index.** Resolved: deferred (no new endpoint now).
- [x] **Q-PR-4 Contact page.** Resolved: deferred, with the form (needs its own spec).
- [x] **Q-PR-5 Story page.** Resolved: deferred until the owner supplies copy and photography.
- [x] **Q-PR-6 Cookie banner.** Resolved: deferred until a legal decision.
- [x] **Q-PR-7 Sale filter chips.** Resolved: skipped.
- [ ] **Q-PR-8 Jira key.** Which SCRUM key should this carry? (placeholder SCRUM-xxx until given, as in chrome-redesign)

## As built (2026-10-25)
Differences from the text above, recorded so the spec describes what exists:
- **Money (PR-2).** Added `SavingAmount` (server-computed decimal) to `ListingTileDto` and `ProductPriceDto`; the browser shows it and never subtracts. Added `Sizes` (code, name, sold-out) to `ListingTileDto`, loaded by one extra query per page (five queries per listing page now, still constant).
- **Existing discount markup.** The badge text, "Now/Was" text and struck price already existed; they now use `rp-tag--sale`, `rp-price--sale`, `rp-save` (card) and `rp-price--lg`, `rp-off`, "You save Rs. X" (product page). The old "Some sizes sold out" tag on cards is gone: the struck sizes in the size line say the same.
- **`.sr-only` was missing.** No stylesheet defined it, so the "Now/Was/sold out" text was visible. It is now defined and pinned by a test (PR-7).
- **PR-4** is implemented for a sold-out product (`isAvailable` false): the product page shows its plain price. Listings never list such a product (LST-15).
- **PR-13 accordions.** Not built: the settings content model has no accordion block (heading, paragraph, list, table, callout, fact). It needs a new block type in storefront-settings and the admin editor, a separate feature. Facts that are text or links are now `<dl>` rows; list and delivery facts keep a heading.
- **Help menu** lists every published page in the order the settings give; draft pages are never linked.
- **404 button.** "Back to home" uses the standard ink button, not marigold (the live button variants are primary, ghost and light).
- **Not-found layout** is one shared component, `rp-not-found`, used by the page, listing and product containers.
- **PR-18 (Lighthouse budgets)** is enforced in CI and was not run locally.
- **Stale design-system files** (handoff SCSS, font entries in `tokens.json`, `bundle.css` header) were not touched; they are for the design-system maintainer (Q-PR-1).
- Playwright: 12 tests (4 pages at 390, 768, 1280) passed against the rebuilt Docker stack with the real API; backend 424 and web 251 unit tests pass.
