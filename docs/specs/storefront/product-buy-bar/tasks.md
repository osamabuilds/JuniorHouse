# Product page buy bar and perks: tasks

Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md). Each step is test-first; the test named is written and seen failing before the code.

## Perks (BUY-10, BUY-11)

- [ ] **B1** `perks.util.ts`: facts to at most three lines. *Tests (`perks.util.spec.ts`): delivery range from the enabled rules only; exchange line from the window; WhatsApp line only when the number is valid; each fact missing means that perk missing; a disabled delivery rule is ignored; no text containing digits typed in code (values come from the input).*
- [ ] **B2** `ProductPerksComponent` (presentational, list of lines, WhatsApp as a real link). *Tests: renders a `<ul>` with only the lines given; the WhatsApp line is an `<a href>`; nothing renders when there are no lines (BUY-11).*
- [ ] **B3** Place the perks under the buy buttons in the product page container, fed by `selectFacts`. *Test (container spec): perks follow the settings; changing the settings changes the text (BUY-10).*

## Buy bar (BUY-1..BUY-9)

- [ ] **B4** In-view observer util, server-safe (no `IntersectionObserver` on the server; fallback "visible" so the bar never renders in SSR). *Tests: with a fake observer, entering and leaving the viewport flips the signal; it disconnects on destroy; on the server platform it never creates an observer.*
- [ ] **B5** `BuyBarComponent`: price, struck old price when discounted, Add to bag button (44px), `busy` and `canAdd` states, `add` output. *Tests: shows the price text and the struck price only when discounted (BUY-1, BUY-6); button disabled while busy; emits `add` on click; no Store injected.*
- [ ] **B6** Container wiring: `barVisible` = available and main button out of view and below 64rem; bar removed from the DOM otherwise. *Tests (container spec with a fake observer and viewport): hidden while the main button is visible (BUY-2); shown when it scrolls out (BUY-1); never rendered for a sold-out product; not rendered at 64rem or wider (BUY-9).*
- [ ] **B7** Bar button behaviour: with a size chosen, calls `addToBag()` once and the drawer opens (BUY-4); with no size, scrolls and focuses the size selector and shows the existing hint without calling add (BUY-3); a sold-out size gives the disabled state (BUY-5). *Tests: container spec for each, asserting the cart action dispatched exactly once or not at all.*
- [ ] **B8** `scroll-padding-bottom` equals the bar height while it shows and is cleared when it goes; bottom padding at the end of the page equals the bar height (BUY-7). *Tests: with a fake `ResizeObserver`, the CSS variable follows the height and is removed on hide and on destroy.*
- [ ] **B9** Styles with tokens only, fixed position, safe-area inset at the bottom, reduced-motion respected. *Proves: stylelint-free grep for raw colours (existing audit), and e2e B11.*

## End to end and close-out

- [ ] **B10** Browser tests in `e2e/journeys/storefront-buy-bar.e2e.spec.ts` against the rebuilt container: at 390 and 768 px the bar appears after scrolling past the main button and disappears when scrolled back; no size, press the bar button, the size selector is focused and nothing is added (BUY-3); choose a size, press the bar button, the bag drawer opens with one line (BUY-4); at 1280 px there is no bar (BUY-9).
- [ ] **B11** Same file: CLS at or under 0.1 while scrolling (BUY-8), no horizontal scroll, axe clean, the bar button is at least 44px, a focused field near the bottom is not hidden behind the bar (BUY-7).
- [ ] **B12** Perks check in the e2e: the three perks appear for the demo settings and the WhatsApp link opens `wa.me` (BUY-10).
- [ ] **B13** Rebuild the docker storefront, check by eye on a phone-width window, then update `spec.md` ("As built"), the storefront README row to Implemented, the design audit (D4 fixed) and the memory hand-off.
- [ ] **B14** PR description links this folder and lists BUY-1..11, FEAT-PDP-01..04, NFR-01, NFR-17, NFR-19, SEC-09.
