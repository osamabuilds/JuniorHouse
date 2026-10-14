# 0013 — Product URL convention, slugs and redirects

- Status: Accepted (Sprint 7, SCRUM-201; plan approved by the owner 2026-10-14)
- Date: 2026-10-14

## Context

SEO-02 requires clean, keyword-descriptive URLs and a documented convention fixed before any catalogue data is loaded. Changing a published URL costs search ranking, so the convention cannot be revised after launch. The owner decided (2026-10-14) that age is a filter, not a path segment, because the age bands are not settled. Gender (`GNDR_LKP`: Boys, Girls, Unisex) and category (`CATG_LKP`: six flat categories) are separate attributes of a product; there is no category tree under Boys and Girls.

## Decision

- **Pattern:** `/{gender}/{category}/{slug}`, all lowercase ASCII with hyphens, for example `/girls/dresses/floral-summer-dress`. Gender and category segments are the lowercase `CODE` of the lookup row; those codes are stable and are not renamed when a display name changes. No IDs, no query strings, no age segment.
- **Slug:** generated from the product name (ASCII-folded, lowercased, non-alphanumerics to a single hyphen, trimmed, at most 80 characters). Unique per gender + category. On collision a numeric suffix is appended (`-2`, `-3`) deterministically and never reused. Reserved words (`cart`, `checkout`, `account`, `admin`, `search`, `api`, `new-in`, `sale`) are refused.
- **History:** every (gender, category, slug) a product has ever had is kept in a history table that points at the product. Looking up an old triple finds the product directly, so a redirect chain is always one hop to the current path. An old triple can never be taken by another product.
- **Resolution result:** `Current` (200), `Moved` (301 with the new path), `Gone` (410, product archived for good) or `NotFound` (404). Normalising case and a trailing slash is a pure function used by the storefront (S9), which serves the actual redirects.
- **Where it lives:** the rules are a pure domain type in Catalog (`ProductPath`, `SlugPolicy`), unit tested; the storefront calls the Catalog contract (`IProductPathQueries`), it does not re-implement them.

## Consequences

- Moving a product to another category changes its path; the old path 301s. Renaming a category's display name does not.
- Unisex products have the single canonical path `/unisex/{category}/{slug}` (open question Q-URL-5 until confirmed).
- If the owner prefers a Boys/Girls category tree instead, the reference data and this ADR change before the first product is published, never after.
