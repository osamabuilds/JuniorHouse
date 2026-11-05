# Storefront design audit against `docs/design-system` (2026-11-05, first pass)

Method: an automated sweep of 20 storefront URLs (status, title, canonical, meta, JSON-LD, landmarks, heading order, alt text, unnamed buttons), a grep for raw colours and fonts in component styles, and a visual comparison of the home page (desktop) and the product page (phone) with `Pages.md` and the component READMEs. Tool: Playwright against the rebuilt storefront container.

**Coverage so far:** home and product page compared visually; every page swept structurally. **Not yet compared visually:** listing, search, cart, wishlist, sign-in/account, content pages, 404/410, the cart drawer, quick add and the search overlay.

## Fixed in this PR

| # | Finding | Fix |
|---|---|---|
| A1 | Listings and search results skipped from `<h1>` to `<h3>` (product names) with no `<h2>`; breaks "headings never skip a level" (CLAUDE.md, WCAG) | Visually hidden `<h2>` ("Products" / "Search results") above the grid; unit tests `LST-12` and `SRC-1` assert no level jump |

## Passing

- One `<h1>`, one `<main>`, canonical, meta description, Open Graph, and JSON-LD on every public page; no images without `alt`; no unnamed buttons; no clickable `div`s.
- Component styles contain no raw colours and no font families outside the tokens (the only hex values are in the placeholder garment illustration).
- Home page section order matches `Pages.md`: hero, audience split, New in, editorial band, gifts, fabric band, trust line, newsletter, footer. Hero height, one light button and one link match the Hero README.

## Needs an owner decision (not changed)

| # | Finding | Why it is not a silent fix |
|---|---|---|
| D1 | **The design system contradicts itself.** `tokens.json` carries the black and white palette (`surface-dark #111111`, `action`, `sale`) that the app uses, but `handoff/storefront/_tokens.scss` and the header of `components/bundle.css` still describe espresso `#2a1f1a`, warm off-white and Instrument Serif, and `tokens.json` still lists Instrument Serif while `Implementation.md` says Manrope. `Pages.md` also calls the editorial band "espresso". The app follows the owner decision of 2026-10-24 (black and white, Manrope, one marigold action colour) | Two sources disagree; the design folder should be brought in step (the app's `_tokens.scss` comment already says to keep `tokens.json` in step) |
| D2 | **`/collections` returns 404.** The footer link "Collections" and the nav entry (`kind: collection-list`, `/collections`) point at it, but only `collections/:code` is routed. `CollectionsPage` exists in the design system and was never built | Needs a spec (what the page lists, how it is ordered, empty state) |
| D3 | **Phone header: "Sign in" wraps onto two lines** beside the wordmark at 390 px (the 12 px label has about 59 px between the wordmark and the wishlist icon). Signed in, "greeting + Sign out" is wider still. The design's `SiteHeader` has no account control, and the mobile menu has no account link, so hiding it would remove sign-in on phones | Choose: an account icon in the header (recommended, 44 px like the other icons), or a Sign in entry in the mobile menu |
| D4 | **Product page on a phone has no sticky buy bar.** `ProductDetail` and `Pages.md` require a bottom bar with price and Add to bag; only the header is sticky. `product-pages` and `pages-redesign` do not list it, so it was never specified. The same README also lists three perks (delivery, exchange, WhatsApp) | Needs spec criteria before building; `scroll-padding-bottom` must equal the bar height (WCAG 2.4.11) |
| D5 | Product page lacks the per-product `FabricBand` and the "Complete the look" rail; accordions are not built (`PR-13` already records the missing accordion block). The rail overlaps the draft `product-recommendations` spec | Wait for the recommendations decision |
| D6 | The audience split with one audience renders a single half-width tile, leaving the right half empty. The design assumes Girls and Boys; demo data has only Girls | Decide whether one tile should span the full width |
| D7 | 404 pages carry a canonical pointing at the missing URL | Minor SEO hygiene; drop the canonical on error pages? |

## Next

Visual pass over the remaining pages (listing and search at 390 and 1280, cart and drawer, wishlist, sign-in, content pages, 404), then the fixes from D2 to D6 once the owner has decided.
