# SCRUM-21: Navigation, breadcrumbs and home page

- **Status:** Approved (owner, 2026-10-16; open items below carry their proposed default)
- **Jira:** SCRUM-21 (FR-01); umbrella SCRUM-203
- **BRD sections:** §4.1 FEAT-CAT-01, §5.1, §14.1
- **Requirement IDs:** FR-01, SEO-03 (BreadcrumbList)

## Problem & intent
A parent shops for a particular child, so the first question is "for whom". The site must lead from audience to age to category without dead ends, work on a phone, and show where the visitor is.

## Requirements (quoted from the BRD)
> **FR-01:** The system shall present navigation segmented by audience and age bracket, per FEAT-CAT-01.

> **FEAT-CAT-01 Age/gender mega-menu:** Primary nav segmented by audience then age bracket.

> **§5.1:** Homepage → mega-menu → segment (age/gender) → New In / Sale / Category / Collection sub-nav.

## User flow
1. The header shows the audience entries (Girls, Boys, Unisex) and the shared entries (New In, Sale). Opening an audience shows its age bands and, per segment, New In, Sale, Category and Collection entries.
2. On a phone the same structure is a full-screen menu opened by a button; every item is a real link.
3. Every page below the home page shows a visible breadcrumb trail.
4. The menu is built from data (genders, categories, collections, age bands) so a new category or collection appears without a code change.
5. Age entries link to the audience listing with age as a filter query parameter (`/girls?age=toddler`); the page's canonical is the audience page (decision D-1).
6. The home page is minimal: a hero from New Arrivals, tiles for the audiences, the site-wide structured data and the home meta. Design direction: black and white base, premium, fashion-forward (D-6).
7. The brand name, tagline, logo and site name in the header, footer and tags come from one brand configuration (B-1).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| NAV-1 | Given the Girls menu is opened, then New In, Sale, Category and Collection entries are all present and scoped to that audience | TC-CAT-01 |
| NAV-2 | Given any menu item, then it is an `<a href>` with a clean URL, and the menu is operable by keyboard and works with scripting disabled for crawlers (the links are in the server HTML) | CLAUDE.md semantic HTML, SEO-01 |
| NAV-3 | Given a category or collection with no live products, then it is not offered in the menu (or is offered only if Q-NAV-3 says so) | FR-05 `(new)` |
| NAV-4 | Given a page below the home page, then a breadcrumb `<nav aria-label="Breadcrumb">` with an ordered list shows the trail, matches the `BreadcrumbList` structured data, and the current item is not a link | SEO-03 |
| NAV-5 | Given a new category, collection or age band is added as a lookup row, then the menu shows it with no code change | CLAUDE.md growth rule |
| NAV-6 | Given a phone width (320 px), then the menu opens, closes and is fully usable, with targets of at least 44 px | NFR-17, NFR-19 |
| NAV-8 | Given an age entry in the menu, then it links to the audience listing with the age filter as a query parameter, the filtered page's canonical is the audience page, and no page has an age-named path | D-1, Catalog D2 |
| NAV-9 | Given the age bands are renamed or added as lookup rows, then the menu follows and no URL breaks (the filter parameter uses the stable lookup code, not the display name) | D-1 |
| NAV-10 | Given the home page, then it shows a hero from New Arrivals (omitted when there are none), the audience tiles, a single `<h1>`, `Organization` and `WebSite` JSON-LD and the home meta template; it is server-rendered with real status 200 | D-6, SEO-01 |
| NAV-11 | Given the brand configuration changes (name, tagline, logo, site name), then header, footer, titles, Open Graph `site_name` and JSON-LD `Organization` all change with no code change and no rebuild beyond the configuration reload | B-1 |
| NAV-7 | Given the menu markup, then there is one `<header>` with a `<nav>`, exactly one `<main>` per page, headings never skip a level, and visible focus on every control | NFR-19, WCAG 2.1 AA |

## Non-functional constraints
NFR-01 (the menu data is cached and ships in the server HTML, no client fetch), NFR-17 mobile-first, NFR-19 WCAG 2.1 AA.

## Out of scope
Editorial lookbook tiles in the menu (FEAT-CAT-09, P2); the age-personalised homepage (FEAT-CAT-10, P2); the footer's legal and help pages; the language switch.

