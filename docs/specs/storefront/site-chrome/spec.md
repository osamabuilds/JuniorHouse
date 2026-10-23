# SCRUM-205: Storefront site chrome (Story 2 of 5)

- **Status:** Draft (awaiting owner approval; nothing is built)
- **Jira:** SCRUM-205 (task for SCRUM-203, Sprint 9). Story 1 is SCRUM-204 (`design-foundation`, implemented, not yet pushed).
- **BRD sections:** §4.1 FEAT-CAT-01, FEAT-CAT-05, §5.1, §14; design source `docs/design-system/` (components SiteHeader, MegaMenu, SearchOverlay, CartDrawer, SiteFooter)
- **Requirement IDs:** FR-01 (presentation only), FR-04 (the search box and suggestions UI only), NFR-01, NFR-17, NFR-19, SEO-01
- **Branch:** `SCRUM-205-storefront-site-chrome`, cut from `SCRUM-204-storefront-design-foundation` (Story 1 has no PR yet; if it merges first, rebase on `main`).

## Problem & intent
Every storefront page needs the same frame: an announcement bar, a sticky header with five entries and search, a mega-menu that carries the age filters, a phone menu, a search overlay, the bag drawer and a dark footer. Building the frame once, from the approved design, lets stories 3 to 5 (home, listing, product) concentrate on their own content. The frame ships with placeholder data (a single mock-data file) and is wired to the real catalogue, search and cart APIs later.

## Requirements (quoted)
> **FR-01:** The system shall present navigation segmented by audience and age bracket, per FEAT-CAT-01.

> **FEAT-CAT-01 Age/gender mega-menu:** Primary nav segmented by audience then age bracket.

> **FR-04:** The system shall provide keyword search with autosuggest and safely handle/escape special or malicious input without error leakage.

> **Design, SiteHeader:** "Exactly five items" (New in, Girls, Boys, Baby, Gifts). "Girls, Boys and Baby are buttons that open the mega-menu; New in and Gifts are links."

> **Design, MegaMenu:** "Age filters live here: the first column is Shop by age (Baby 0–2, Toddler 2–4, Little 4–6, Junior 6–9, Big kids 9–12), each linking to the category filtered by age (`/girls?age=4-6`)."

> **Design, Implementation.md:** "`rp-cart-drawer`: `<dialog>` right drawer, bottom sheet under 40rem; cart state in the existing NgRx setup." "Dialogs: native `<dialog>` (focus trap, Esc, inert background); close on scrim click; restore focus to the opener; lock body scroll."

> **Owner instruction (2026-10-24):** clay-deep on CTAs and links, WCAG 2.2 AA with 44 px targets, `prefers-reduced-motion`, semantic HTML and SEO; storefront project only; no formatting-only changes; no brand assets.

## Conflicts raised (not resolved; each needs your ruling before the plan)

1. **Header items.** Approved `navigation/spec.md` (NAV-1, user flow 1) shows audiences Girls, Boys, Unisex plus New In and Sale. The design has exactly New in, Girls, Boys, Baby, Gifts, with no Sale and no Unisex entry. Q-DF-2 (Story 1) accepted amending the navigation spec, but I have not changed it. See Q-SC-1.
2. **Baby and Gifts are not in the catalogue data.** Catalog's genders are Boys, Girls and Unisex; categories are Tops, Bottoms, Dresses, Outerwear, Sleepwear, Accessories (flat, not a tree); collections are owner-defined (Catalog Q-PRD-5). Neither "Baby" nor "Gifts" exists as a gender, category, collection or age band. The URLs `/baby` and `/gifts` therefore have nothing to list. See Q-SC-2.
3. **Age bands differ in three ways.** Design: Baby 0–2, Toddler 2–4, Little 4–6, Junior 6–9, Big kids 9–12, with values like `?age=4-6`. Catalog (D3, Q-AGE-2): the business range is 1–12Y, proposed bands Toddler 1–3Y, Kids 4–8Y, Junior 9–12Y, seeded rows Newborn, Infant, Toddler, Kids, Tween. The navigation spec (NAV-8, NAV-9) requires the filter parameter to carry the stable lookup code, not a display name or a range. Also, "Baby" appears twice in the design (a top-level item and an age band). See Q-SC-3.
4. **Palette.** Navigation D-6 fixes a "black and white base"; the design uses warm off-white with clay. Owner said follow the design; the D-6 text is still in the approved spec. Covered by Q-SC-1.
5. **Cart state before the cart exists.** The cart is out of scope for Sprint 9 (storefront README: cart in S11), there is no backend cart, and the storefront has no NgRx yet. The brief asks for `rp-cart-drawer` with NgRx cart state. See Q-SC-4.
6. **Search overlay vs the approved search spec.** SRC-9 requires an ARIA combobox that works as a plain GET form with scripting off; the design's overlay is a `<dialog>` with chips and suggested product cards. Suggestions need the search API, which does not exist yet. See Q-SC-5.
7. **Footer targets.** The navigation spec lists "the footer's legal and help pages" as out of scope, yet the design footer links to Delivery, Exchanges, Size guide, WhatsApp, Our story, Made in Pakistan and Contact. See Q-SC-6.
8. **Reveal directive and view transitions** were deferred to "story 2/3" (Q-DF-7). Stated here as in scope (Q-SC-7).

