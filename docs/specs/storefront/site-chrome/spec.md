# SCRUM-205: Storefront site chrome (Story 2 of 5)

- **Status:** Draft v2 (owner answers of 2026-10-24 applied; awaiting approval; nothing is built)
- **Jira:** SCRUM-205 (task for SCRUM-203, Sprint 9). Story 1 is SCRUM-204 (`design-foundation`, implemented, not yet pushed).
- **BRD sections:** §4.1 FEAT-CAT-01, FEAT-CAT-05, §5.1, §14; design source `docs/design-system/` (SiteHeader, MegaMenu, SearchOverlay, SiteFooter), used as a guide, not as exact values
- **Requirement IDs:** FR-01 (presentation), FR-04 (search box and suggestions UI only), NFR-01, NFR-17, NFR-19, SEO-01, SEO-04, SEO-08
- **Branch:** `SCRUM-205-storefront-site-chrome`, cut from `SCRUM-204-storefront-design-foundation`.

## Problem & intent
Every storefront page needs the same frame: a sticky header with the main entries and search, a mega-menu that carries the age filters, a phone menu, a search overlay and a footer whose links lead to real pages. The frame is **driven by data**, so adding, renaming, reordering or removing a navigation entry, an age, a footer link or a page is a data change and not a code change. It ships with placeholder data and is wired to the catalogue and search APIs when those exist.

## Owner decisions applied (2026-10-24)
| # | Decision |
|---|---|
| O-1 | Header entries: **New in, Girls, Boys, Baby, Sale.** No Unisex, no Gifts. |
| O-2 | The look is **black and white** (navigation D-6 stands). The design system is a guide that "will change", not a contract for exact values. |
| O-3 | Age steps are **one year apart, starting from 2** (2Y, 3Y, … up to the 12Y end of the business range). The design's bands and range-style URL values are not used. |
| O-4 | **Cart and wishlist are skipped** for this story (no bag drawer, no bag count, no wishlist icon, no NgRx cart or wishlist state). |
| O-5 | **Newsletter is deferred** to a later sprint (no form in the footer). |
| O-6 | **Footer links lead to real pages** with premium content. |
| O-7 | WhatsApp number: **+92 331 5613217** (`+923315613217`), `https://wa.me/923315613217`. Held in the brand configuration, never in templates. |

## Requirements (quoted)
> **FR-01:** The system shall present navigation segmented by audience and age bracket, per FEAT-CAT-01.

> **FEAT-CAT-01 Age/gender mega-menu:** Primary nav segmented by audience then age bracket.

> **FR-04:** The system shall provide keyword search with autosuggest and safely handle/escape special or malicious input without error leakage.

> **Navigation NAV-5 / NAV-9 (approved):** "Given a new category, collection or age band is added as a lookup row, then the menu shows it with no code change." "The filter parameter uses the stable lookup code, not the display name."

> **Design, Implementation.md:** "Dialogs: native `<dialog>` (focus trap, Esc, inert background); close on scrim click; restore focus to the opener; lock body scroll."

> **Owner instruction:** clay-deep on CTAs and links is a Story 1 token rule; WCAG 2.2 AA with 44 px targets, `prefers-reduced-motion`, semantic HTML and SEO; storefront project only; no formatting-only changes; no brand assets.