## Open questions
- [x] **Q-NAV-1 (decided, D-1)** Age entries are filter links to the audience listing; canonical is the audience page; no age-named indexable pages in S9. Revisit landing pages with SEO data after launch.
- [ ] **Q-NAV-2 Age band names (Q-AGE-2).** Proposed names Toddler 1-3Y, Kids 4-8Y, Junior 9-12Y (owner, 2026-10-16: "proposed"); the existing rows Newborn, Infant and Tween are retired or renamed by a data change. Because names are lookup data (NAV-9), this is not a blocker, but confirm before staging content is entered.
- [ ] **Q-NAV-3 Empty categories in the menu.** Hide categories with no live products (proposed), or always show them and land on the empty state of FR-05?
- [ ] **Q-NAV-4 Unisex.** Catalog Q-URL-5 proposes `/unisex/...` with one canonical URL per product. Does a Girls visitor also see unisex products in the Girls listings (proposed: yes, as a listed product whose canonical page is the unisex URL), or only on the Unisex entry?
- [ ] **Q-NAV-5 Collections in the menu.** `COLN_LKP` launch names (New Arrivals, Essentials, Sale, …) are the owner's (Catalog Q-PRD-5). Which ones appear in the menu at launch?

## Amendment 2026-10-24 (SCRUM-205 and SCRUM-206, owner decision)
The header entries are **Girls, Boys, New in, Collections, Sale**. **Unisex, Baby and Gifts are not header entries**; this supersedes the Girls/Boys/Unisex/New In/Sale list in user flow 1 and NAV-1 where they differ. The palette stays black and white (D-6). Age entries are **one per year from 1Y to 8Y** (supersedes Q-NAV-2 band names); the filter parameter still carries the stable lookup code (NAV-8, NAV-9). **Every entry can be a main category with nested members, and members can have further members, to three levels (for example Boys: Tees and shirts, Bottoms, Casual shirts). Navigation entries, ages, footer links and page content are configured in the admin app** ([storefront-settings](../storefront-settings/spec.md)), so a change needs no code. An enabled entry with no products shows the empty state. The footer help and brand pages are in scope, specified in [site-chrome](../site-chrome/spec.md).

## Amendment 2026-10-25 (owner decision, Q-NAV-6)
**The header menu is built from lookups, not from the admin navigation editor.** This supersedes the 2026-10-24 sentence that navigation entries are configured in the admin app, the three-level nesting, and "an enabled entry with no products shows the empty state" **for the menu only**:

- Each audience (gender lookup row) opens to **New In, Sale, one entry per category, one per collection and one per age** (`?age=<code>`), all from lookup rows. An entry is shown only if the audience has at least one listable product in it (NAV-3, Q-NAV-3 decided: hide). New In and Sale are shown for an audience only if it has products (Sale: any on sale). Nesting is therefore audience then entry, one level; a new lookup row appears with no code change (NAV-5, NAV-9).
- The top-level entries are the audiences that have listable products, then **New in**, **Collections** and **Sale** (all audiences). Unisex is not a header entry (amendment of 2026-10-24); the genders left out are the configuration `Storefront:MenuHiddenGenderCodes` (default `UNISEX`) `(new)`.
- Paths follow the proposed listing paths of [listings](../listings/spec.md) Q-LST-8: `/{gender}`, `/{gender}/{category}`, `/{gender}/new-in`, `/{gender}/sale`, `/new-in`, `/sale`, `/collections/{code}`; a segment is the lookup code lowercased with `_` as `-`. They stay provisional until Q-LST-8 is decided.
- Ages: one entry per active age lookup row that has listable products for that audience.
- **Storefront settings:** footer, contact, delivery, exchange, announcement and content pages stay admin-managed. The age steps (SCRUM-206) no longer feed the menu, which reads the age lookup. The SCRUM-206 navigation editor no longer feeds the header; what happens to its stored data and screen is Q-NAV-7 `(new)`.
- [x] **Q-NAV-6 (decided 2026-10-25)** menu built from lookups, as above.
- [ ] **Q-NAV-7 (new)** Retire the admin navigation editor and its `NAV` data, or keep it for the footer only? Until answered nothing is deleted.
