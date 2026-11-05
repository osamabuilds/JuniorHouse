# Product gallery and lightbox: tasks

Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md). Each step is test-first; the test named is written and seen failing before the code.

## Pure logic

- [x] **G1** `zoom.util.ts`: toggle at a point, pinch, pan, clamp. *Tests (`zoom.util.spec.ts`): toggling from 1x gives 2x with the point kept under the pointer, toggling again returns to 1x (GAL-4); pinch scales between 1 and 4 and never beyond (GAL-5); pan is clamped to the picture edges and ignored at 1x; keyboard pan steps equal button steps (GAL-7).*
- [x] **G2** `gallery-counter.util.ts`: index of the picture most in view from the strip's scroll position and item width. *Tests: exact positions, half-way positions round to the nearer picture, one picture gives no counter (GAL-2).*

## Gallery (GAL-1, 2, 11, 12, 14)

- [x] **G3** Rebuild `ProductGalleryComponent` to the design's markup: `rp-gallery`, each picture an `<img>` in `<button type="button" class="rp-gimg">`, eager and high priority for the first only, alt and size kept. *Tests (component spec): server-rendered markup contains every picture as `img` in a `button` (GAL-1, GAL-14); first eager, others lazy; no pictures shows the existing message (GAL-12); a colour change swaps the pictures (GAL-11).*
- [x] **G4** Live counter `n / N` with `aria-live="polite"`, browser only, hidden for one picture. *Tests: the counter text follows a simulated scroll, is announced once per settled position, and is absent for one picture (GAL-2).*
- [x] **G5** Port the gallery rules into `_components.scss` (strip on phone, two-column grid from 64rem, `zoom-in` cursor), tokens only. *Proves: the existing raw-colour grep audit and e2e GAL-13.*

## Lightbox (GAL-3 to GAL-10)

- [x] **L1** `LightboxComponent` shell: native dialog, top bar `n / N` and close, picture fitted at 4:5, hint line, previous and next buttons disabled at the ends. *Tests: opens modally at the given index, shows the counter and the hint (GAL-3); previous is disabled on the first and next on the last; Left and Right keys move (GAL-6).*
- [x] **L2** Larger source on open only. *Tests: no viewer image exists before opening; on open exactly one request-bearing `<img>` is created with the largest listed width (GAL-9, Q-GAL-1).*
- [x] **L3** Zoom and pan wiring: click, double-tap and `+` toggle 2x; pinch with two pointers; drag pans when zoomed; swipe moves only when not zoomed; zoom in, zoom out and reset buttons. *Tests (pointer events simulated): each gesture changes the state through the util; swipe does nothing while zoomed (GAL-6); each gesture has a button or key that gives the same state (GAL-7).*
- [x] **L4** Focus and scroll: Esc closes, focus returns to the opening button, `rp-scroll-lock` is set while open and cleared on close and on destroy. *Tests (GAL-8).*
- [x] **L5** Reduced motion removes transitions. *Test: with the media query matched, no transition class is applied (GAL-10).*
- [x] **L6** Lightbox rules in `_components.scss`, tokens only (`--lightbox` ground). *Proves: raw-colour audit.*

## End to end and close-out

- [x] **E1** Browser tests in `e2e/journeys/storefront-gallery.e2e.spec.ts` (rebuilt container): at 390 px the counter updates when the strip scrolls and tapping opens the viewer; at 1280 px a grid picture opens it; Esc closes and focus returns; buttons and keys zoom and move; no wheel or pinch needed for any action (GAL-3..8).
- [x] **E2** Same file: no layout shift when the viewer opens, no horizontal scroll at 390, 768 and 1280 px, axe clean, controls at least 44px (GAL-13); without scripting every picture is in the HTML (GAL-14).
- [x] **E3** Rebuild the docker storefront, check the product page by eye on phone and desktop width, then update `spec.md` ("As built"), the storefront README row, the design audit (G3 fixed) and the memory hand-off.
- [x] **E4** PR description links this folder and lists GAL-1..14, FR-07, SEO-07, NFR-01, NFR-17, NFR-19.