## What exists today (explored 2026-10-24)
Storefront has the Story 1 foundation only: tokens, `rp-image`, `rp-icon` (13 icons), button/link-CTA/icon-button directives, `rp-field`, `ImageAsset` model, a dev-only `/_foundations` page and an empty route table (`/` redirects to the foundations page in development). No NgRx store, no `layout/` folder, no mock-data file, no header or footer, no router outlet wrapper. The handoff `_components.scss` already carries the `rp-header`, `rp-mega`, `rp-drawer`, `rp-footer` classes, so component SCSS holds layout only. Nothing needs to be replaced; shared UI is reused (`rp-icon`, `rp-image`, button, link-CTA, icon-button, field).

## Scope
**In** (all under `src/web/projects/storefront`)
1. `layout/site-header`: announcement bar and sticky bar; nav built from one const array; wordmark placeholder; desktop search field, wishlist (icon only, no state, see Q-SC-8), bag with live count.
2. `layout/mega-menu`: Shop by age, Clothing, The edit, one image tile (placeholder); hover/focus-in on desktop, click/Enter anywhere; Esc closes; closes on leaving the panel and on route change; focus moves to the first link and Tab stays inside until closed; `aria-expanded` on the trigger. Replaced by the mobile menu under 64rem.
3. `layout/mobile-menu`: left drawer on a native `<dialog>`; search field at the top; accordion sub-lists for Girls, Boys, Baby.
4. `layout/search-overlay`: full-viewport `<dialog>`; 200 ms debounced suggestions; Popular chips; up to four suggested product cards; empty state "Nothing for 'x'"; Enter goes to `/search?q=`; Esc/close returns focus to the opener.
5. `layout/cart-drawer`: right drawer (440 px), bottom sheet under 40rem, scrim; header "Your bag (n)"; free-delivery bar; lines with 4:5 image, quantity stepper and price; sticky footer with subtotal, Checkout, COD note. State in a small NgRx `cart` feature (see Q-SC-4); all totals computed from the line items in one selector, never typed in (SEC-09 spirit, even for mock data).
6. `layout/site-footer`: `rp-dark`; newsletter form (see Q-SC-9); Shop, Help, Romp columns; decorative wordmark (`aria-hidden`); legal row.
7. App shell: header, `<main>`, footer around the router outlet; skip link to `<main>`.
8. `shared/ui/reveal.directive.ts` (`[rpReveal]`: `rp-js` on `<html>` and `.is-in` via `IntersectionObserver`, browser only, nothing under reduced motion) and `provideRouter(..., withViewTransitions())` with the reduced-motion opt-out. The server HTML stays fully visible.
9. `shared/data/mock-nav.ts` and `mock-products.ts` (placeholders only; replaced by the API in stories 3 to 5 and S9 backend).
10. Unit tests for every component and the directive; one Playwright + axe spec for the chrome at 390, 768 and 1280 px.

