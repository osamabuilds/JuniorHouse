# Product gallery and lightbox

- **Status:** Implemented (2026-11-05)
- **Jira:** none yet
- **BRD sections:** §5 product page (FEAT-PDP-01..04); design source `docs/design-system/components/Gallery/README.md` and `Lightbox/README.md`
- **Requirement IDs:** FR-07 (gallery), SEO-07, NFR-01, NFR-17, NFR-19 (WCAG 2.5.7)

## Problem & intent

The design makes the product pictures the page: a counter on the phone swipe strip, pictures that open in a full-screen viewer with real zoom, and a keyboard and button equivalent for every gesture. The live gallery already has the right layout (a snap-scrolling strip on a phone, a two-column grid from 64rem, first picture eager with high priority, alt text, explicit sizes) but has no viewer, a static "N images" label instead of a live counter, and its pictures are not controls (design audit G3).

## Requirements (from the design system)

> **Gallery:** "Desktop (64rem+): a two-column grid of 4:5 images, 8px apart, all visible as the page scrolls ... Each image is a `<button class="rp-gimg">` with `cursor: zoom-in` that opens the `Lightbox` at that image. Phone: one full-bleed 4:5 image at a time in a `scroll-snap` swipe strip, edge to edge, with a small `1 / 6` counter (`aria-live="polite"`). Tapping opens the `Lightbox`. The first image is the LCP element on this page (`priority`); the rest lazy-load. Alt text describes the view."
>
> **Lightbox:** "Native `<dialog>` with `showModal()`; ground `lightbox`. Top bar: `1 / 6` and a close button. The image is shown at its full 4:5 ratio, fitted to the viewport. Bottom hint line ('Pinch or double-tap to zoom'). Zoom: click, double-tap or the `+` key toggles 2x about the pointer; pinch (two pointers) zooms freely up to 4x with Pointer Events (`touch-action: none`); drag pans when zoomed. Use a larger source only when the lightbox opens. Moving: arrow buttons on desktop, Left/Right keys, swipe on touch (swipe is disabled while zoomed). Esc closes and returns focus to the thumbnail; focus is trapped; background scroll locked. Every gesture has a button or key equivalent (WCAG 2.5.7)."

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| GAL-1 | Given a product with several pictures, then the server HTML contains every picture as an `<img>` inside a `<button>` that opens the viewer, and the first picture is eager with `fetchpriority="high"` while the others are lazy; every picture keeps its alt text and explicit width and height | Gallery, PDP-10 |
| GAL-2 | Given a phone, when the visitor swipes the strip, then a counter such as `2 / 6` updates to the picture in view and is announced politely to screen readers; with one picture there is no counter | Gallery |
| GAL-3 | Given a picture, when it is pressed (or Enter or Space on its button), then a native dialog opens modally at that picture, showing `n / N`, a close button, the picture fitted to the viewport at 4:5, and the hint line | Lightbox |
| GAL-4 | Given the viewer is open, when the visitor clicks, double-taps or presses `+`, then it zooms to 2x about the pointer, and the same action zooms back out | Lightbox |
| GAL-5 | Given two pointers on touch, when they pinch, then the picture scales between 1x and 4x; and when zoomed, dragging pans it without leaving the picture's bounds | Lightbox |
| GAL-6 | Given the viewer, then previous and next buttons (desktop), the Left and Right keys, and a horizontal swipe (touch) move between pictures, wrapping is not used, and swipe does nothing while zoomed | Lightbox |
| GAL-7 | Given every gesture (zoom, pan, move), then a visible button or a key does the same thing: zoom in and zoom out buttons, arrow keys to pan when zoomed | WCAG 2.5.7 |
| GAL-8 | Given the viewer is open, then focus is trapped inside it, background scroll is locked, Esc closes it, and focus returns to the picture button that opened it | Lightbox |
| GAL-9 | Given the page loads, then no larger picture is requested; the larger source is requested only when the viewer opens (and only for the pictures shown) | Lightbox, NFR-01 |
| GAL-10 | Given `prefers-reduced-motion`, then zoom and slide changes happen without animation | NFR-19 |
| GAL-11 | Given the visitor chooses another colour, then the gallery and the viewer show that colour's pictures (the existing PDP-2 rule) | PDP-2 |
| GAL-12 | Given a product with no pictures, then the existing "photographs are on their way" message shows and nothing opens | existing |
| GAL-13 | Given 390, 768 and 1280 px, then there is no horizontal page scroll, no layout shift when the viewer opens or closes, controls are at least 44px, and axe reports no violations | NFR-17, NFR-19, NFR-01 |
| GAL-14 | Given scripting is unavailable, then every picture is still visible in the server HTML and the strip still scrolls (the viewer is an enhancement) | SEO-01 (new) |

## Non-functional constraints

NFR-01 (CLS at most 0.1; the viewer is a top-layer dialog, so the page does not move), SEO-01 and SEO-07 (alt text, server HTML), NFR-19 and WCAG 2.5.7 (single-pointer alternatives), NFR-17 (mobile first).

## Out of scope

Thumbnails strip (the design has none), video, 360 views, sharing a picture, product-recommendation rails (separate spec).

## Open questions

- [x] **Q-GAL-1** Which source does the viewer use? The image pipeline already makes derivatives at several widths. Recommendation: the largest existing derivative of that picture (no new processing), requested when the viewer opens. **Resolved: recommendation accepted.**
- [x] **Q-GAL-2** Adopt the design's class names (`rp-gallery`, `rp-gimg`, counter) in place of the component's own (`gallery`, `gallery__item`)? Recommendation: yes, so styling comes from the shared stylesheet and not from component CSS. **Resolved: recommendation accepted.**
- [x] **Q-GAL-3** Wrap-around at the first and last picture: the design is silent. Recommendation: no wrap (buttons disable at the ends). **Resolved: recommendation accepted.**

## As built

`shared/ui/lightbox` (native dialog through `RpDialog`, Pointer Events) and `shared/utils/zoom.util` (pure zoom, pan and counter maths); `product-gallery` rebuilt to the design markup (one list: strip on a phone, two columns from 64rem; pictures are buttons; live `n / N` counter). The viewer asks for the largest listed derivative only when it opens. Two checks skip until a product has more than one picture (counter on scroll, moving between pictures); they are unit-tested. The old design CSS for a two-tree gallery was removed from `_components.scss`. Breadcrumb links got a 24px minimum target (axe target-size) because the product page axe test failed on them. `+` toggles 2x (GAL-4); `-` and the buttons step by 0.5 (GAL-7).
