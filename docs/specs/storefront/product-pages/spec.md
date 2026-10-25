# SCRUM-27: Product pages

- **Status:** Approved (owner, 2026-10-16; open items below carry their proposed default)
- **Jira:** SCRUM-27 (FR-07); size chart, composition and care display come from SCRUM-28/29 (shipped in Sprint 7); umbrella SCRUM-203
- **BRD sections:** §4.2 FEAT-PDP-01..04, §5.2 (without Fit Finder), §7.2
- **Requirement IDs:** FR-07, FR-08, FR-09, FR-10 (display of availability only), SEO-03, SEO-07, NFR-01, NFR-19

## Problem & intent
This is the page where a parent decides. It must show the garment clearly from several angles, say which sizes and colours exist and which are available, state the price honestly, and give the size chart and fabric care a parent needs to avoid a wrong-size return.

## Requirements (quoted from the BRD)
> **FR-07:** Each product page shall display a multi-image gallery, price, SKU, and available variants.

> **FR-08:** Each product shall expose a size chart mapping size labels to age range and/or measurements.

> **FR-09:** Each product shall display composition and wash-care information.

> **FEAT-PDP-04 Variant selector w/ live stock:** Colour/size selection reflecting real-time stock.

> **§5.2 (extract):** User selects colour → gallery/price update → selects size → stock check at exact SKU+variant. In stock → Add to Cart. Out of stock → 'Notify Me' capture.

## User flow
1. The visitor reaches `/{gender}/{category}/{slug}` (the Catalog path; case and trailing slash are normalised with a 301, old slugs 301 to the current one, an archived product answers 410).
2. The page shows the breadcrumb, a gallery with thumbnails, the name, price (with compare-at price and saving when on sale), the SKU of the selected colour and size, colour swatches, size buttons, availability, the description, the size chart, composition and care.
3. Choosing a colour switches the gallery to that colour's images and updates price and SKU; sizes show as available, low stock or sold out for that colour.
4. In S9 the purchase area explains availability; the Add to Cart and Notify Me controls arrive in S10/S11 (Q-PDP-1).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| PDP-1 | Given a published product, then the page shows a gallery of its images, name, price, SKU and its variants | FR-07 |
| PDP-2 | Given a colour is selected, then the gallery and price match that colour (variant images override product-level ones) and the choice is reflected in the URL's query only as a non-indexed hint (Q-PDP-2) | TC-PDP-02 |
| PDP-3 | Given a size is selected, then the SKU of that colour and size is shown | FR-07 |
| PDP-4 | Given a size with no stock, then it is shown as unavailable (struck through with text "Sold out", not colour alone) and cannot be chosen | TC-PDP-04 (display part) |
| PDP-5 | Given a variant that Labelling has not yet issued a GTIN for, then it is listed as "not yet available" | Catalog D1 |
| PDP-6 | Given a size chart exists for the category and gender, then a table (with `th scope`) shows each size with its age range and measurements; the product's own size is highlighted | TC-PDP-01, FR-08 |
| PDP-7 | Given no size chart exists, then the page says so and still works | SCRUM-28 AC-4 |
| PDP-8 | Given composition and care data, then fibre percentages and care instructions with their symbols and text are shown in the server HTML | FR-09, FEAT-PDP-03 |
| PDP-9 | Given a product on sale, then the compare-at price is shown struck through with the selling price, never a "was" price the server did not provide | SEC-09, Catalog D4 |
| PDP-10 | Given the gallery, then the main image is loaded eagerly with `fetchpriority="high"`, others are lazy, every image has its alt text, and width and height are explicit | CLAUDE.md, SEO-07 |
| PDP-11 | Given the page, then the gallery, swatches and size choice work with the keyboard and have labels, and information is never conveyed by colour alone | NFR-19 |
| PDP-12 | Given the page loads on a mid-range phone over 4G, then LCP is at most 2.5 s | NFR-01 |
| PDP-13 | Given a path that does not exist, then the page answers 404; a path of an unpublished product also answers 404; an archived product's answers 410; an old slug answers 301 in one hop | CLAUDE.md status codes, ADR 0013 |
| PDP-14 | Given the page, then the structured data (Product, Offer with `availability`, BreadcrumbList) matches what is visible | SEO-03 |
| PDP-16 | Given a product with no in-stock variant, then its page stays live with HTTP 200, shows a clear "Sold out" state, no purchase controls, and its structured data `Offer.availability` is `OutOfStock`; it is not removed from the sitemap; given some sizes sold out, the page is `InStock` with those sizes greyed out | D-2, SEO-03 |
| PDP-17 | Given availability on the page, then it may be a few seconds old and is worded as availability, never as "reserved for you" | P-1 |
| PDP-15 | Given a product whose price differs by size, then the price shown is that of the selected variant, and before a choice "From PKR x" (lowest available) | FR-07 `(new)` |

