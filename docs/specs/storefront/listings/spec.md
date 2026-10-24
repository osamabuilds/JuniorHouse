# SCRUM-22: Listings, filters and sorting

- **Status:** Approved (owner, 2026-10-16; open items below carry their proposed default)
- **Jira:** SCRUM-22 (FR-02), SCRUM-23 (FR-03), SCRUM-25 (FR-05, empty categories); umbrella SCRUM-203
- **BRD sections:** §4.1 FEAT-CAT-02, 03, 04, 07; §5.1; §14.1
- **Requirement IDs:** FR-02, FR-03, FR-05, SEO-06 (canonical on variants), NFR-01, NFR-02

## Problem & intent
A visitor narrows a catalogue to what fits a child and a budget. The pages must be fast, shareable, never broken when nothing matches, and must not create thousands of duplicate indexable URLs from filter combinations.

## Requirements (quoted from the BRD)
> **FR-02:** The system shall support New Arrivals, Sale, Category, and Collection sub-views per segment.

> **FR-03:** The system shall support filtering by size, colour, and price, with combined (AND) filter logic, and sorting by price and recency.

> **FR-05:** The system shall show an explicit, non-broken empty state for zero-result search/filter combinations and for categories with zero live products.

> **SEO-06:** Canonical tags shall be set on all filter/sort/pagination URL variants to point at the primary category URL, preventing duplicate-content dilution from FEAT-CAT-04's filter combinations.

## User flow
1. Audience → New In, Sale, a category or a collection opens a listing: a grid of product tiles (image, name, price, compare-at price if any, colour count, "sold out" when applicable), a result count, filters, sort and pagination.
2. Filters (size, colour, price range) combine with AND; sort is by price (low to high, high to low) or recency (newest first). Choices live in the query string, so a link can be shared and the back button works.
3. "Clear filters" removes every chip and restores the full listing.
4. When nothing matches, or a category has no live products, an explanatory empty state offers category suggestions and a clear way back.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| LST-1 | Given a size filter `5-6Y`, then only products with that size available are shown | TC-CAT-02 |
| LST-2 | Given size, colour and price range together, then every result satisfies all three | TC-CAT-03 |
| LST-3 | Given active filters, when "Clear filters" is chosen, then the full listing returns and no chips remain | TC-CAT-04 |
| LST-4 | Given a sort by price, when page 2 is opened, then the sort is kept and the order continues from page 1 | TC-CAT-05 |
| LST-5 | Given a category with no in-stock products, then a clear empty state is shown with a link to New Arrivals and category suggestions, HTTP 200 and `noindex` (a category that exists is not a 404) | TC-CAT-10, FR-05, D-2 |
| LST-15 | Given a product with no in-stock variant, then it is excluded from every listing; given a product with some sizes sold out, then it is listed, and its sold-out sizes are shown greyed out with the text "Sold out" wherever sizes appear | D-2 |
| LST-16 | Given an age filter (`?age=<code>`), then it combines with the other filters by AND, an unknown code is ignored, and the canonical is the audience page | D-1 |
| LST-6 | Given a filter combination with no results, then the same empty state is shown, naming the active filters with a link to clear each | FR-05, FEAT-CAT-07 |
| LST-7 | Given New In, then products appear newest first by publish date within the audience, for a window set in configuration (Q-LST-3) | FR-02 |
| LST-8 | Given Sale, then products with a compare-at price above the selling price appear, filterable like any listing | FR-02, FEAT-CAT-03, Catalog D4 |
| LST-9 | Given a collection, then products in that collection appear, with the collection's name as the heading | FR-02 |
| LST-10 | Given any filter, sort or pagination variant, then the page carries a canonical link following the rule in [seo](../seo/spec.md) SEOX-5 and the variant is not listed in the sitemap | SEO-06 |
| LST-11 | Given a malformed or hostile query string (unknown filter, negative price, huge page number, script text), then unknown parts are ignored or refused with 400/clamped, never an error page or reflected unescaped | TC-CAT-08 `(new, for filters)` |
| LST-12 | Given a listing, then exactly one `<h1>`, the grid is a list of `<article>` tiles in a `<ul>`, filter controls have labels and work without JavaScript as a GET form, and the product tile images are lazy-loaded except the first row | CLAUDE.md, NFR-19 |
| LST-13 | Given a listing request, then the filter and sort run in the read model with a constant query count and answer within 1 second under normal load | NFR-02 |
| LST-14 | Given a listing page past the last page, then the page answers 404 (or redirects to the last page; Q-LST-4), not an empty 200 | CLAUDE.md status codes `(new)` |

