# SCRUM-201: URL convention and slugs

- **Status:** Approved (owner decisions 2026-10-14; open questions below use the stated defaults)
- **Jira:** SCRUM-201 (epic SCRUM-12); source tickets SCRUM-149, SCRUM-78
- **BRD sections:** §14.1
- **Requirement IDs:** SEO-02, SEO-05 (sitemap, S9), SEO-06 (canonicals, S9), NFR-01

## Problem & intent
URLs that read like words rank better and never change; changing them later costs search ranking. The convention must be written and approved before the first product is loaded, and slugs must be generated, kept unique and redirected when they change.

## Requirements (quoted from the BRD)
> **SEO-02:** URLs shall be clean and keyword-descriptive (e.g. /girls/dresses/floral-summer-dress) rather than parameter-based IDs, with a consistent, documented URL-naming convention set before catalogue data is migrated in.

> **CLAUDE.md (frontend rules):** Clean, keyword-descriptive, lowercase, hyphenated URLs, with no IDs or query strings on indexable pages. If a slug changes, the old URL gets a 301 redirect to the new one.

## User flow
1. When a product is created, a slug is suggested from its name and a distinguishing attribute. Staff may edit it before publishing.
2. The product's public path is `/{gender}/{category}/{slug}` (decision D2; age is a filter, not a path segment).
3. Changing the slug of a published product keeps the old one; the old path answers with a 301 to the current path in one hop.
4. The convention is also written into an ADR so the storefront (S9) follows it.

## Acceptance criteria
| ID | Given / When / Then | Source |
|---|---|---|
| URL-1 | Given a published product, then its path matches the documented pattern and is lowercase, hyphenated, ASCII | SCRUM-149 AC-1 |
| URL-2 | Given a slug change, then the old path resolves to a 301 pointing at the new path in one hop | SCRUM-149 AC-2, SCRUM-78 AC-7 |
| URL-3 | Given several slug changes, then the redirect chain collapses: every old slug points straight at the current one | SCRUM-149 (behaviour 3) |
| URL-4 | Given two products with the same name, then their slugs are unique and the suffix is deterministic and stable | SCRUM-149 AC-4 |
| URL-5 | Given a path with upper case or a trailing slash (`/Girls/Dresses/`), then it resolves to a 301 to the canonical form | SCRUM-149 AC-3 `(the check is a pure rule here; the redirect is served by the storefront in S9)` |
| URL-6 | Given a slug with spaces, symbols, non-ASCII letters or stop-word noise, then it is normalised or refused | SCRUM-149 (scope) |
| URL-7 | Given a reserved word (for example `cart`, `checkout`, `account`, `admin`, `search`), then it is refused as a slug | SCRUM-149 (scope) |
| URL-8 | Given a slug longer than the maximum length, then it is refused | SCRUM-149 (scope) |
| URL-9 | Given an unknown path, then the lookup reports "not found"; given an unpublished-for-good product, it reports "gone" (410 for the storefront) | CLAUDE.md status codes `(new)` |
| URL-10 | Given a canonical path, then no ID or query string appears in it | SEO-02 |
| URL-11 | Given an old slug that would collide with a new product's slug, then it is refused for the new product | `(new)` |

## Non-functional constraints
NFR-01 (the lookup is a single indexed read). Slug input sanitised (secure-coding baseline). Slugs and history in `CTLG` tables with unique constraints.

## Out of scope
Serving redirects, canonical and meta tags, sitemap and robots (S9, SEO-03..06), category and collection page URLs beyond the pattern, age-segment landing pages such as `/girls/6-14y` (S9).

## Decisions (owner, 2026-10-14)
- **D2 (was Q-URL-1)** Age is a filter attribute, never a path segment. The age brackets are not settled, and baking them into URLs would risk broken links and lost ranking if they change. The product path uses the audience (gender) and the category, matching the SEO-02 example `/girls/dresses/floral-summer-dress`.

**How the path is built (checked against the data):** gender and category are two separate attributes of a product (`GNDR_LKP`: Boys, Girls, Unisex; `CATG_LKP`: six flat categories). The category list is not a tree under Boys and Girls today. This spec therefore builds the path from the two attributes, `/{gender}/{category}/{slug}`, rather than splitting each category into a Boys and a Girls copy. That keeps one "Dresses" category and one size chart per category.

## Open questions
- [ ] **Q-URL-1 Confirm the gender segment.** The answer to this sprint's decision 2 reads `{category}/{slug}` and asks to confirm that the hierarchy includes the audience. It does as an attribute, not as a category tree (see above). Proposed: the path is `/{gender}/{category}/{slug}`. If you prefer a tree (Girls → Dresses as categories of their own), say so now: it changes the reference data and cannot be changed after launch.
- [ ] **Q-URL-5 Unisex products.** What is the path segment for a Unisex product: `unisex`, or both `boys` and `girls` listing the same canonical product? Proposed: `unisex`, one canonical URL per product.
- [ ] **Q-URL-2 Collection in the product URL?** SCRUM-149 Q2. Proposed: no, collections have their own pages.
- [ ] **Q-URL-3 Category rename.** If a category is renamed, every product path under it changes. Proposed: the category slug is a stable code that is not renamed with the display name.
- [ ] **Q-URL-4 Slug source.** SCRUM-149 says "name + distinguishing attribute". Colour is a variant, not the product. Proposed: the product name only, with a number suffix on collision.
- [ ] **Q-URL-6 Where the slug is unique.** Proposed: unique within gender + category, so `/girls/dresses/x` and `/boys/tops/x` can share a slug.
