# SCRUM-203: Storefront design foundation (Story 1 of 5)

- **Status:** Approved (owner, 2026-10-17; all Q-DF-1..10 proposed defaults accepted)
- **Jira:** SCRUM-203 (umbrella); SCRUM-204 (task under umbrella SCRUM-203)
- **BRD sections:** §14 (SEO and performance), NFR-01, NFR-17, NFR-19; design source `docs/design-system/` (Romp design system, "Gallery")
- **Requirement IDs:** NFR-01, NFR-17, NFR-19, SEO-07, SEO-10 (image and performance parts only)

## Problem & intent
The storefront project is an empty Angular scaffold. Every later page (header, home, listing, product) needs the same visual base: colour, type, spacing and motion tokens, self-hosted fonts, global styles, one image component that never shifts the layout, and the shared buttons and fields. Building that once, first, keeps the five stories consistent and stops pages from carrying their own colours, sizes or image handling. This story ships no page; it ships the foundation and a way to see it.

The design is a quiet gallery frame: product photography is the only loud thing on screen. Clay is an accent under 5% of the screen, and `clay-deep` (#9C5537) is the clay used wherever clay is text or a button fill. One light theme only.

## Requirements (quoted)
BRD and design-system text this story implements. Design-system rules are quoted from `docs/design-system/` (they are the visual source of truth for the storefront, owner instruction 2026-10-17).

> **NFR-01 (CLAUDE.md budgets):** SEO score 100, Accessibility ≥ 95, Performance ≥ 90 on mobile, LCP ≤ 2.5s, CLS ≤ 0.1, INP ≤ 200ms.

> **SEO-07 (CLAUDE.md):** Images: `NgOptimizedImage`, WebP/AVIF with `srcset`, explicit `width`/`height` (no layout shift), the product's main image loaded eagerly with `fetchpriority="high"`, everything else lazy-loaded. Alt text is required.

> **NFR-19 (CLAUDE.md):** Meet WCAG 2.1 AA: contrast, visible focus, full keyboard operation, meaningful alt text. (The design system and owner instruction raise this to WCAG 2.2 AA, see Q-DF-3.)

> **Design system, README:** "Every product image is 4:5 with one consistent crop." "Clay is the only accent and stays under 5% of the screen." "One theme only (no dark mode)." "Focus is a 2px charcoal outline with 3px offset (cream inside dark sections)."

> **Design system, Implementation.md:** "Tokens live in one place: no raw colours, sizes or durations in component SCSS." "One image component (`rp-image`) around `NgOptimizedImage`: fixed `width`/`height` or `fill` inside a box with `aspect-ratio: var(--image-ratio)` (4/5), so nothing shifts (CLS 0)."

## What exists today (explored 2026-10-17)
`src/web/projects/storefront` is the unmodified `ng new` scaffold: `app.html` is the Angular welcome placeholder, `app.routes.ts` is empty, `styles.scss` is empty, `index.html` has no fonts or metadata, `app.config.ts` has only `provideRouter` and `provideClientHydration`, `app.routes.server.ts` prerenders every route, there is no NgRx, no path alias, no shared folder and no component to reuse. Dependencies lack the two font packages. The admin project has its own, different structure and is not touched. Nothing needs to be reused or removed except the placeholder content of `app.html`.

## Scope
**In**
1. **Tokens.** `src/styles/_tokens.scss` from `docs/design-system/handoff/storefront/_tokens.scss` (generated from `tokens.json`; values not edited).
2. **Fonts, self-hosted.** `@fontsource/instrument-serif` (400 and 400 italic) and `@fontsource-variable/dm-sans` added to the `styles` array in `angular.json`; only the Instrument Serif 400 latin file and the DM Sans latin file are preloaded; `font-display: swap`. No CDN.
3. **Global styles.** `src/styles/_components.scss` from the handoff file and a small reset in `styles.scss` (body colour and font, `scroll-padding` so the sticky header and buy bar never hide focus). `class="rp-root"` on the app root. Only the classes this story needs are exercised; the rest of the handoff file comes with their pages.
4. **`rp-image` component** around `NgOptimizedImage` (see Behaviour).
5. **Shared UI** as attribute directives on native elements, in `shared/ui`: primary / ghost / light / small / block button, link-style CTA, icon button, and form field (label above, input, hint, error with `aria-describedby` and `aria-invalid`).
6. **Icons.** The thirteen design icons (search, heart, bag, menu, close, arrow, check, alert, truck, swap, chat, cash, pin) as one `rp-icon` component: 24px grid, 1.25px stroke, `currentColor`, decorative by default (`aria-hidden`).
7. **Image model** (`src`, `width`, `height`, `alt`, `blurDataUrl`) in `shared/models`, so later stories and the published-catalog read model share one shape.
8. **Foundations page** (development only, not a public route in production builds) showing tokens, type scale, buttons, fields and `rp-image` states, used for visual checks and the Playwright smoke test (see Q-DF-6).
9. `<html lang="en">`, correct viewport, `prefers-reduced-motion` rules from the handoff file, and the removal of the scaffold placeholder.

**Out (later stories)**
Header, mega-menu, search, footer, cart drawer (story 2); home (3); listing and product card (4); product page, gallery, lightbox, quick view, size selector (5); the reveal directive and view transitions (first use is story 2/3, see Q-DF-7); NgRx store for cart and wishlist (story 2); real catalogue data and real image delivery (the published-catalog and product-pages features); admin re-skin; brand logo and any brand asset (every image is a placeholder).

## Behaviour: `rp-image`
- Always renders inside a box with `aspect-ratio: var(--image-ratio)` (4 / 5) and `object-fit: cover`, so every product image has one crop and the box has its size before the file arrives.
- Wraps `NgOptimizedImage` with explicit `width`/`height` (or `fill` inside the box), `alt` required (empty `alt=""` only when the caller states the image is decorative), `ngSrcset` widths 400, 640, 828, 1080, 1440, 1920 and a `sizes` string supplied by the caller (grid card, gallery, hero presets provided as constants).
- `priority` input: sets eager loading and `fetchpriority="high"` for the one LCP image on a page; every other image is `loading="lazy"` and `decoding="async"`.
- Blur-up: shows the tiny `blurDataUrl` blurred (`rp-blur`) and sharpens when loaded (`is-loaded`). Under reduced motion it swaps without the transition.
- No `src`, or while loading: shows the `rp-ph` placeholder (tonal ground) or `rp-skel` skeleton in the same box; there is never a broken-image icon and never a layout shift.
- Optional second image: the component can render a second view that crossfades in on hover and fades in on first focus; the second file is requested on first `pointerenter` or focus, not on load (used by the product card in story 4; built here because it is the image component's behaviour).
- Server-rendered: the `<img>` with `src`, `srcset`, `width`, `height`, `alt` and the preload hint for a priority image are in the HTML the server returns.
- Image URL building goes through one image loader (injection token), so changing where derivatives live is one provider (Q-DF-4).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| DSF-1 | Given the built stylesheet, then every colour, radius, shadow, spacing and duration comes from a custom property defined in `_tokens.scss`; a unit/lint check fails on a raw hex colour or `box-shadow` outside the token file and the allow-list in Q-DF-8 | Implementation.md, CLAUDE.md |
| DSF-2 | Given any page, then there is exactly one theme: no `prefers-color-scheme: dark` rules and no theme switch exist | README (one theme only) |
| DSF-3 | Given the production build, then fonts are served from the app's own origin (no request to a font CDN), only the Instrument Serif 400 latin and DM Sans latin files are preloaded, and text is visible while fonts load (`font-display: swap`) | Implementation.md, NFR-01 |
| DSF-4 | Given text on each surface (`surface`, `surface-raised`, `surface-sunken`, dark scope), then body and secondary text meet 4.5:1, control borders 3:1, and clay text or fills use `accent-deep` (brand `accent` appears only on non-text marks) | README, WCAG 2.2 AA |
| DSF-5 | Given any button, link-CTA, icon button or input, then its hit area is at least 44 × 44 px, focus shows a 2px outline with 3px offset (cream inside `rp-dark`), and it is operable by keyboard | README, WCAG 2.5.8, 2.4.7 |
| DSF-6 | Given the primary button, then it uses `accent-deep` fill with white text, turns `ink` on hover, and a disabled button is transparent with `ink-muted` text and a stated reason is available through `aria-describedby` | Button README |
| DSF-7 | Given a field with an error, then the label sits above the input, the error text names what to do, is linked with `aria-describedby`, and the input has `aria-invalid="true"`; a placeholder is never the label | Field README, WCAG 3.3.1 |
| DSF-8 | Given `rp-image` with an image, then the rendered box is 4:5, the `<img>` has explicit `width`/`height`, `alt` text and a `srcset` of the six widths, and a Lighthouse run of the foundations page reports CLS 0 | SEO-07, NFR-01 |
| DSF-9 | Given `rp-image` with `priority`, then the server HTML contains the `<img>` with `fetchpriority="high"`, no `loading="lazy"`, and a `<link rel="preload" as="image">` with matching `imagesrcset` and `imagesizes`; given any other image, then it is lazy and async | SEO-07, NFR-01 |
| DSF-10 | Given `rp-image` without `src` or before load, then the placeholder or skeleton fills the same 4:5 box, the blur-up sharpens on load, and no broken-image icon or layout shift appears | Implementation.md |
| DSF-11 | Given an image with a second view, then it is not requested at load, is requested on first pointer-enter or focus, and then crossfades in over `--dur-slow`; touch devices never trigger the hover path | README (non-negotiable), Implementation.md |
| DSF-12 | Given `prefers-reduced-motion: reduce`, then blur-up, crossfade, zoom and shimmer are removed and elements simply appear | README, WCAG 2.3.3 |
| DSF-13 | Given the server-rendered foundations page, then it has one `<h1>`, landmarks (`<header>`, `<main>`, `<footer>`) and no heading skips, and an axe run reports no violations | CLAUDE.md semantic HTML, NFR-19 |
| DSF-14 | Given the app shell, then `<html lang="en">`, the viewport meta and `class="rp-root"` are present in the server HTML, and the scaffold placeholder is gone | CLAUDE.md |
| DSF-15 | Given viewports of 390, 768 and 1280 px, then the foundations page has no horizontal scroll, page margins are 16 / 32 / 56 px and the content is capped at 1600 px | tokens, NFR-17 |
| DSF-16 | Given this story's diff, then it changes only `src/web/projects/storefront`, `angular.json` (storefront `styles` entry), `package.json`/lockfile (the two font packages) and docs; the admin project and unrelated files are untouched and there are no formatting-only edits | owner instruction |

## Non-functional constraints
NFR-01 (CLS 0, preload only what is needed, fonts local), NFR-17 mobile-first, NFR-19 / WCAG 2.2 AA, SEO-01 (everything above is in the server HTML). Component styles stay under the existing 4 kB warning budget because global classes carry the look; only component-local layout lives in component SCSS.

## Out of scope
See Scope. Also: dark mode, any brand logo or asset, real photography, the Fit Finder, checkout.

## Conflicts raised (not resolved here)
These are differences between the design system and existing approved text. Nothing below was decided by the assistant; each needs your ruling, and the answer may change an approved spec.

1. **Design vs approved navigation spec.** `storefront/navigation/spec.md` (approved) lists Girls, Boys, Unisex, New In, Sale in the header and fixes the home design as "black and white base" (D-6). The design system says exactly five items: New in, Girls, Boys, Baby, Gifts, with ages inside the mega-menu, and a warm off-white and clay palette. Owner instruction says follow the design. Affects story 2 and 3, not this story, but D-6 and `NAV-*` need amending. See Q-DF-2.
2. **AVIF.** The design and SEO-07 say AVIF with WebP fallback; decision P-5 narrows Sprint 9 to WebP + JPEG. This story builds `srcset` and the loader format-agnostic. See Q-DF-4.
3. **Render mode.** The scaffold prerenders every route (`RenderMode.Prerender`); decision P-2 says per-request SSR (`RenderMode.Server`). This story switches the server routes to `Server` because catalogue pages cannot be prerendered. Noted for confirmation (Q-DF-5).
4. **Selector and folder conventions.** `docs/CONVENTIONS.md` says `app-` selectors, `features/<module>/<feature>/` folders and `@shared`. The design handoff uses `rp-` selectors and `layout/`, `shared/ui/`, `features/home`. See Q-DF-9.
5. **WCAG level.** CLAUDE.md says 2.1 AA; the design and your instruction say 2.2 AA. This spec uses 2.2 AA (a superset). CLAUDE.md should be updated when you approve.
6. **Design branch not on `main`.** The design system exists only on `origin/design/romp-design-system` (commit 9a564e0, headed "v8", `tokens.json` says version 7). The files this story copies must be on the branch S9 builds from. See Q-DF-1.

## Open questions
- [x] **Q-DF-1** (accepted as proposed) Branches and Jira. Proposed: merge `design/romp-design-system` to `main` first (it only adds `docs/design-system/`), create a Jira task under SCRUM-203 for this story, and branch `SCRUM-xxx-storefront-design-foundation` from `main`. Until then this spec lives on the current spec branch and nothing is committed. Which key do I use, and should the design PR be merged first?
- [x] **Q-DF-2** (accepted as proposed) Does the design system replace navigation D-6 and the header/menu list in `NAV-1..`? Proposed: yes; I amend `navigation/spec.md` (five items, Baby and Gifts as audience/collection entries, ages in the mega-menu) when story 2 is specified. Also confirm "Baby" and "Gifts" exist as catalogue data (genders/collections) or need new lookup rows in Catalog.
- [x] **Q-DF-3** (accepted as proposed) Raise the project accessibility target to WCAG 2.2 AA in CLAUDE.md and the constitution? Proposed: yes.
- [x] **Q-DF-4** (accepted as proposed) Image delivery. The repo has no CDN or `sharp` script; ADR 0018 plans backend derivatives (WebP + JPEG, hashed names, configured widths). Proposed: `rp-image` uses an image loader token whose default builds `?w=` style URLs against the derivative path from ADR 0018; mock data uses static placeholder files; AVIF is added by configuration later. OK, or do you want AVIF in S9?
- [x] **Q-DF-5** (accepted as proposed) Switch `app.routes.server.ts` from `Prerender` to `Server` now? Proposed: yes, in this story.
- [x] **Q-DF-6** (accepted as proposed) Foundations page: serve it at `/_foundations`, excluded from the production build and `robots`/sitemap, `noindex`. Or skip it and test components only through unit tests and Storybook-less specs? Proposed: keep it (dev/test only).
- [x] **Q-DF-7** (accepted as proposed) The reveal directive (scroll fade-in, `rp-js`) is listed under shared UI in Implementation.md but it is first needed by the home page. Proposed: build it in story 2/3 with its first user, not here. Move it into this story instead?
- [x] **Q-DF-8** (accepted as proposed) The handoff `_components.scss` contains a few raw values (for example `#35281F` in the dark scope, `rgba()` in placeholders, an inline SVG noise). "No raw colours outside tokens" conflicts with copying it verbatim. Proposed: lift those into tokens (`--surface-dark-raised`, `--ph-light`, ...) while keeping the rendered result identical, and list them in the plan. Alternatively allow-list them.
- [x] **Q-DF-9** (accepted as proposed) Conventions. Proposed: follow the design handoff for the storefront (`rp-` selectors for design-system pieces, folders `layout/`, `shared/ui/`, `shared/…`, `features/<page>/`), and record it in a short ADR plus a storefront section in `docs/CONVENTIONS.md`, because the admin conventions (`app-` prefix, `features/<module>/<feature>`) were written for the admin app. Features that talk to the API (listing, product, search) will still use the `features/storefront/<feature>` layout with NgRx. OK?
- [x] **Q-DF-10** (accepted as proposed) Package installs: `@fontsource/instrument-serif` and `@fontsource-variable/dm-sans` only (no UI library). Lucide for new icons is named in the design as a stand-in; add it in the story that first needs an icon beyond the thirteen in the design, or inline the thirteen SVGs now? Proposed: inline the thirteen as an `rp-icon` component here.

## Deviations from this spec so far (implementation in progress)
- **Font preload (DSF-3).** `@fontsource` CSS references hashed font files, so a static `<link rel="preload">` in `index.html` would point at a different URL and download the font twice. Fonts are local with `font-display: swap`; the preload is not added. To get it back, ship our own `@font-face` with stable file names (follow-up if Lighthouse shows font-driven LCP or CLS).
- **Placeholder art (DSF-1, Q-DF-8).** The tonal placeholder and macro-texture rules are isolated in `styles/_placeholders.scss` with raw colours allowed on purpose (deleted when photography exists). Every other raw value was lifted into tokens: `--line-strong-dark`, `--surface-dark-raised`, `--surface-dark-sunken`, `--ink-on-light` (added to `_tokens.scss` by hand; `tokens.json` should get the same four entries).
- **`rp-image` blur-up.** Uses `NgOptimizedImage`'s own `placeholder` (blur removed on load) instead of the `rp-blur` class. `priority` is static per image (Angular forbids changing it after init).
- **Foundations route** is registered only when `isDevMode()`, so production builds have no public page yet.
