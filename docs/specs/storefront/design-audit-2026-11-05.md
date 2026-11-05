# Storefront design audit against `docs/design-system` (2026-11-05, first pass)

Method: an automated sweep of 20 storefront URLs (status, title, canonical, meta, JSON-LD, landmarks, heading order, alt text, unnamed buttons), a grep for raw colours and fonts in component styles, and a visual comparison of the home page (desktop) and the product page (phone) with `Pages.md` and the component READMEs. Tool: Playwright against the rebuilt storefront container.

**Coverage so far:** home and product page compared visually; every page swept structurally. **Not yet compared visually:** listing, search, cart, wishlist, sign-in/account, content pages, 404/410, the cart drawer, quick add and the search overlay.

## Fixed in this PR

| # | Finding | Fix |
|---|---|---|
| A1 | Listings and search results skipped from `<h1>` to `<h3>` (product names) with no `<h2>`; breaks "headings never skip a level" (CLAUDE.md, WCAG) | Visually hidden `<h2>` ("Products" / "Search results") above the grid; unit tests `LST-12` and `SRC-1` assert no level jump |
| D1 | Design system contradicted itself (espresso and Instrument Serif vs the black and white, Manrope app) | Owner chose to align the folder with the app. Updated `tokens.json` (fonts, usage text), `handoff/storefront/_tokens.scss` (now the live tokens), the `README.md` colour paragraph, `Pages.md` and the `bundle.css` comments. **Still stale:** component READMEs and previews (Button, EditorialBand, HomePage, SiteFooter, SiteHeader, Admin*) and `handoff/storefront/_components.scss` (differs from the live styles in about 160 places); they need a per-component review |
| D3 | Phone header "Sign in" wrapped onto two lines | One 44px account icon below 40rem (to sign-in, or the account page when signed in); the words stay from 40rem. Unit tests `HDR-1`, e2e on phone and desktop |
| D6 | One audience gave a half-empty row | One audience renders a full-width 16:9 tile; two or more keep two columns. Unit and e2e tests |
| D7 | Error pages carried a canonical and og:url for the missing address | `SeoService` `error` option removes them; applied to listing, search, product and content pages. Unit and e2e tests |

## Passing

- One `<h1>`, one `<main>`, canonical, meta description, Open Graph, and JSON-LD on every public page; no images without `alt`; no unnamed buttons; no clickable `div`s.
- Component styles contain no raw colours and no font families outside the tokens (the only hex values are in the placeholder garment illustration).
- Home page section order matches `Pages.md`: hero, audience split, New in, editorial band, gifts, fabric band, trust line, newsletter, footer. Hero height, one light button and one link match the Hero README.

## Decided on 2026-11-05 (owner accepted the recommendations), specs first

D2 and D4 need a spec (drafts: `collections-page`, `product-buy-bar`); D5 waits for the `product-recommendations` decision.

## Still listed for reference

| # | Finding | Why it is not a silent fix |
|---|---|---|
| D2 | **`/collections` returns 404.** The footer link "Collections" and the nav entry (`kind: collection-list`, `/collections`) point at it, but only `collections/:code` is routed. `CollectionsPage` exists in the design system and was never built | Needs a spec (what the page lists, how it is ordered, empty state) |
| D4 | **Product page on a phone has no sticky buy bar.** `ProductDetail` and `Pages.md` require a bottom bar with price and Add to bag; only the header is sticky. `product-pages` and `pages-redesign` do not list it, so it was never specified. The same README also lists three perks (delivery, exchange, WhatsApp) | Needs spec criteria before building; `scroll-padding-bottom` must equal the bar height (WCAG 2.4.11) |
| D5 | Product page lacks the per-product `FabricBand` and the "Complete the look" rail; accordions are not built (`PR-13` already records the missing accordion block). The rail overlaps the draft `product-recommendations` spec | Wait for the recommendations decision |

## Next

Visual pass over the remaining pages (listing and search at 390 and 1280, cart and drawer, wishlist, sign-in, content pages, 404), then the fixes from D2 to D6 once the owner has decided.