**Out:** home, listing, product page, product card, quick view (stories 3 to 5); backend cart, checkout, wishlist state, accounts (S10/S11); real search and catalogue API calls; breadcrumbs (navigation feature, with the listing and product pages); admin; any brand asset or real photograph; the Fit Finder.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| SC-1 | Given any page, then the HTML returned by the server contains one `<header>`, `<nav aria-label="Primary">` with exactly the five items from the nav array, one `<main>`, and one `<footer>`; the wordmark link has `aria-label="Romp, home"` | Design, CLAUDE.md |
| SC-2 | Given New in and Gifts, then they are `<a href>` links; given Girls, Boys and Baby, then they are `<button aria-expanded aria-haspopup>` that also have a crawlable `<a href>` fallback inside the panel (the panel markup is in the server HTML) | Design, SEO-01, NAV-2 |
| SC-3 | Given a desktop viewport and the pointer over, or focus inside, Girls, then the mega-menu opens within `--dur-base`; Esc, leaving the panel, or a route change closes it; focus returns to the trigger on Esc | Design |
| SC-4 | Given the mega-menu is open, then focus moves to its first link, Tab does not leave the panel until it is closed, and the trigger shows `aria-expanded="true"` | Design, WCAG 2.1.2, 2.4.3 |
| SC-5 | Given an age entry in the mega-menu, then it is an `<a href>` to the audience listing with the age as a query parameter whose value format is decided in Q-SC-3 | Design, NAV-8, NAV-9 |
| SC-6 | Given a viewport under 64rem, then the mega-menu is not rendered and the menu button opens the mobile menu `<dialog>`; every item is a link or a button, targets are at least 44 px, Esc/scrim closes it and focus returns to the opener | Design, NAV-6 |
| SC-7 | Given any `<dialog>` (mobile menu, search, bag), then it opens with `showModal()`, traps focus, makes the background inert, locks body scroll, closes on Esc and scrim click, and returns focus to the opener | Implementation.md, WCAG 2.4.3 |
| SC-8 | Given the search overlay and typing, then suggestions update 200 ms after the last keystroke (not before, and one request per pause); every character is rendered as text (HTML-encoded), never as markup | Design, FR-04, SRC-3 |
| SC-9 | Given the search overlay and Enter, then the browser goes to `/search?q=<encoded query>`; given a query with no suggestions, then "Nothing for 'x'" shows with links to the five nav entries; given scripting off, then the header field is a plain GET form to `/search` | Design, SRC-9, CLAUDE.md |
| SC-10 | Given the bag drawer, then it opens from the bag icon (and later from Add to bag), shows "Your bag (n)", the free-delivery bar, line items, quantity steppers, subtotal and Checkout; it is a right drawer at 40rem and wider and a full-width bottom sheet below | Design |
| SC-11 | Given cart lines in the store, then the count on the bag icon, the "(n)" in the title, the free-delivery progress and the subtotal are all derived by selectors from the same lines; quantity 0 removes a line; quantity cannot go below 0 or above the stock stated on the line | SEC-09 (new), FR-28 spirit (new) |
| SC-12 | Given an empty bag, then the drawer shows a plain empty state with a link to New in, the Checkout button is disabled with a stated reason via `aria-describedby`, and nothing shows `Rs. 0` as a call to action | Design (Button README), `(new)` |
| SC-13 | Given the bag count changes, then an `aria-live="polite"` region announces it; the count dot is 16 px, clay with espresso text | Implementation.md (WCAG checklist) |
| SC-14 | Given the footer, then it is `rp-dark`, has the newsletter form with a visible label, an email-type input and a Join button, three link columns of `<ul>` lists with 36 px rows, a decorative `aria-hidden` wordmark and a legal row; text and links meet 4.5:1 on espresso | Design, WCAG 1.4.3 |
| SC-15 | Given every button, link, icon button and input in the chrome, then its hit area is at least 44 × 44 px, focus shows the 2 px outline with 3 px offset (cream inside `rp-dark`), and the sticky header never hides a focused element (`scroll-padding`) | NFR-19, WCAG 2.4.11, 2.5.8 |
| SC-16 | Given links and primary buttons, then clay text and fills use `accent-deep` (`clay-deep`); brand `accent` appears only on non-text marks (the wordmark full stop, the nav underline, the bag dot) | Owner instruction, DSF-4 |
| SC-17 | Given `prefers-reduced-motion: reduce`, then the reveal, the drawer slide, the mega-menu fade, the nav underline slide and view transitions are removed and elements appear without motion; given no reduced-motion preference and a browser without the View Transitions API, then navigation works normally | Design, WCAG 2.3.3 |
| SC-18 | Given the server-rendered HTML, then no content is hidden by the reveal's initial state (it applies only under `rp-js`), and a Lighthouse run reports SEO 100 and Accessibility ≥ 95 on the chrome-bearing page | SEO-01, NFR-01 |
| SC-19 | Given 390, 768 and 1280 px, then there is no horizontal scroll, header bar height is 56 px under 64rem and 72 px from 64rem, and an axe run (wcag2a/2aa/21aa/22aa) reports no violations with each dialog closed and open | NFR-17, NFR-19 |
| SC-20 | Given this story's diff, then it changes only `src/web/projects/storefront`, the storefront entries of `angular.json`/`package.json` if a package is needed (none is planned), the e2e spec, and docs; the admin project and unrelated files are untouched, with no formatting-only edits and no brand assets | Owner instruction |

