# Implementation guide (for Claude Code)

Build exactly this. The previews in `components/*/preview.html` are the source of truth for markup, classes and behaviour; `components/bundle.css` for styling; `tokens.json` for values. Do not redesign, substitute fonts, add radius, shadows, gradients, badges or colours. The design is deliberately quiet: if a screen feels busy, something was added, and if a UI element competes with a photograph, remove it.

Scope: **storefront only** (`src/web/projects/storefront`). Do not touch the admin project or any unrelated file, and make no formatting-only changes.

## Process (repo rules in CLAUDE.md still apply)

1. Explore first: read the storefront's current structure, styles and any existing components, and reuse what exists (map each preview to an existing component where one fits instead of creating a parallel one). Then spec first: one spec per story, approved before code. Suggested stories: (1) tokens, fonts, global styles, image component and shared UI; (2) header, mega-menu, search, footer, cart drawer; (3) home; (4) listing; (5) product page with gallery, lightbox and quick view.
2. Commit messages start with `SCRUM-xxx:`. Semantic HTML, SEO, WCAG 2.2 AA and the performance budgets in CLAUDE.md are non-negotiable. The Fit Finder is not built.
3. Products, prices, images, the wordmark and all copy in the previews are **placeholders**. Keep them in one mock-data file (`shared/data/mock-products.ts`) and replace later with the API. Every product needs `images[]` (front, second view, macro, detail), each with `src`, `width`, `height`, `alt` and a tiny `blurDataUrl`.

## Files

| From this design | To |
|---|---|
| `handoff/storefront/_tokens.scss` | `src/styles/_tokens.scss` |
| `handoff/storefront/_components.scss` | `src/styles/_components.scss` |

`src/styles.scss`: `@use './styles/tokens'; @use './styles/components';` plus a small reset (`body { margin: 0; background: var(--surface); color: var(--ink); font: 400 16px/26px var(--font-sans); } html { scroll-padding-top: 96px; scroll-padding-bottom: 80px; }`). Put `class="rp-root"` on the app root element. Tokens live in one place: no raw colours, sizes or durations in component SCSS.

Fonts (no CDN): `npm i @fontsource/instrument-serif @fontsource-variable/dm-sans`; add `@fontsource/instrument-serif/400.css`, `@fontsource/instrument-serif/400-italic.css` and `@fontsource-variable/dm-sans/index.css` to the `styles` array in `angular.json`. `_tokens.scss` already lists `'Instrument Serif'` and `'DM Sans Variable'` first. Preload only the Instrument Serif 400 latin file and the DM Sans latin file; `font-display: swap`.

## Structure

Standalone components, `ChangeDetectionStrategy.OnPush`, signals, new control flow. Layout under `src/web/projects/storefront/src/app` (adapt to the existing folders you find):

| Path | Selector | Matches preview | Notes |
|---|---|---|---|
| `layout/site-header` | `rp-site-header` | SiteHeader | sticky; nav from a const array (New in, Girls, Boys, Baby, Gifts) |
| `layout/mega-menu` | `rp-mega-menu` | MegaMenu | hover and focus-in (desktop), click anywhere; Esc closes; age links use `?age=` |
| `layout/mobile-menu` | `rp-mobile-menu` | SiteHeader (phone) | left drawer, `<dialog>` |
| `layout/search-overlay` | `rp-search-overlay` | SearchOverlay | `<dialog>`, 200ms debounced suggestions |
| `layout/cart-drawer` | `rp-cart-drawer` | CartDrawer | `<dialog>` right drawer, bottom sheet under 40rem; cart state in the existing NgRx setup |
| `layout/site-footer` | `rp-site-footer` | SiteFooter | `rp-dark`; includes the newsletter form |
| `shared/ui/button`, `link-cta`, `icon-button`, `field` | attribute selectors on native elements, e.g. `button[rpBtn]` | Button, Field | keep native elements |
| `shared/ui/image` | `rp-image` | everything with a photo | wraps `NgOptimizedImage`; see Images; shows `rp-ph`/`rp-skel` when no `src` |
| `shared/ui/reveal.directive.ts` | `[rpReveal]` | Motion | see below |
| `shared/product-card` | `rp-product-card` | ProductCard, Skeleton | inputs: product, `wished`; outputs: `quickView`, `wishToggle` |
| `shared/product-rail` | `rp-product-rail` | ProductRail | prev/next scroll by one card |
| `shared/size-selector`, `size-guide` | `rp-size-selector`, `rp-size-guide` | SizeSelector | radiogroup with arrow keys |
| `shared/quick-view` | `rp-quick-view` | QuickView | `<dialog>` modal |
| `shared/lightbox` | `rp-lightbox` | Lightbox | see Lightbox |
| `features/home` | route `''` | HomePage | lazy; Hero, CategorySplit, EditorialBand, GiftsStrip, FabricBand, TrustLine as small components |
| `features/listing` | route `:category` | ListingPage | `FilterBar` (+ bottom sheet), grid; filters and sort in query params |
| `features/product` | route `p/:slug` | ProductPage | gallery, buying panel, accordions (native `<details>`), sticky buy bar, FabricBand |

