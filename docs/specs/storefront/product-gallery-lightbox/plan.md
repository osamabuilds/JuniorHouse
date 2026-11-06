# Product gallery and lightbox: technical plan

- **Spec:** [spec.md](spec.md) (approved by the owner on 2026-11-05 with the recommendations for Q-GAL-1..3)
- **Module(s):** Storefront (web only). No API, schema or event change.

## Decisions taken from the open questions

- **Q-GAL-1:** the viewer uses the largest derivative already listed in the picture's `sources` (the pipeline makes several widths); no new processing. It is requested only when the viewer opens.
- **Q-GAL-2:** adopt the design's class names (`rp-gallery`, `rp-gimg`, `rp-gallery__count`, `rp-lightbox*`); the rules move from the component's inline styles into the shared stylesheet (ported from `docs/design-system/handoff/storefront/_components.scss`, tokens only).
- **Q-GAL-3:** no wrap-around; the previous and next buttons disable at the ends.

## Domain model / Application layer

None on the server. In the browser the existing product-page store already gives the gallery its `ProductImage[]` for the chosen colour, so GAL-11 (colour switch) needs no new state. New local UI state only:
- `ZoomState { scale: 1..4, x, y }`, a pure value with pure transition functions in `utils/zoom.util.ts` (`toggleAt(point)`, `pinch(startDistance, distance, centre)`, `pan(dx, dy)`, `clamp(bounds)`), so every gesture is unit-testable without a DOM.
- `viewerIndex: number | null` in the gallery container (null means closed).

## API endpoints / Events / Data

None.

## NFR / security design

- **NFR-01:** the viewer is a native `<dialog>` in the top layer, so opening it moves nothing. The first picture keeps `loading="eager"` and `fetchpriority="high"`; the rest are lazy (GAL-1). The viewer picture is created only on open (GAL-9) and uses the largest existing derivative.
- **SEO-01 / GAL-14:** every picture stays in the server HTML. Each is an `<img>` inside a real `<button type="button">`; without scripting the button does nothing and the strip or grid still shows every picture. The counter and the viewer are browser-only enhancements (`afterNextRender`).
- **NFR-19 / WCAG 2.5.7:** every gesture has a control: zoom in, zoom out and reset buttons, arrow keys to pan when zoomed, previous and next buttons, Left and Right keys, `+` and `-` keys. The counter uses `aria-live="polite"` and is updated at most once per settled scroll (no chatter while swiping).
- **Focus and scroll:** `showModal()` makes the rest of the page inert, which traps focus; Esc closes (native `cancel`); on `close` the container focuses the button that opened it; the document gets a `rp-scroll-lock` class while open.
- **Reduced motion:** under `prefers-reduced-motion` the zoom and slide transitions are removed.
- **Pointer handling:** Pointer Events with `touch-action: none` on the zoomed picture only, tracking at most two pointers; no `preventDefault` on passive scroll elsewhere.

## Frontend

All under `features/storefront/product-pages/` (the gallery belongs to the product page) and `shared/ui/lightbox/` for the reusable viewer, as `docs/design-system/Implementation.md` places it:
- `shared/ui/lightbox/lightbox.component.{ts,html}`: presentational. `input()` images, start index; `output()` closed, indexChanged. Owns the dialog, zoom and gesture wiring.
- `product-pages/components/product-gallery/`: converted to the design's markup (strip on phone, two-column grid from 64rem), pictures as buttons, live counter, opens the lightbox.
- `product-pages/utils/zoom.util.ts` and `gallery-counter.util.ts` (pure).
- Styles: the gallery and lightbox rules join `_components.scss` using tokens only.

## Risks & alternatives considered

- **A third-party lightbox library:** adds a dependency and its own focus handling; the design specifies native `<dialog>` and Pointer Events. Rejected.
- **CSS-only zoom:** cannot do pinch or pan limits. Rejected.
- No architectural choice that sets precedent, so no ADR.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | n/a, public product pictures only |
| Input validation | picture URLs come from the existing typed model (`/media/...` paths built by the API); the viewer never builds a URL from user input |
| Output encoding | alt text and URLs through Angular bindings; no `innerHTML` |
| Outbound calls | the larger picture is a same-origin `/media/` request |
| Resource limits | one viewer picture at a time (neighbours are not preloaded); at most two active pointers |
| Concurrency | n/a |
| Idempotency | n/a |
| Audit | n/a |
| Data protection | no personal or child data |
| Secrets and dangerous configuration | none |
| Error responses | a picture that fails to load shows the alt text and keeps the viewer usable |
| New dependencies | none |
| Coupling | `shared/ui/lightbox` imports nothing from a feature; the product page imports it through `shared/ui` |
| Abuse cases | (1) rapid double-tap: toggles once per tap and is clamped to 1x..4x; (2) pinching beyond bounds: clamped to 4x and to the picture edges; (3) holding Right at the last picture: stays on the last (no wrap, no error) |
