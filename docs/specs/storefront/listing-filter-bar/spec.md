# Listing filter bar, drawer and swatches

- **Status:** Implemented (2026-11-05), with the deviations below
- **Jira:** none yet
- **BRD sections:** FR-02, FR-03, FR-05 (filters and sorting); design source `docs/design-system/components/FilterBar/README.md` and `ListingPage/README.md`
- **Requirement IDs:** FR-02, FR-03, FR-05, SEO-06, NFR-01, NFR-17, NFR-19

## Problem & intent

The design's listing has a sticky filter bar under the header, filter chips with popovers on desktop, a "Filter and sort" bottom sheet on phones, round colour swatches, and a live "Show N pieces" button. The live listing and search pages use a plain form-style filter panel and numbered pagination (design audit G1). The panel works without scripting, which the approved `listings` spec requires, so this spec keeps that behaviour and adds the design's bar and drawer on top.

**Owner decision (2026-11-05):** keep server-rendered pagination instead of the design's "Show more" button, because it gives each page a crawlable address (SEO-06). Everything else in the design is taken as written.

## Requirements (from the design system)

> **FilterBar:** "The listing's filter bar is sticky directly under the header, with 1px `line` borders top and bottom. Desktop: `rp-chip` filters (Age, Size, Colour, Price) open small popovers; the selected state is an ink fill with a remove icon. At right: result count and a native sort select. Phone: one 'Filter and sort' chip and the count. It opens a bottom sheet (`rp-drawer`, full width, 88% high) with Age chips, Colour swatches and Sort, and a sticky 'Show 48 pieces' primary button. Active filters repeat below as removable chips with a 'Clear all' link. Changing a filter updates the URL query and the grid without a full reload, and shows skeleton cards while loading."
>
> **ListingPage:** "Grid: 2 / 3 / 4 columns of 4:5 cards with the tight 8/16px gap; an editorial banner (`rp-banner`, full row) after the fourth card. 12 at a time, then a 'Show more' ghost button with 'Showing 12 of 48'. Zero results: `display-sm` message, applied filters as removable chips, links to New in and Gifts."

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| FLT-1 | Given a listing or search page on desktop, then a bar sticks directly under the header with a 1px line above and below, holding filter chips (Age, Size, Colour, Price), the result count and a native sort select | FilterBar |
| FLT-2 | Given a desktop chip, when it is pressed, then a small popover opens with that filter's options; Esc or an outside click closes it and focus returns to the chip; a selected option shows as an ink-filled chip with a remove icon | FilterBar |
| FLT-3 | Given a phone, then the bar shows one "Filter and sort" chip and the count; pressing it opens a native dialog bottom sheet, 88% high, with Age chips, colour swatches and Sort, and a sticky primary button "Show N pieces" that closes it | FilterBar |
| FLT-4 | Given colours, then each is a round swatch with its name as the accessible name and a visible selected ring, never colour alone | FilterBar, NFR-19 |
| FLT-5 | Given a filter or sort change with scripting, then the URL query changes and the grid updates without a full page load, with skeleton cards while the new page loads and the previous cards kept in place until then | FilterBar, existing LST rule |
| FLT-6 | Given scripting is unavailable, then the same controls work as a GET form to the same address, and the page the server returns has the same filters applied (progressive enhancement) | LST (existing), SEO-01 |
| FLT-7 | Given active filters, then removable chips repeat below the bar with a "Clear all" link (existing behaviour kept) | FilterBar |
| FLT-8 | Given the result list, then server-rendered numbered pagination stays; filter, sort and page variants keep their canonical rules (SEO-06) | owner decision, SEO-06 |
| FLT-9 | Given the bar is sticky, then it does not cover focused elements below it (scroll padding equals the header plus the bar), causes no layout shift, and has no horizontal scroll at 390, 768 or 1280 px | NFR-01, WCAG 2.4.11 |
| FLT-10 | Given the drawer or a popover, then focus moves into it, Tab stays inside the drawer, Esc closes, and the page behind does not scroll under the drawer | NFR-19 |
| FLT-11 | Given zero results, then the existing empty state shows with the applied filters as removable chips and links to New in and Gifts | ListingPage, existing |
| FLT-12 | Given the same bar on `/search`, then it behaves identically (the search page reuses the component) | ListingPage |
| FLT-13 | Given the count button "Show N pieces", then N is the server's total for the current selection, updated as the visitor changes options in the drawer | FilterBar, SEC-09 spirit |

## Non-functional constraints

NFR-01 (no layout shift; skeletons are the same height as cards), SEO-06 (canonical for filter, sort and page variants), NFR-17, NFR-19, SEC: all filter values are validated by the existing parser and the API; the count call is a GET with the same input rules and the existing page-size cap.

## Out of scope

"Show more" paging (declined), editorial banner after the fourth card (see Q-FLT-2), saved filters, filter by price slider.

## Open questions

- [x] **Q-FLT-1** The design says "12 at a time". The live page size is 24. Recommendation: keep the current page size (fewer pages, same SEO), and show "Showing 1-24 of N" under the grid. **Resolved: recommendation accepted.**
- [x] **Q-FLT-2** The banner after the fourth card needs an image and a line of text per audience. Recommendation: not now; add it when the business supplies the content, as a settings-driven block in its own spec. **Resolved: recommendation accepted.**
- [x] **Q-FLT-3** The count in "Show N pieces" while the drawer is open needs one small extra API call per change. Recommendation: use the existing listing endpoint with page size 1 and read the total, so no new endpoint is needed. **Resolved: recommendation accepted.**

## As built

`listings/components/filter-bar` (two server-rendered GET forms: desktop chips with `<details>` popovers from 64rem, one "Filter and sort" disclosure that becomes an 88% bottom sheet below it), `filter-fields` (the shared controls and the colour swatches), `directives/sheet.directive.ts` (Escape, scrim click, focus trap and return, scroll lock, phone-sized screens only), `utils/filter-form.util.ts`, and `count` methods on `ListingsApi` and `SearchApi` (page size 1) for the live "Show N pieces" button. The old `filter-panel` component was removed. Listing and search pages keep the previous cards, dimmed and `aria-busy`, while a new filter of the same listing loads, and show "Showing 1-24 of N" under the grid.

**Deviations from the spec, for the owner:**
- **Swatches show the colour's initial, not its colour.** The colour lookup carries no colour value anywhere (no hex in Reference, the read model or the listing options), so nothing is guessed. Real colour dots need a colour value added to the colour lookup (a Reference change; its own spec).
- **No auto-submit on the sort select.** Changing a select must not change the page by itself (WCAG 3.2.2), so the sort select has an explicit Apply button beside it. This also works with scripting off, so no `<noscript>` is needed (a `<noscript>` button caused a hydration error).
- **FLT-5 skeleton cards were not built.** With a previous page the cards stay dimmed; with nothing on screen the existing "Loading…" text shows as before.
- **Selected chip remove link** is a separate 28px link at the chip's right edge (a link cannot sit inside a `<summary>`).
- **Banner after the fourth card (Q-FLT-2)** deliberately not built.
- Colour swatch is part of `filter-fields`, not a separate component.
