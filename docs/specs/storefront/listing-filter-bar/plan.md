# Listing filter bar, drawer and swatches: technical plan

- **Spec:** [spec.md](spec.md) (approved by the owner on 2026-11-05 with the recommendations for Q-FLT-1..3; pagination kept by the owner's earlier decision)
- **Module(s):** Storefront (web only). No API, schema or event change: the existing listing endpoint already takes `size`, `colour`, `age`, `price`, `sort`, `page` and `pageSize`.

## Decisions taken from the open questions

- **Q-FLT-1:** keep the current page size and add "Showing 1-24 of N" under the grid.
- **Q-FLT-2:** no editorial banner now; it gets its own spec when the business supplies content.
- **Q-FLT-3:** the drawer's live count uses the existing listing endpoint with `pageSize=1` and reads the total; no new endpoint.

## How the bar works with and without scripting (FLT-6)

The filter form (all controls, a GET to the same path) is the base, as today. The new bar wraps it in native disclosure elements so it works with no script and no layout change at load:
- **Desktop:** each chip is a `<details class="rp-chip-pop">` whose panel holds that filter's options as form controls inside the one `<form>`; the sort `<select>` and the count sit at the right. Without script, opening a chip and pressing Apply submits the GET.
- **Phone:** one `<details class="rp-filterbar__sheet">` "Filter and sort". With script its open state is styled as a bottom sheet (fixed, full width, 88% high, scrim behind) and a small directive adds what CSS cannot: Esc closes, focus moves in and is kept inside while open, the page behind is locked, and focus returns to the summary on close. Without script it simply expands in place.
- Server HTML contains the whole form, so crawlers and no-script visitors get the same filters. Nothing is added after hydration that moves content (no CLS).

## Application layer (web state)

No new store slice. `ListingsActions.load` and `SearchResultsActions.load` already take the parsed filter, retain the previous page while loading, and ignore stale keys. New local state only:
- `draft: ListingFilter` in the bar (what the visitor has ticked in the drawer, not yet applied).
- `draftCount` signal: the total for `draft`, fetched with `pageSize=1` through the existing `ListingsApi`/`SearchApi` (debounced 250 ms, `switchMap`-cancelled), used by the "Show N pieces" button (FLT-13).

| Action | Behaviour | Covers |
|---|---|---|
| Apply (desktop popover or drawer button) | navigates to the same path with the canonical query (existing `toCanonicalQuery`), which the page effect turns into a load | FLT-5 |
| Loading with a previous page | previous cards stay, dimmed with `aria-busy`; skeleton cards only when there is no previous page (first client navigation to a fresh listing) | FLT-5 (see note) |

Note on FLT-5: the spec asks for skeletons and for keeping the previous cards. The two conflict, so the plan resolves it as above: keep and dim, skeleton only when nothing is on screen. Called out for the owner in the PR.

## API endpoints / Events / Data

None new. The count call reuses `GET /api/storefront/listings...` and `GET /api/storefront/search` with `pageSize=1`; both already clamp page size and validate every filter value (LST-11, LST-16).

## NFR / security design

- **NFR-01:** the sticky bar has a fixed height (a CSS variable), so the header plus bar height is known; skeleton cards use the card's 4:5 ratio and text lines of fixed height.
- **WCAG 2.4.11:** `scroll-padding-top` on the document equals the header plus the bar height so a focused element is never hidden behind the sticky bar.
- **SEO-06:** unchanged; the canonical for filter, sort and page variants is produced by the existing code. The bar adds no new URLs: it submits to the same paths with the same query names.
- **Swatches (FLT-4):** each colour is a native checkbox styled as a round swatch, with a visible text name (visually hidden) in the label and a ring for the selected state, so colour is never the only signal. Swatch colours come from the lookup colour data the page already receives; where a colour has no hex value the swatch shows its initial.
- **Input handling:** the draft is built only from the option lists the server returned; values still go through `parseFilter` before navigation.

## Frontend

`features/storefront/listings/` (shared by `search`, which already imports from the listings barrel):
- `components/filter-bar/` (presentational: options, selected, count, outputs `apply`, `draftChange`); replaces the visible use of `filter-panel` but keeps its form fields as the content of the popovers and sheet (the `filter-panel` component is reduced to the fields, so the logic is not duplicated).
- `components/colour-swatch/` (presentational checkbox swatch).
- `directives/sheet.directive.ts`: Esc, focus trap, scroll lock, focus return (browser only).
- `utils/draft-count.util.ts`: debounced count stream helper.
- `containers/listing-page` and `search/containers/search-page`: render the bar, wire `apply`, `draftCount`, "Showing x-y of N".
- Styles: `rp-filterbar`, `rp-chip`, `rp-drawer`, `rp-swatch` ported from the design's `_components.scss` into `_components.scss`, tokens only.

## Risks & alternatives considered

- **Native `<dialog>` for the phone sheet:** gives free focus trapping but is not visible without script, which would break FLT-6 (no-script filters). Rejected in favour of `<details>` plus a small enhancement.
- **Client-only filter state:** would break shareable URLs and the server-rendered result. Rejected.
- **A new count endpoint:** needless; page size 1 does it.
- No architectural choice that sets precedent, so no ADR.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | n/a, public catalogue reads |
| Input validation | all values pass the existing `parseFilter` (length, charset, list caps) before navigation and again in the API |
| Output encoding | option labels and counts via Angular bindings |
| Outbound calls | same-origin API only |
| Resource limits | count calls debounced and cancelled; page size 1; existing caps on list lengths (20 values) and price |
| Concurrency | `switchMap` cancels stale count calls; stale list results are ignored by key (existing) |
| Idempotency | GET only |
| Audit | n/a |
| Data protection | no personal data |
| Secrets and dangerous configuration | none |
| Error responses | a failed count keeps the last count and the button still applies the filter |
| New dependencies | none |
| Coupling | `search` keeps importing the bar through the listings barrel; nothing new crosses features |
| Abuse cases | (1) a forged query with 10,000 colours: capped at 20 by `parseFilter`; (2) hammering options in the drawer: debounced and cancelled, one request in flight; (3) markup in a colour name: rendered as text |