## Non-functional constraints
NFR-01, NFR-19, SEC-09 (every displayed amount comes from the server read model, none from the client), no personal data on the page, cache with per-path invalidation.

## Out of scope
Add to cart and live stock check at add (S11, FR-10 proper); Notify Me (FR-11, needs customer data, arrives with S10/S12 as agreed); wishlist (S10); Fit Finder and recommendation (descoped); try-on (descoped); reviews and ratings (P2); share buttons beyond the Open Graph tags.

## Open questions
- [ ] **Q-PDP-1 What stands in for Add to Cart in S9?** Proposed: the purchase area shows availability only, and the Add to Cart and Notify Me controls arrive with S11 and S10/S12. A dead "Add to cart" button would mislead, and S9 runs on staging only. Confirm, or ask for a visible placeholder.
- [ ] **Q-PDP-2 Colour and size in the URL.** One canonical URL per product; the selected colour may travel as `?colour=navy` for sharing but canonicalises to the product URL (SEO-06). Confirm.
- [ ] **Q-PDP-3 Care icon artwork (Catalog Q-CMP-2).** The ISO 3758 symbols' licence was left to S9. Proposed: text always shown, with icons from a licensed or self-drawn set only after the owner confirms the source; until then the symbol is a text label.
- [ ] **Q-PDP-4 Size chart unit.** Centimetres only (Catalog Q-CHT-3); highlight the chosen size. Confirm no inches toggle.
- [x] **Q-PDP-5 (decided, D-5)** The cross-sell rail and the provenance badge are backlog, not Sprint 9.

## As built (Sprint 9b)

Tests that prove each criterion: [traceability.md](traceability.md). The open questions were built to their proposed defaults and are still awaiting the owner. What differs from, or adds to, the text above:

- **API:** `GET /api/storefront/products/by-path?path=` answers 200 for every outcome (`Found`, `Moved`, `Gone`, `NotFound`) so the storefront server can choose the real status; a `NotFound` answer is `no-store`; a path over 200 characters is a 400. A product page costs 7 to 8 queries, not 5 (selection, variants, images, size chart, composition and care, availability), and the count is fixed whatever the number of variants.
- **Selection:** colour and size are plain links (`?colour=&size=`), not radio groups, so they work without JavaScript and update in place with it. The selection lives in the URL and the server's answer, not in store fields. The colour is shown by name because the reference data has no swatch. A sold-out size is not a link and says so in words.
- **Codes in the URL** are the lower-case, hyphenated form of the lookup code (`1-2y`, `wht`); matching uses `CodeSegment.Of`, as in listings.
- **Gallery:** one scroll-snap strip on a phone and a two-column grid from 64rem; no thumbnails. The first picture is eager with `fetchpriority="high"`.
- **Status codes:** the route matches any three-segment address and the container sets 200, 301, 404, 410 or 503 on the server response.
- **Layout fix found by the e2e run:** the product grids needed `minmax(0, 1fr)` columns, otherwise the gallery strip stretched the page to twice the width at 320px.
- **Not built here:** Product JSON-LD with `Offer.availability` (PDP-14 moves to `seo`). The old mock product page, the product card and the product part of the mock catalogue are removed; this closes published-catalog task 23.
