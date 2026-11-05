# Collections page (`/collections`)

- **Status:** Approved by the owner on 2026-11-05 (recommendations accepted for all open questions); plan and tasks written
- **Jira:** none yet
- **BRD sections:** §14 (SEO), navigation (FR-01); design source `docs/design-system/components/CollectionsPage/README.md`
- **Requirement IDs:** FR-01, NFR-01, NFR-17, NFR-19, SEO-03, SEO-04

## Problem & intent

The header's Collections entry and the footer's "Collections" link point at `/collections`, which returns 404 today because only `/collections/:code` exists (design audit, finding D2). Visitors need an index of the collections they can open. The design system already defines the page.

## Requirements (from the design system, not the BRD)

> **CollectionsPage:** "The index of collections, reached from the header's Collections entry. Collections are data (a lookup), so the page lists whatever is enabled in the settings." Layout: breadcrumb, `display-lg` "Collections", a one-line lead, a two-up grid of `rp-tile`s (image, scrim, eyebrow, `display-md` name, "[N] pieces"); tiles stack on a phone; one link per tile; a collection opens `/collections/<code>`.
>
> "Only collections with at least one live product are listed; an enabled collection with none shows the listing empty state; the page is indexable with a unique title and description, `BreadcrumbList` JSON-LD and a canonical (SEO-03, SEO-04)."

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| COL-1 | Given enabled collections with live products, when `/collections` is requested, then the server HTML lists one tile link per collection to `/collections/<code>`, with its name and piece count | design |
| COL-2 | Given a collection with no live product, then it is not listed on `/collections`, and its own `/collections/<code>` page still returns 200 with the listing empty state | design, ST-5 |
| COL-3 | Given a collection that is disabled in the settings, then it is neither listed nor reachable | design (new: stated explicitly) |
| COL-4 | Given the page, then it has one `<h1>` "Collections", a breadcrumb, the tiles in a `<ul>`, product counts as text, and headings that do not skip a level | CLAUDE.md |
| COL-5 | Given the page, then it has a unique title and description, a self-referencing canonical, Open Graph tags and BreadcrumbList JSON-LD, and is indexable | SEO-03, SEO-04 |
| COL-6 | Given no collection qualifies, then the page returns 200 with a short message and a link to New in (not a 404) | (new) |
| COL-7 | Given 390, 768 and 1280 px, then tiles stack on a phone and sit two across from 40rem, with no horizontal scroll, 44px link targets and no axe violations | NFR-17, NFR-19 |
| COL-8 | Given the header, footer or mobile menu link to `/collections`, then the link opens this page (no 404) | (new, the reason for the feature) |

## Non-functional constraints

NFR-01: server-rendered, CLS 0.1 or less (images with width and height). If the page gets its own result slice, it joins the whitelist in ADR 0028 (decision in the plan). No customer data is read.

## Out of scope

Collection editing (admin, already exists), collection descriptions beyond what the listing page already shows, sorting options.

## Open questions

- [x] **Q-COL-1** Where do the piece counts and tile images come from? The menu API carries only code, label and path. Recommendation: a small paged endpoint `GET /api/storefront/collections` from the read model returning name, code, live-product count and the first product's image. **Resolved: recommendation accepted.**
- [x] **Q-COL-2** Order of the tiles: the settings order (recommended, the owner controls it) or alphabetical? **Resolved: recommendation accepted.**
- [x] **Q-COL-3** Until collection photography exists, use the product's first image (recommended) or the garment placeholder? **Resolved: recommendation accepted.**