## Non-functional constraints
NFR-01 (chrome adds no layout shift; dialogs and drawers are lazy-rendered and add no JS to first paint beyond the header; fonts already local), NFR-17 mobile-first, NFR-19 WCAG 2.2 AA, SEO-01 (header, menu links and footer are in the server HTML). Security and coupling: all text from mock data or user input is rendered through Angular bindings (no `innerHTML`); the newsletter form posts nowhere in this story (Q-SC-9); no personal data is stored.

## Out of scope
See Scope. Also dark mode, language switch, account menu, currency switcher.

## Open questions
- [ ] **Q-SC-1 Header items vs navigation spec.** Proposed: the design wins; I amend `navigation/spec.md` (NAV-1, user flow 1, D-6, NAV-10 palette) to New in, Girls, Boys, Baby, Gifts and drop "Sale" and "Unisex" from the header (Sale becomes a collection reachable inside the menu's "The edit"). Do you confirm, and may I edit the approved navigation spec for it?
- [ ] **Q-SC-2 Baby and Gifts data.** Options: (a) Baby is an age filter or a gender-like audience for 0–2Y and Gifts is a collection, both created in Catalog as lookup rows (a Catalog change outside this story); (b) both ship in the header as links to pages that show the empty state until data exists; (c) leave Baby and Gifts out until the data exists, giving three items. The design says exactly five, so (b) matches it. Which?
- [ ] **Q-SC-3 Age bands and the `?age=` value.** Which band names and ranges are final (design 0–2/2–4/4–6/6–9/9–12 versus Catalog 1–3/4–8/9–12)? The filter value must be a stable lookup code (NAV-9), not `4-6`. Proposed: the menu renders whatever bands the age lookup has, with the design's names as placeholders in mock data; the design's range format is not used in URLs.
- [ ] **Q-SC-4 Cart state.** Options: (a) build a minimal NgRx `cart` feature in memory (add, remove, change quantity, selectors) fed by mock lines, with no persistence and no backend, so the drawer is real; S11 replaces the effects; (b) build the drawer around an input-only presentational component and add NgRx in S11. Proposed: (a). Should the cart persist to local storage in the browser (as the wishlist is meant to)?
- [ ] **Q-SC-5 Search overlay.** Proposed: header field and overlay are a GET form to `/search` first (works without scripting), enhanced by the dialog and a mock-backed suggestion service; the ARIA combobox pattern of SRC-9 is applied to the overlay's input and suggestion list. Later the service calls the S9 suggest endpoint. Is that acceptable, or should SRC-9 be amended to the dialog pattern?
- [ ] **Q-SC-6 Footer links.** Which footer links point at real pages now? Proposed: Shop column links to the five nav targets; Help and Romp links render as links to routes that return a proper 404 until the pages exist (not `#`, no dead anchors), or are omitted. Which of the Help/Romp pages are in launch scope?
- [ ] **Q-SC-7 Reveal directive and view transitions.** Proposed in this story (first user is the header/footer and, from Story 3, the home sections).
- [ ] **Q-SC-8 Wishlist icon.** Wishlist is S10 in the storefront README. Proposed: the header shows the icon as a link to `/wishlist` only when that route exists; until then it is omitted (the design lists five nav items, not the icon, as a fixed count). Confirm.
- [ ] **Q-SC-9 Newsletter.** There is no newsletter backend, consent text or privacy page. Proposed: the form validates the email client-side and shows a success message without sending anything, with a visible "demo only" TODO in the plan; real submission waits for an owner decision on consent and storage. Confirm, or omit the form for now.
- [ ] **Q-SC-10 Announcement copy.** The preview says "Complimentary delivery over Rs. 8,000 · Cash on delivery". Is the Rs. 8,000 threshold a business fact or placeholder copy? Proposed: placeholder, kept in the mock data file, not in templates.