Class names are `rp-*` exactly as in the previews and live in the global stylesheet; put only component-local layout in component SCSS. Breakpoints are 40rem and 64rem with `@media (min-width)`; `_components.scss` is already converted from the previews' container queries and `cqw` (now `vw`). The previews' `.rp-vp` wrapper is preview-only. Check phone (390), tablet (768) and desktop (1280) against `HomeResponsive`, `HomePage`, `ListingPage` and `ProductPage`.

## Images and performance (Core Web Vitals)

- One image component (`rp-image`) around `NgOptimizedImage`: fixed `width`/`height` or `fill` inside a box with `aspect-ratio: var(--image-ratio)` (4/5), so nothing shifts (CLS 0).
- Serve AVIF with WebP fallback and a JPEG last resort; `ngSrcset` at 400, 640, 828, 1080, 1440 and 1920 w; `sizes` per layout: grid card `(min-width: 64rem) 25vw, (min-width: 40rem) 33vw, 50vw`, gallery `(min-width: 64rem) 29vw, 100vw`, hero `100vw`. Use an image loader (CDN) or a build-time `sharp` script, whichever the repo already uses.
- LCP: the hero (home) and the first gallery image (product) get `priority` and `fetchpriority="high"` and a `<link rel="preload" as="image" imagesrcset imagesizes>`; target LCP under 2.5s on mobile 4G. Everything below the fold is `loading="lazy"` and `decoding="async"`. Second card images are requested on first `pointerenter`/focus, not on load. Lightbox source images load only when it opens.
- Placeholder: each image has a ~20px base64 `blurDataUrl` (`placeholder` / `placeholderConfig`), blurred then sharpened (`.rp-blur`/`.is-loaded`). Grids show `rp-skel` cards while data loads (`aria-busy`).
- Hero video (optional): see `Hero`. Never autoplay with sound; poster is the LCP image; skip it under reduced motion and Save-Data.

## Lightbox and gestures

Native `<dialog>` + `showModal()`. Zoom with Pointer Events: track up to two pointers; one tap or double-tap toggles 2x at the point; pinch scales between 1x and 4x; drag pans while zoomed; `touch-action: none` on the image. Left/Right arrows and swipe change image (swipe off while zoomed), `+`/`-` zoom, Esc closes. Every gesture has a visible button or key alternative. Card and gallery swipe is plain CSS `scroll-snap`; the dots and counter update from an `IntersectionObserver`/`scrollend` handler.

## Behaviour details

- **Reveal directive**: add `rp-js` to `<html>` and `.is-in` via `IntersectionObserver` (threshold 0.2, once), in the browser only (`afterNextRender`). Stagger with `style="--d:n"`. Server-rendered HTML must be fully visible; the hidden initial state exists only under `.rp-js`. Do nothing under `prefers-reduced-motion`.
- **Route transitions**: `provideRouter(routes, withViewTransitions())`, plus `::view-transition-old(root), ::view-transition-new(root) { animation-duration: var(--dur-slow); animation-timing-function: var(--ease-out); }` and a reduced-motion opt-out. Progressive enhancement: browsers without the API navigate normally.
- **Dialogs**: native `<dialog>` (focus trap, Esc, inert background); close on scrim click; restore focus to the opener; lock body scroll. Drawers slide with `transform` over `--dur-base` and `--ease-inout`.
- **Wishlist heart**: `<button aria-pressed>`; state in the NgRx store, persisted in local storage in the browser only.
- **Header search**: the desktop field opens the overlay on focus; phones use the icon. Submit goes to `/search?q=`.
- **SEO**: unique `<title>` and meta description per route, canonical URLs, `Product` JSON-LD on product pages, breadcrumbs as `<nav>` with `aria-current`.

