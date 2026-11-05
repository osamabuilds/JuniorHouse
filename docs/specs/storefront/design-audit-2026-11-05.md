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

## Conformance gaps found by comparing previews with the live pages (2026-11-05, owner: "follow whatever the design folder says")

Method: the `rp-` classes used by each `components/<Name>/preview.html` were compared with the classes the live page renders (1280 px), then each real difference was checked against the component README. Differences caused only by demo data (empty cart, no checkout) or by preview wrapper classes (`rp-vp`, `rp-hide-sm`) are left out. The owner decisions below the table already override the previews and are **not** treated as gaps.

| # | Design says | Live page | Kind |
|---|---|---|---|
| G1 | **Listing** (`ListingPage`): sticky `FilterBar` (`rp-filterbar__in`), phone bottom-sheet drawer, colour swatches (`rp-swatches`), active-filter chips, 12 at a time then a "Show more" ghost button with "Showing 12 of 48", an editorial banner (`rp-banner`) after the fourth card | A form-style filter panel (`rp-filters`), numbered pagination, no drawer, no swatches, no banner | Rebuild of the filter and paging UI. **Conflict:** the approved `listings` spec chose server-rendered pagination for SEO (SEO-06); "Show more" needs a decision |
| G2 | **Product card** (`ProductCard`): second image on hover and a swipe with page dots (`rp-slide`, `rp-pagedots`), Quick view (`rp-qv`), "low stock" tag (`rp-tag--low`), meta line (`rp-card__meta`) | Single image, wishlist heart, sale tag, price, availability line | New features (quick view, second image, low-stock tag) |
| G3 (fixed 2026-11-05) | **Gallery** (`Gallery`, `Lightbox`): `rp-gallery`, swipe with count and arrows, thumbnails grid on desktop, lightbox with pinch zoom | Own markup (`gallery`, `gallery__item`), a plain list of images, no lightbox | Rebuild to the design's structure and class names |
| G4 | **Product page**: accordions (`rp-acc`), per-product fabric band (`rp-fabric`), "Complete the look" rail, perks, sticky buy bar | None of these | Already specified or waiting: buy bar and perks (`product-buy-bar`), rail (`product-recommendations`), accordions (PR-13 note) |
| G5 | **Mega menu**: image tiles (`rp-mega__tile`) and `display-xs` column titles | Link columns only | New feature (needs images per entry) |
| G6 | **Collections page** | Not built (404) | Specified and planned: `collections-page` |
| G7 | **Retracted.** Auth screens: the class comparison flagged `rp-formgrid`, `rp-notice`, `rp-label`, but the live pages already use them (sign-in, forgot password, register) and the split photo layout exists. The preview only showed states the demo data does not trigger | none |
| G8 | **Retracted.** Content page: `rp-callout`, `rp-fact` and `rp-table` exist in the styles and the block component uses them; the demo content simply has no such blocks. Accordions remain the PR-13 note | none |
| G9 | **Home**: card slides, Quick view and low-stock tag (same as G2) | as G2 | covered by G2 |

**Owner decisions that already override the previews (kept as they are unless you say otherwise):** the footer is light, with no big decorative wordmark (Q-CR-3, CR-3); the desktop header lays out wordmark, navigation, search (CR-1); the account control in the header (not in the design); black and white colours and Manrope (2026-10-24). The design's `SiteFooter` preview is therefore intentionally not matched.

**Cart, checkout and wishlist** could not be compared in the demo data (empty cart, checkout redirects, empty wishlist). They need a seeded cart to audit.

### Proposed order (each group is a spec, then plan and tasks, then build)

1. **Product page**: G3 gallery and lightbox, then the existing `product-buy-bar` (G4).
2. **Listing**: G1 filter bar and drawer, swatches, banner; decide Show more vs pagination first.
3. **Product card**: G2 (second image, low-stock tag, quick view).
4. **Mega menu tiles** G5, and the cart/checkout/wishlist audit with a seeded cart.

**Specs approved with plans and tasks, 2026-11-05:** [product-gallery-lightbox](product-gallery-lightbox/spec.md) (G3), [listing-filter-bar](listing-filter-bar/spec.md) (G1, pagination kept by decision), [product-card-quick-add](product-card-quick-add/spec.md) (G2), [mega-menu-tile](mega-menu-tile/spec.md) (G5). Buy bar and collections page already have approved plans.
