# SCRUM-206: Storefront settings (admin-managed configuration)

- **Status:** Draft (awaiting owner approval; nothing is built)
- **Jira:** SCRUM-206 (task for SCRUM-203, Sprint 9). Consumed by SCRUM-205 (`site-chrome`).
- **BRD sections:** §4.1 FEAT-CAT-01 (navigation), §14 (SEO), NFR-01; the BRD has no requirement for a settings system. It is an owner decision (below), so **no FR ID applies and none is invented**; the BRD errata list (SCRUM-168) should log it.
- **Requirement IDs:** FR-01, NAV-5, NAV-9 (navigation data), NFR-01, SEC-08 (admin writes), SEO-01, SEO-04.

## Problem & intent
Owner decision (2026-10-24): the storefront must be **highly or completely configurable from the admin side**, so that changing navigation items, age steps, footer links, delivery, exchange and contact information, announcement text and content pages never needs a code change or a deployment. Staff change it in the admin app; the public storefront shows it, server-rendered, within a short time.

## Owner decisions
| # | Decision |
|---|---|
| S-1 | Header entries are configurable. Launch set: **Girls, Boys, New in, Collections, Sale** (no Baby, no Unisex, no Gifts). |
| S-2 | Age steps are configurable. Launch set: **1Y to 8Y, one year apart** (the earlier "from 2" and "to 12" are superseded). |
| S-3 | Footer links, delivery cities, times and charges, courier, exchange window, contact details (WhatsApp `+92 331 5613217`), the announcement bar text and the content pages are all configurable. |
| S-4 | An enabled navigation entry with no products shows an empty page (the empty state), not a hidden entry. Where products have sold, the entry may show the **sold items for that season** (Q-ST-4). |
| S-5 | Black and white look (navigation D-6). |

## Scope
**In**
1. **Data (schema `STFR`, owned by the Storefront module, ADR 0018)**, normalised, with lookups and the standard audit columns (names follow `docs/db/naming.md`; the plan fixes them):
   - **Navigation entries**: key, label, kind (link / menu / collection list), target, sort order, enabled; a menu entry has **columns** of **links** (label, target, order, enabled).
   - **Age steps**: stable code, label, sort order, enabled. Launch seed 1Y to 8Y. The code, not the label, goes into URLs (NAV-9).
   - **Footer columns and links**: title, order; link label, target, order, enabled.
   - **Brand and contact settings**: key/value rows with a type and a validation rule (brand name, tagline, WhatsApp number, email, hours, announcement text, announcement enabled).
   - **Delivery and exchange facts**: delivery areas (city, days, charge, free-delivery threshold, courier), exchange window and conditions, as typed rows (not free text) so the same facts appear in the delivery page, the exchange page, the footer and later the checkout without being typed twice.
   - **Content pages**: slug, title, meta description, body (structured blocks, Q-ST-2), status (Draft / Published / Archived), publish date, version.
2. **Admin app screens** (`src/web/projects/admin`, feature `storefront-settings`; admin conventions, NgRx): list and edit navigation (add, rename, reorder, enable/disable, nested links), ages, footer, brand and contact, delivery and exchange facts, and pages (create, edit, preview, publish, unpublish, archive). Inline validation with specific messages; reordering by a button alternative to drag (WCAG 2.5.7); a **preview** of the draft before publishing.
3. **API**: admin endpoints (create/read/update/reorder/publish) that check the caller's role on every request (SEC-08); one public read endpoint per concern, or one combined "site settings" document, cached, no personal data.
4. **Publishing model**: edits are saved as a draft; **Publish** makes the whole settings set live atomically (a half-edited menu is never visible); the last published version can be restored (rollback).
5. **Audit**: every change records who, when, what changed (ADR 0016, through the outbox).
6. **Cache and freshness**: the storefront reads the published set and caches it; publish invalidates the cache; the stale window is at most 60 s (matches the edge cache of P-2). If the settings source is down, the storefront keeps serving the **last good** copy and never an empty header.
7. **Sitemap and redirects**: publishing, unpublishing or changing a page's slug updates `sitemap.xml`; a changed slug gets a 301 from the old one; an archived page returns **410** (CLAUDE.md SEO rules).
8. **Seed data**: migrations seed the launch set (S-1, S-2), the six footer pages as **Draft**, the WhatsApp number, and placeholder values.