## WCAG 2.2 AA checklist

Contrast per `tokens.json` usage notes; focus-visible 2px ring on everything interactive; targets at least 44px (2.5.8); focus not obscured by the sticky header or buy bar (2.4.11) via `scroll-padding`; no gesture without a button or key alternative (2.5.7); consistent help (WhatsApp link in the same place) (3.2.6); no cognitive tests in forms (3.3.8); labels above fields, specific error text; `aria-live` on the gallery counter and bag count; video loop has a pause control and respects reduced motion.

## Acceptance checklist

- Home, listing and product pages match the previews at 390, 768 and 1280 px (layout, colour, type, spacing, motion).
- Top navigation has exactly five items; search is visible on desktop and one tap away on phones; Add to bag and sizes are reachable without scrolling past the gallery on a phone.
- Product images are 4:5 everywhere with no layout shift; second image crossfades on desktop hover and swipes on touch; lightbox zooms by click, double-tap and pinch.
- Nothing sits on top of images except the hero text, the tiny tag and the heart. No badges, borders or shadows on images.
- One `<h1>` per page, landmarks as in `Pages.md`; dialogs trap focus and return it. Axe: no violations. Lighthouse mobile at or above the CLAUDE.md thresholds and LCP under 2.5s.
- `prefers-reduced-motion` removes reveal, crossfade, zoom, shimmer, view transitions and the hero loop.
- No colours, radii, shadows or durations outside `_tokens.scss`. Unit tests for the directive, lightbox and shared components; one Playwright smoke test per page.

## Out of scope

Checkout, accounts, the admin re-skin, real photography and video, the final logo and the final copy.

## Admin

Scope: `src/web/projects/admin`. Keep every component, route, store and template as they are; this is a skin plus a new shell. Spec first (a UI-only story) and no behaviour changes.

1. Copy `handoff/admin/_admin.scss` to `src/styles/_admin.scss` and `handoff/storefront/_tokens.scss` to the admin's own `_tokens.scss` (the admin and storefront are separate Angular projects, so each carries its own copy of the tokens). Import both from `styles.scss`, replacing the old `:root` variables (`--color-brass`, `--color-ink` and so on) and the global rules they restyle. Add `class="ad"` to the root element.
2. Fonts: replace the Google Fonts links in `index.html` (Fraunces, Inter) with `@fontsource/instrument-serif` and `@fontsource-variable/dm-sans`, as in the storefront.
3. `app.html` / `app.scss`: replace the header and wrapping nav with the sidebar shell from `AdminShell` (`ad-shell`, `ad-side`, `ad-top`, `ad-body`, `ad-main`, `ad-foot`); same links, grouped, with `routerLinkActive` setting `is-active` and `aria-current`. Phones: the same nav in a `<dialog>` drawer opened from the top bar. Keep `chromeless()` behaviour.
4. Map old variables if a component SCSS still uses them: `--color-ink` to `--ink`, `--color-ink-soft` to `--ink-muted`, `--color-paper` to `--surface`, `--color-surface` to `--surface-raised`, `--color-border` to `--line`, `--color-border-strong` to `--line-strong`, `--color-brass` to `--accent`, `--color-brass-dark` to `--accent-deep`, `--color-brass-tint` to `--surface-sunken`, `--color-danger` to `--danger-text`, `--color-success` to `--success-text`. Remove the Fraunces/Inter references and any shadows.
5. Acceptance: all existing admin unit tests pass unchanged; axe has no violations; targets at least 44px on phones; tables stack under 40rem; status is never colour alone; checked at 390, 768 and 1280 px against the Admin previews.
