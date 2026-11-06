# Listing filter bar, drawer and swatches: tasks

Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md). Each step is test-first; the test named is written and seen failing before the code.

## Pieces

- [ ] **F1** `ColourSwatchComponent`: a native checkbox styled as a round swatch, name as accessible text, selected ring, initial when no hex. *Tests: the label has the colour name, `checked` follows the input, a selected swatch has a ring class and `aria-checked` semantics from the checkbox, colour is not the only signal (FLT-4).*
- [ ] **F2** `FilterBarComponent` desktop: sticky container with `<details>` chips (Age, Size, Colour, Price), count, native sort select, all fields inside one GET `<form>`; selected options show as ink-filled chips with a remove link. *Tests: the markup posts as GET to the current path, every option is in the server HTML, a selected option shows its remove control, the count and sort exist (FLT-1, FLT-2, FLT-6).*
- [ ] **F3** `FilterBarComponent` phone: one "Filter and sort" `<details>` holding the same form with Age chips, swatches and Sort, and a sticky "Show N pieces" submit. *Tests: present in the same markup, button label uses the count input, works as a plain form (FLT-3, FLT-6).*
- [ ] **F4** `sheet.directive.ts`: Esc closes, focus moves in and is kept inside, page scroll locked while open, focus returns to the summary; popovers close on Esc and outside click and return focus to their chip. *Tests (jsdom keyboard and focus events): Esc closes and focuses the summary; Tab from the last control returns to the first; the lock class is set and cleared, including on destroy (FLT-2, FLT-10).*
- [ ] **F5** `draft-count.util.ts` and wiring: draft filter to a debounced, cancelled `pageSize=1` call through the existing API services; the button shows the total. *Tests: one call after the debounce, a newer change cancels the older call, a failed call keeps the last count, the call uses the same canonical query rules (FLT-13).*
- [ ] **F6** Reduce `filter-panel` to the shared field set used inside the bar (no duplicated logic) and keep the active-filter chips and "Clear all". *Tests: the existing `filter-panel` and `active-filters` specs still pass; chips and Clear all unchanged (FLT-7).*

## Pages

- [ ] **P1** Use the bar in `listing-page.container` and `search-page.container`; "Showing 1-24 of N"; pagination and canonical rules unchanged. *Tests (container specs): the bar renders with the server's options; applying navigates with the canonical query; the canonical, noindex and pagination tests (SEO-06, SRC-11, LST rules) still pass (FLT-8, FLT-12).*
- [ ] **P2** Loading behaviour: keep the previous cards, dimmed and `aria-busy`; skeleton cards only when no previous page exists. *Tests: with a previous page the cards stay and the grid is `aria-busy`; with none, skeletons of the card ratio show (FLT-5).*
- [ ] **P3** Empty state with applied filters as removable chips and links to New in and Gifts stays. *Test: the existing empty-state tests pass (FLT-11).*
- [ ] **P4** Styles ported to `_components.scss` (`rp-filterbar`, `rp-chip`, `rp-drawer`, `rp-swatch`), tokens only; `scroll-padding-top` equals header plus bar. *Proves: raw-colour audit and e2e FLT-9.*

## End to end and close-out

- [ ] **E1** Browser tests in `e2e/journeys/storefront-filter-bar.e2e.spec.ts` (rebuilt container): desktop chips open popovers, Esc closes and returns focus, selecting an option updates the URL and the grid without a full reload; phone sheet opens at 88% height, shows "Show N pieces", Apply closes it (FLT-1, 2, 3, 5).
- [ ] **E2** Same file: with JavaScript disabled the filters still work as a GET form and the server returns the filtered page (FLT-6); sticky bar covers no focused element, no horizontal scroll at 390, 768 and 1280 px, axe clean, CLS at most 0.1 (FLT-9, FLT-10).
- [ ] **E3** The same bar works on `/search?q=` (FLT-12).
- [ ] **E4** Rebuild the docker storefront, check by eye, then update `spec.md` ("As built", including the FLT-5 resolution), the storefront README row, the design audit (G1 fixed except the banner) and the memory hand-off.
- [ ] **E5** PR description links this folder and lists FLT-1..13, FR-02, FR-03, FR-05, SEO-06, NFR-01, NFR-17, NFR-19.