## Non-functional constraints
NFR-01 (LCP ≤ 2.5 s: first row of images prioritised, derivatives from `published-catalog`), NFR-02 (≤ 1 s filter/sort), CLS ≤ 0.1 (explicit image sizes), paged results (no unbounded list), CONVENTIONS.md (no N+1).

## Out of scope
Search (separate feature), infinite scroll, saved filters, wishlist hearts on tiles (S10), quick-add to cart (S11), the Fit-Finder-driven size pre-selection (descoped).

## Open questions
- [x] **Q-LST-1 (decided, D-2)** Products with no in-stock variant are excluded from listings; the page stays live; a size filter matches only in-stock sizes; partly sold-out products stay listed with those sizes greyed out.
- [ ] **Q-LST-2 What is "Sale"?** The Sale collection row (`COLN_LKP`) is curated by staff, while the compare-at price (Catalog D4) is a per-variant fact. Proposed: the Sale view is every product with at least one variant whose compare-at price is above its price, and the Sale collection is just a label staff can add for grouping. Confirm, or say the view should be the curated collection only. (§14.3's "Up to {X}% Off" is computed from the biggest discount in the view.)
- [ ] **Q-LST-3 New Arrivals window.** Newest first for how long: the last 30 days (proposed) or simply the newest 48 products regardless of date?
- [ ] **Q-LST-4 Page size and past-the-end.** Proposed 24 per page; a page past the end answers 404 (proposed) rather than redirecting.
- [ ] **Q-LST-5 Price filter shape.** Min and max boxes (proposed), or fixed bands (Under 1,500, 1,500-3,000, …)? Price is per variant; a product's filter price is its lowest available variant price (proposed).
- [ ] **Q-LST-6 Colour display.** Colour swatches need a hex value per colour; the reference colours hold names. Confirm that `COLR_LKP` has or may gain a swatch value, otherwise filters list names only.
- [ ] **Q-LST-8 (new) Listing URLs.** ADR 0013 fixes the product path only (`/{gender}/{category}/{slug}`) and reserves `new-in` and `sale`. Proposed listing paths, all lowercase and without IDs: `/{gender}` (audience), `/{gender}/{category}` (category), `/{gender}/new-in`, `/{gender}/sale`, `/new-in`, `/sale` (all audiences), `/collections/{code}` (collection; code = stable lookup code). Filters, sort and page are query parameters (`?size=5-6y&colour=navy&price=1000-3000&age=kids&sort=price-asc&page=2`). `collections` is added to the reserved slugs (it cannot clash with a gender segment, but staff should not be able to create it as a category code).
- [ ] **Q-LST-9 (new) What "matches" means across filters.** Proposed: within one filter several values mean OR (sizes 3-4Y or 5-6Y); across filters AND (TC-CAT-03); and size, colour and price must all hold on **the same in-stock variant**, so a product is not shown for "navy, 5-6Y" when navy exists only in 3-4Y. The product-level reading (each filter satisfied by any variant) is simpler but shows products that cannot be bought as filtered.
- [ ] **Q-LST-7 Pagination canonical (see also [seo](../seo/spec.md) Q-SEO-2).** SEO-06 says every pagination variant canonicalises to the primary category URL. Pages 2+ then tell search engines that the products on them are not worth indexing from there; they are still found through the sitemap and product links. Follow SEO-06 literally, or let pagination pages canonicalise to themselves and only filter/sort variants to the base?
