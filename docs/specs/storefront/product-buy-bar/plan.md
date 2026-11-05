# Product page buy bar and perks: technical plan

- **Spec:** [spec.md](spec.md) (approved by the owner on 2026-11-05 with the recommendations for Q-BUY-1..3)
- **Module(s):** Storefront (web only). No API, schema, event or endpoint change.

## Decisions taken from the open questions

- **Q-BUY-1:** perk wording is built from the settings the admin already manages (`selectFacts`): delivery days, exchange window and the WhatsApp link. No fixed copy with numbers.
- **Q-BUY-2:** the bar is used below 64rem (phones and tablets); from 64rem the sticky buying panel is used.
- **Q-BUY-3:** the bar shows price (and the struck old price) and the button only, no colour name.

## Domain model

None. The bar and perks are presentation. The price, availability and chosen size come from the product page's existing state, which the server already calculated (SEC-09).

## Application layer (web state)

No new store slice. The container already owns `canAdd`, `addHint`, `addToBag()` and the price. New local UI state only:
- `mainAddVisible` signal: whether the main Add to bag button is in the viewport, set by an `IntersectionObserver` created in `afterNextRender` (browser only, so server rendering is unchanged and SEO-01 holds).
- `barVisible = computed(() => isAvailable && !mainAddVisible && viewportBelow64rem)`.

| Action | Behaviour | Covers |
|---|---|---|
| bar Add to bag pressed, a size is chosen and in stock | calls the container's existing `addToBag()` (same path, same drawer, same errors) | BUY-4 |
| bar Add to bag pressed, no size chosen | scrolls the size selector into view, moves focus to it, and shows the existing "Choose a size" hint; nothing is added | BUY-3 |
| product or size sold out | bar is not shown for a sold-out product (the main panel shows the sold-out state and Notify me); for a sold-out size the bar button is disabled with the same hint as the main button | BUY-5 |

## API endpoints

None. Delivery, exchange and WhatsApp facts already arrive with the settings the storefront loads at start (and are in the ADR 0028 transfer for the shell).

## Events / Data

None. No schema change.

## NFR / security design

- **NFR-01:** the bar is `position: fixed` and overlays; the page does not move when it appears (browser test measures CLS). It is created only in the browser, so the server HTML is unchanged.
- **NFR-17 / NFR-19:** 44px button, visible focus, keyboard operable, respects `prefers-reduced-motion` (no slide animation under reduced motion, a short fade otherwise). The bar is not announced as a landmark; it is a `div` region inside `main` with the button named "Add to bag" and a visible price text, so a screen reader hears no duplicate controls: while the bar is shown the main button is off screen but still present, so the bar button has `aria-describedby` pointing at the price and the bar is `aria-hidden` while hidden (it is removed from the DOM, not just hidden).
- **WCAG 2.4.11:** while the bar shows, `scroll-padding-bottom` on the document root equals the bar height (measured with `ResizeObserver`, cleared when the bar goes away), and the page gets the same amount of bottom padding at the very end so the footer's last line is not covered (BUY-7).
- **SEC-09:** the price shown is the page's server-calculated price; the bar never computes or edits it.
- **SEO:** no new indexable content; perks are plain text from settings in the server HTML.

## Frontend

All in `features/storefront/product-pages/` following `docs/CONVENTIONS.md`:
- `components/buy-bar/buy-bar.component.{ts,html}`: presentational, `input()` price, struck price, `canAdd`, `busy`, `hint`; `output()` `add`. No store.
- `components/product-perks/product-perks.component.{ts,html}`: presentational, `input()` of the facts view; shows only the perks whose fact exists (BUY-11).
- `utils/perks.util.ts`: pure function turning `FactsView` into at most three perk lines: `Delivery in {min}-{max} days` (min of `minDays`, max of `maxDays` across enabled rules; hidden if none), `Exchange within {n} days` (hidden if no window), `Questions? Message us on WhatsApp` linking to the safe `whatsapp.href` (hidden if no number).
- `directives/in-view.directive.ts` (or a small util) wrapping `IntersectionObserver` with a server-safe guard.
- `product-page.container.{ts,html,scss}`: wires the main button's wrapper to the observer, renders the bar and perks, handles the "no size" case.
- Styles: global `rp-` classes where the design system has them; the bar's component-local layout in the container SCSS, using tokens only (surface, line, `--tap`, `--space-*`, the `action` button style).

## Risks & alternatives considered

- **Always show the bar (no observer):** simpler, but two Add to bag buttons are visible at once at the top of the page. Rejected (BUY-2).
- **CSS `position: sticky` bar inside the panel:** sticks only inside its container, so it fails at the bottom of a long page. Rejected.
- No architectural choice that sets precedent, so no ADR.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | n/a, no endpoint; same add-to-bag action as the main button, with its existing server checks |
| Input validation | n/a, no new input; the WhatsApp href comes from the existing safe link builder (digits only) |
| Output encoding | all text through Angular bindings; no `innerHTML` |
| Outbound calls | none new |
| Resource limits | one observer and one resize observer, both disconnected when the component is destroyed |
| Concurrency | the add button is disabled while the bag is busy (existing `bagBusy`), so a double tap sends one request |
| Idempotency | existing add-to-bag behaviour |
| Audit | n/a |
| Data protection | no personal data; no child data |
| Secrets and dangerous configuration | none |
| Error responses | add-to-bag errors show in the existing message area; the bar scrolls it into view if the main area is off screen |
| New dependencies | none |
| Coupling | reads the settings through the public `site-settings` barrel only (`selectFacts`); no other feature internals |
| Abuse cases | (1) tapping the bar button repeatedly: disabled while busy; (2) a settings value with unsafe markup in a fact: rendered as text, WhatsApp built from digits only; (3) a tampered price in the DOM: the add call sends the variant, never the price, and the server prices the line |

## Open items carried to the tasks

The tasks list the exact tests; the e2e checks run on a phone viewport (390) and a tablet (768) because the bar appears below 64rem.