## Conflicts that remain (not resolved here)
1. **Black and white vs the Story 1 tokens.** Story 1 is implemented with the warm off-white and clay palette (`accent-deep` for links and primary buttons). O-2 says black and white. Q-SC-1.
2. **Baby with ages starting at 2.** "Baby" was the design's 0–2 audience; with O-3 no age below 2 exists in the filter. Q-SC-2.
3. **Sale and Baby are not catalogue data yet.** Sale is a collection (Catalog Q-PRD-5, names are the owner's); Baby does not exist as a gender, category, collection or age. Until the data exists the entries show the proper empty state. Q-SC-3.
4. **Catalog age rows** (Newborn, Infant, Toddler 1–3Y, Kids 4–8Y, Tween 9–12Y, proposed Toddler/Kids/Junior) do not match one-year steps from 2. The Catalog age lookup needs a data change; this story's mock data uses the new steps. Q-SC-4.
5. **Search overlay vs SRC-9** (GET form without scripting). Q-SC-5.
6. **Footer pages** are public pages the navigation spec called out of scope; this story brings them in. The approved navigation spec is amended (see "Amendments" below).

## What exists today (explored 2026-10-24)
Storefront has only the Story 1 foundation: tokens, `rp-image`, `rp-icon`, button, link-CTA, icon-button and `rp-field` directives, the `ImageAsset` model, a dev-only `/_foundations` page, an empty route table. No NgRx store, no `layout/` folder, no mock-data file, no header or footer, no brand configuration. All of those are new. Shared UI is reused.

## Scope
**In** (all under `src/web/projects/storefront`)
1. **Navigation as data.** One typed configuration (`shared/data/nav-config`, later replaced by the API response of the navigation feature) describes: top-level entries (label, link or menu, order, visible), per entry the mega-menu columns (title and links), the age list, and the footer columns. The header, mega-menu, mobile menu and footer all render from it; no label, URL or count is written in a template.
2. `layout/site-header`: sticky; announcement bar; wordmark placeholder (brand name from configuration); entries from the configuration; desktop search field; search icon on phones.
3. `layout/mega-menu`: Shop by age (2Y to 12Y), Clothing (categories), The edit (collections), one image tile (placeholder). Opens on hover/focus-in (desktop) and click/Enter; Esc closes; closes on leaving the panel and on route change; focus moves to the first link; `aria-expanded` on the trigger. Not rendered under 64rem.
4. `layout/mobile-menu`: left drawer on `<dialog>`; search at the top; accordion lists for entries that have a menu.
5. `layout/search-overlay`: `<dialog>`; 200 ms debounced suggestions from a mock-backed service; Popular chips; up to four suggested products; empty state; Enter goes to `/search?q=`.
6. `layout/site-footer`: dark; link columns from the configuration; contact and WhatsApp link; decorative wordmark; legal row. No newsletter.
7. **Footer pages** (see "Footer pages"): `/delivery`, `/exchanges`, `/size-guide`, `/our-story`, `/made-in-pakistan`, `/contact`, plus `/privacy` and `/terms` only if you want them (Q-SC-6). Server-rendered, with their own title, description, canonical, Open Graph tags and breadcrumbs.
8. App shell: header, `<main>` with the router outlet, footer, skip link.
9. `shared/data/mock-nav.ts` and `mock-products.ts` (placeholders only).
10. Unit tests for each component and the configuration; a Playwright + axe spec for the chrome and one footer page at 390, 768 and 1280 px.

**Out:** cart, bag drawer, bag count, wishlist (O-4); newsletter (O-5); reveal directive and view transitions (Q-SC-7, proposed: leave out); home, listing, product page, product card (stories 3 to 5); the real catalogue and search APIs; breadcrumbs for catalogue pages (navigation feature); admin; brand assets; the Fit Finder.

## Scalability: how the configuration stays flexible
Each of these is a configuration change only, with a test that proves it:
- **Add, rename, reorder or remove** a top-level entry (header, mobile menu and footer follow).
- An entry is a **plain link** or a **menu**; changing one into the other changes nothing else.
- **Add or remove an age** or change the age range; the menu shows what the list contains, and the URL carries the stable code.
- **Hide an entry temporarily** (a `visible` flag) or hide one whose data is empty (Q-SC-3).
- **Many entries:** the header wraps gracefully and the desktop row falls back to the mobile menu when entries do not fit; long labels truncate without breaking the 44 px target.
- **Different locale or brand name:** labels and brand text come from the configuration (the brand configuration of navigation B-1), so a rename needs no template edit.
- **Unknown or broken link:** a link whose target returns 404 is caught by a test over the configuration (every internal link resolves to a route).
- **Duplicates and empty menus:** duplicate ids fail the configuration validation test; a menu with no links renders as a plain link or is omitted.
- **Failure of the data source (later, when it is an API):** the last good configuration is used, never an empty header.

## Footer pages
Each page: one `<h1>`, semantic sections, breadcrumb, a unique title and meta description, a self-referencing canonical, Open Graph and Twitter tags, `BreadcrumbList` JSON-LD, server-rendered, 200 status. Copy is written to read as premium (calm, specific, no filler), as **placeholder copy for your review**. Content that states a business fact is marked with a visible `[CONFIRM]` marker in the source and listed under Open questions, because I cannot know it: delivery times and charges, courier names, the exchange window and conditions, fabric and manufacturing claims, addresses, hours. No fact is invented in a way that could reach production unreviewed (a test fails the build if any `[CONFIRM]` marker remains in a production build).

| Route | Purpose |
|---|---|
| `/delivery` | Delivery areas, times, charges, cash on delivery, tracking |
| `/exchanges` | Exchange and return conditions, window, how to start one |
| `/size-guide` | How to measure, an age-by-age size table (2Y to 12Y), fit notes |
| `/our-story` | Who Romp is and what it stands for |
| `/made-in-pakistan` | Where and how garments are made, fabrics, quality control (draws on the vendor and QC process) |
| `/contact` | WhatsApp (`+92 331 5613217`), email, hours, how to reach us |

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| SC-1 | Given any page, then the server HTML has one `<header>`, `<nav aria-label="Primary">` listing exactly the visible configured entries (today New in, Girls, Boys, Baby, Sale), one `<main>`, one `<footer>`; the wordmark link has an accessible name | CLAUDE.md, O-1 |
| SC-2 | Given an entry configured as a link, then it is an `<a href>`; given one configured as a menu, then it is a `<button aria-expanded aria-haspopup>` and its panel's links are in the server HTML | NAV-2, SEO-01 |
| SC-3 | Given the configuration gains, loses, renames or reorders an entry, an age, a menu column or a footer link, then header, mega-menu, mobile menu and footer follow with no template or component change | NAV-5, owner (2026-10-24) |
| SC-4 | Given the configuration validation test, then duplicate ids, a link to a route that does not exist, an entry with an empty menu and an age outside the allowed range fail the build | owner scalability request |
| SC-5 | Given the age list, then it shows one entry per year from 2Y up to 12Y; each is an `<a href>` to the audience listing with the age's stable code as the query parameter, never a display name or a range | O-3, NAV-8, NAV-9 |
| SC-6 | Given a desktop viewport and pointer over, or focus inside, a menu entry, then the mega-menu opens; Esc, leaving the panel or a route change closes it; focus moves to the first link, Tab stays inside until closed, and Esc returns focus to the trigger | Design, WCAG 2.1.2, 2.4.3 |
| SC-7 | Given a viewport under 64rem, then the mega-menu is not rendered; the menu button opens the mobile menu `<dialog>`; every item is a link or button with a target of at least 44 px | NAV-6 |
| SC-8 | Given any `<dialog>` (mobile menu, search), then it opens with `showModal()`, traps focus, makes the background inert, locks body scroll, closes on Esc and scrim click, and returns focus to the opener | Implementation.md, WCAG 2.4.3 |
| SC-9 | Given the search overlay and typing, then suggestions update 200 ms after the last keystroke with one request per pause, and every character is rendered as text (HTML-encoded), never as markup | FR-04, SRC-3 |
| SC-10 | Given Enter in the overlay or the header field, then the browser goes to `/search?q=<encoded>`; given no suggestions, "Nothing for 'x'" shows with links to the configured entries; given scripting off, the header field is a plain GET form | SRC-9, CLAUDE.md |
| SC-11 | Given the footer, then it has link columns of `<ul>` lists generated from the configuration, a contact line with the WhatsApp link from the brand configuration (`https://wa.me/923315613217`), a decorative `aria-hidden` wordmark and a legal row, and no newsletter form | O-5, O-7 |
| SC-12 | Given every footer link, then it resolves to a route that returns 200 with content; a test fails if any footer link points to `#`, a missing route or a page that returns 404 | O-6 |
| SC-13 | Given each footer page, then it has one `<h1>`, no skipped heading, a unique `<title>` and description, a self-referencing canonical, Open Graph and Twitter tags, a visible breadcrumb and matching `BreadcrumbList` JSON-LD, all in the server HTML | SEO-01, 03, 04, 08 |
| SC-14 | Given the production build, then no `[CONFIRM]` marker remains in any page or configuration; the build fails if one does | owner (premium content, no unreviewed facts) |
| SC-15 | Given the size guide, then its table lists every configured age from 2Y to 12Y with measurements that come from one data file, and uses `<table>` with `<th scope>` | CLAUDE.md, FR-08 spirit |
| SC-16 | Given every button, link, icon button and input in the chrome, then its hit area is at least 44 × 44 px, focus shows the Story 1 focus ring, and the sticky header never hides a focused element | NFR-19, WCAG 2.4.11, 2.5.8 |
| SC-17 | Given the palette decision (Q-SC-1), then text and controls in the chrome meet 4.5:1 (text) and 3:1 (control borders) on every surface, and no raw colour appears outside the token file | O-2, DSF-1, DSF-4 |
| SC-18 | Given `prefers-reduced-motion: reduce`, then the drawer slide, the mega-menu fade and the nav underline slide are removed | WCAG 2.3.3 |
| SC-19 | Given 390, 768 and 1280 px, then there is no horizontal scroll, and an axe run (wcag2a/2aa/21aa/22aa) reports no violations with each dialog closed and open, on the chrome and on one footer page | NFR-17, NFR-19 |
| SC-20 | Given the server-rendered HTML of any page, then Lighthouse reports SEO 100 and Accessibility ≥ 95 | NFR-01, SEO-10 |
| SC-21 | Given this story's diff, then it changes only `src/web/projects/storefront`, its e2e spec and docs; the admin project and unrelated files are untouched, with no formatting-only edits and no brand assets | owner instruction |

## Amendments to the approved navigation spec (made with this draft)
Per the owner's "yes" on header items and the decisions above, `navigation/spec.md` carries an amendment note: header entries are New in, Girls, Boys, Baby, Sale (no Unisex, no Gifts); the palette stays black and white; age entries are one per year from 2Y; the footer pages are in scope for the storefront. The original text is kept and marked superseded where it differs.

## Non-functional constraints
NFR-01 (no layout shift from the chrome, fonts already local, dialogs add no JavaScript to first paint beyond the header), NFR-17, NFR-19 WCAG 2.2 AA, SEO-01 (header, menu links, footer and footer pages are in the server HTML). Security: all text from data or user input is rendered through Angular bindings (no `innerHTML`); no personal data is collected by the chrome.

## Open questions
- [ ] **Q-SC-1 Palette.** Story 1's tokens are warm off-white and clay. For black and white, do I (a) change the storefront tokens in this story (a Story 1 amendment: surface white, ink black, accent black or grey, no clay), or (b) keep the clay tokens until a later restyle? Proposed: (a), done as a token-only change so no component changes, with the contrast checks re-run.
- [ ] **Q-SC-2 Baby.** With ages starting at 2, is "Baby" an audience entry whose listing shows products for sizes below 2Y, or is it still a placeholder entry? If Baby needs sizes under 2Y, the age list needs entries below 2.
- [ ] **Q-SC-3 Empty entries.** Until Baby and Sale have data, should the entry (a) show anyway and land on the empty state, or (b) be hidden automatically while it has no live products (configuration flag)? Proposed: (a) for launch content, with (b) available per entry.
- [ ] **Q-SC-4 Age lookup.** The Catalog age rows must change to one row per year from 2Y to 12Y (and the upper end, 12Y, confirmed). May I list this as a Catalog data change for its own task, and use the new steps in mock data here?
- [ ] **Q-SC-5 Search overlay.** Proposed: GET form first, enhanced by the dialog and a mock-backed suggestion service, with the combobox pattern on the overlay input. Acceptable?
- [ ] **Q-SC-6 Pages and facts.** Is the page list above right (add `/privacy` and `/terms`, or leave them)? Please supply or confirm: delivery cities, times and charges, courier(s), the exchange window and conditions, contact email, hours and address if public, and the claims for Our story and Made in Pakistan. Until then these appear as marked `[CONFIRM]` placeholders and the production build refuses to ship them.
- [ ] **Q-SC-7 Reveal and view transitions.** Optional scroll-in and page crossfade motion. Proposed: not in this story. Confirm leaving them out.
- [ ] **Q-SC-8 Announcement bar.** Cart and free-delivery progress are out, so the bar is a plain one-line message from configuration. What should it say at launch, or should it be hidden until you decide?