**Out**
Visual theming and layout changes (colours, fonts, section order on the home page; those stay in code); products, prices and stock (Catalog and Warehouse); the newsletter; the cart and wishlist; translations (a second language); A/B testing; media library beyond uploading images for pages (Q-ST-3); staff accounts and roles (Identity module; see Q-ST-5).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| ST-1 | Given an admin renames, reorders, adds or disables a navigation entry or link and publishes, then the public header, mobile menu and footer show it within 60 s with no deployment | S-1, NAV-5 |
| ST-2 | Given the age steps are edited (added, renamed, disabled) and published, then the mega-menu list follows, and links use the stable code; renaming a label does not change any URL | S-2, NAV-9 |
| ST-3 | Given a draft with unpublished edits, then the public site shows only the last published set; after Publish the whole set appears at once | Section 4 |
| ST-4 | Given an admin saves an invalid value (empty label, duplicate key, bad phone number, negative charge, link to a page that does not exist, a menu with no links), then it is rejected with a message naming the field and what to do, and nothing is saved | Validation |
| ST-5 | Given a navigation entry that points to a listing with no live products, then the public page returns 200 with the empty state (and `noindex`) while the entry is enabled; the entry never 404s | S-4, FR-05 |
| ST-6 | Given an enabled entry whose products have all sold, then the page shows the sold items for the current season as sold-out cards (rule in Q-ST-4) | S-4 |
| ST-7 | Given delivery or exchange facts change, then the delivery page, exchange page and footer show the new values, from one record each | S-3 |
| ST-8 | Given a page is published, then it is server-rendered with its title, meta description, canonical, Open Graph tags, breadcrumb and `BreadcrumbList` JSON-LD; given it is unpublished or archived, then it returns 404 (unpublished) or 410 (archived) and leaves the sitemap | SEO-01, 04, 05 |
| ST-9 | Given a page's slug changes, then the old URL returns a 301 to the new one | CLAUDE.md SEO |
| ST-10 | Given page body content from the admin, then it is rendered from structured blocks through Angular bindings; markup or script typed into any field is shown as text or stripped, never executed (no `innerHTML` of admin input without sanitisation) | SEC (input handling) |
| ST-11 | Given a non-admin or unauthenticated caller, then every write endpoint returns 401/403 and changes nothing; the check is server-side on every request | SEC-08 |
| ST-12 | Given any change, then an audit record names the user, time, entity, and old and new values; given a bad publish, then the previous published version can be restored | ADR 0016, rollback |
| ST-13 | Given the settings source is unreachable, then the storefront serves the last good copy; given there is none (first start), then it serves the built-in launch set and logs the fault | NFR-FT (resilience) |
| ST-14 | Given two admins edit the same entity, then the second save is rejected with a conflict message (concurrency token), not silently overwritten | Constitution |
| ST-15 | Given the admin screens, then they meet WCAG 2.2 AA, are usable at 390 px, every reorder has a button alternative and every control has a label | NFR-17, NFR-19 |
| ST-16 | Given the schema, then every table and column follows `docs/db/naming.md`, enumerated values use lookup tables, every FK is indexed, and the naming test passes | CLAUDE.md database rules |
| ST-17 | Given a page with a fact still marked unfilled (a required delivery or exchange field empty), then it cannot be published | Premium-content rule |

## Non-functional constraints
NFR-01 (settings ride in the server HTML, no client fetch for the chrome), SEC-08, ADR 0016 (audit), NFR-FT (last-good fallback). Settings contain no personal data. Architecture: Storefront module only; Admin talks to it over its own API; no cross-schema access.

## Open questions
- [ ] **Q-ST-1 Home of the settings.** Proposed: the **Storefront module** (schema `STFR`, already planned as a read-model schema) gets a second, small *write* side for settings, since its own consumers are the storefront. Alternative: a new `Content` module. Needs an ADR either way. Which?
- [ ] **Q-ST-2 Page content format.** (a) Structured blocks (heading, paragraph, list, table, image, call-out) edited with form fields: safest and most consistent, matches "premium" typography. (b) Rich-text/Markdown: more freedom, more risk. Proposed: (a).
- [ ] **Q-ST-3 Images in pages.** Allow image upload for pages now (reuses the image delivery of the published-catalog feature) or text-only pages for launch? Proposed: text-only at launch.
- [ ] **Q-ST-4 "Sold items for that season".** The catalogue has no "season" concept today. Is a season a **collection** (for example "Summer 2027"), a date range, or a field on the style? And should sold items show only on an otherwise-empty entry, or always? Proposed: a season is a collection; sold items appear only when no in-stock item exists.
- [ ] **Q-ST-5 Who can edit and when.** Authentication and roles land in S8. Until then the admin has no login (CLAUDE.md); the settings endpoints must not be deployed publicly before S8. Which role may edit (Content editor? Admin only?) and who may publish?
- [ ] **Q-ST-6 Sequence.** Proposed: SCRUM-206 backend + admin first (or the minimum slice: navigation, ages, brand/contact), then SCRUM-205 consumes it. SCRUM-205 can start earlier against the same typed contract with a built-in launch set. Which order do you want?
- [ ] **Q-ST-7 Admin is touched.** The earlier rule was "storefront only, never touch admin" (design stories). This story changes the admin app. Confirm that is intended, and whether the admin re-skin is still separate.
- [ ] **Q-ST-8 Collections entry.** Is "Collections" in the header a **menu** listing the live collections, or a **link** to an all-collections page? Proposed: a menu fed by enabled collections from Catalog.
