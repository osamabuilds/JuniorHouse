# SCRUM-148: SEO baseline and performance budgets

- **Status:** Approved (owner, 2026-10-16; open items below carry their proposed default)
- **Jira:** SCRUM-148 (SEO-01), SCRUM-150 (SEO-03), SCRUM-151 (SEO-04), SCRUM-152 (SEO-05), SCRUM-153 (SEO-06), SCRUM-155 (SEO-08), SCRUM-157 (SEO-10); umbrella SCRUM-203
- **BRD sections:** §14.1, §14.3, §9.2
- **Requirement IDs:** SEO-01, SEO-03, SEO-04, SEO-05, SEO-06, SEO-08, SEO-10, NFR-01 (SEO-02 and SEO-07 are done in Catalog; SEO-09 is S18, SEO-11 is S19)

## Problem & intent
Nothing technical may limit how well Romp ranks, and links shared on WhatsApp must preview well. Search engines and chat apps read the HTML the server returns, so everything they need (content, links, tags, structured data) must be in it, with truthful status codes.

## Requirements (quoted from the BRD)
> **SEO-01:** All customer-facing pages (home, category, PDP, content/blog) shall be server-side rendered or pre-rendered, per the Angular Universal decision in Section 9.2 — this is the single largest technical SEO factor and is already architecturally planned, not an add-on.

> **SEO-03:** Every product and category page shall emit Schema.org structured data (Product, Offer, BreadcrumbList; AggregateRating once reviews exist per FEAT-PDP-11) to enable rich search snippets (price, stock status, rating stars).

> **SEO-04:** Meta title and meta description shall be generated from a per-page-type template (home/category/PDP) with product/category name interpolated in, rather than requiring a hand-written entry per SKU — see 14.3 for the actual copy templates.

> **SEO-05:** An XML sitemap shall be auto-generated and kept current as products are published/unpublished, and submitted to Google Search Console and Bing Webmaster Tools; robots.txt shall be explicitly configured (disallowing cart/checkout/account paths, allowing catalogue paths).

> **SEO-06:** Canonical tags shall be set on all filter/sort/pagination URL variants to point at the primary category URL, preventing duplicate-content dilution from FEAT-CAT-04's filter combinations.

> **SEO-08:** Open Graph and Twitter Card meta tags shall be present on all product/category pages, given the high rate of link-sharing over WhatsApp in Romp's target market — a broken/missing link preview measurably hurts click-through from shared links.

> **SEO-10:** Core Web Vitals targets (already set as NFR-01/NFR-17) shall be treated as an SEO requirement, not only a UX one — page speed and mobile usability are direct Google ranking factors.

> **CLAUDE.md (budgets):** Lighthouse CI on the home, category and product pages: SEO score 100, Accessibility ≥ 95, Performance ≥ 90 on mobile, LCP ≤ 2.5s, CLS ≤ 0.1, INP ≤ 200ms. A drop below any budget fails the build.

## User flow
No visitor flow: this feature is the shared machinery the pages use. A page asks for its meta, canonical, Open Graph and structured data from one place; the server returns the HTML with them, the right status code, and redirects where a URL moved.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| SEOX-1 | Given any public route fetched with scripting disabled, then the HTML holds the full content, links, meta tags and structured data | SEO-01 |
| SEOX-2 | Given a product, category, collection, search or home route, then the status is real: 200, 301 (moved, case, trailing slash), 404 (unknown or unpublished), 410 (archived), never a "not found" page with 200 | CLAUDE.md, ADR 0013 |
| SEOX-3 | Given a product page, then JSON-LD contains `Product` (name, description, image, sku, brand, material when known) with `Offer` (price in PKR, `availability`, `url`) and a `BreadcrumbList`, valid in Google's Rich Results Test | SEO-03 |
| SEOX-4 | Given the site, then `Organization` and `WebSite` (with `SearchAction`) JSON-LD are on every page; `BreadcrumbList` on every page below the home page; there is no `AggregateRating` | SEO-03, CLAUDE.md |
| SEOX-5 | Given a filter, sort or pagination variant, then the canonical follows the rule decided in Q-SEO-2 and the variant is excluded from the sitemap | SEO-06 |
| SEOX-6 | Given any page, then there is a unique `<title>` and meta description built from the §14.3 template for its type, with the name interpolated, truncated at word boundaries (title ≤ 60 chars, description ≤ 160 where possible) | SEO-04 |
| SEOX-7 | Given any page, then `<html lang="en">`, a viewport meta and a self-referencing canonical exist, with an absolute URL built from `Site:PublicBaseUrl` (never a hard-coded host) | CLAUDE.md, ADR 0017 |
| SEOX-8 | Given a product or category page, then Open Graph (`og:title`, `og:description`, `og:image` with width and height, `og:url`, `og:type`, `og:site_name`) and Twitter Card tags are present and the image URL is absolute and publicly reachable | SEO-08 |
| SEOX-9 | Given `/sitemap.xml`, then it lists every published product, category and collection page once (canonical URLs only), is split into index files above 50,000 URLs, and a publish or unpublish changes it within the freshness target | SEO-05 |
| SEOX-10 | Given `/robots.txt`, then cart, checkout, account, search and API paths are disallowed, the catalogue is allowed and the sitemap is declared; on staging it disallows everything | SEO-05, ADR 0017 |
| SEOX-11 | Given a staging response, then every page has `X-Robots-Tag: noindex, nofollow` and a `robots` meta; given the admin app, the same | ADR 0017, CLAUDE.md |
| SEOX-16 | Given the brand configuration (name, tagline, logo, site name), then every title template, Open Graph `site_name`, JSON-LD `Organization` and the header read it; a test changes the name and asserts that no page still contains the old one | B-1 |
| SEOX-17 | Given the staging environment, then the storefront sits behind Cloudflare Access or basic auth, so unreleased products are not reachable without credentials, in addition to `noindex`; the health paths and the CI probe are the only exceptions | D-4 |
| SEOX-18 | Given a test configuration set to a production host name, then robots.txt, sitemap.xml and every canonical, Open Graph URL and JSON-LD URL render for that host (no `noindex`), and a CI test asserts them; the production configuration itself is never used on staging | D-4 |
| SEOX-19 | Given an anonymous HTML response, then it carries `Cache-Control: public, s-maxage=60` (value from configuration) so Cloudflare may cache it for about a minute, never `Set-Cookie`, and error, redirect and `noindex` pages follow the same rule only when safe (404/410 short, 5xx never cached); a response that varies by anything but the URL is not cached | P-2 |
| SEOX-20 | Given a request through Cloudflare, then the client address is taken from `CF-Connecting-IP` only when the connection provably came from a Cloudflare range (checked at the reverse proxy; the SSR layer trusts only the proxy's single header), and otherwise from the socket; spoofed forwarded headers from any other source are ignored | P-4 |
| SEOX-12 | Given the home, a category and a product page on mobile in CI, then Lighthouse scores SEO 100, Accessibility ≥ 95, Performance ≥ 90 with LCP ≤ 2.5 s, CLS ≤ 0.1 and INP ≤ 200 ms (interaction proxy, Q-SEO-5); a miss fails the build | SEO-10, NFR-01 |
| SEOX-13 | Given each page, then there is exactly one `<main>`, exactly one `<h1>`, no skipped heading levels, and every image has alt text | CLAUDE.md, SEO-07 |
| SEOX-14 | Given any text interpolated into a tag or JSON-LD (names, descriptions), then it is encoded for its context so content cannot break out of the markup | secure-coding baseline `(new)` |
| SEOX-15 | Given a category with fewer than the minimum useful content, or an empty listing, then the page carries `noindex` | FR-05 `(new)` |

## Non-functional constraints
SEC (encoding of interpolated text), NFR-01, NFR-17, NFR-19. The sitemap and canonical use configuration for the host, so changing the domain is a deploy change. Performance budgets are checked against a seeded fixture database in CI (not production data).

## Out of scope
Search Console and Bing submission (SEO-11, S19), Merchant Center feed (SEO-09, S18), `AggregateRating` (reviews are P2), blog and guide pages and their `Article` data, hreflang (single language), AMP.

## Open questions
- [x] **Q-SEO-1 (decided, D-3)** "Size Finder" is replaced by "size guide" in every template (category: "…the right fit, every time, with our size guide."; product: "Find the right size with our size guide."), the rest of each template stays verbatim, and BRD §14.3 is logged on SCRUM-168 (its item 7 already covers the wording). The templates also say "Romp"; that word comes from the brand configuration (B-1).
- [ ] **Q-SEO-2 Canonical for pagination (BRD vs common practice).** SEO-06 points every filter, sort *and pagination* variant at the primary category URL. A paginated page 2 with a canonical to page 1 can cause products found only on later pages to lose a crawl path. Proposed (a): filter and sort variants canonical to the base listing; each paginated page canonical to itself with a link to page 1 and next/previous links; `noindex` not used. Option (b): follow SEO-06 literally for pagination too and rely on the sitemap and product links for discovery. I will not pick for you.
- [ ] **Q-SEO-3 Brand and image fields for structured data.** `Product.brand` ("Romp"), `Product.sku`, `gtin13` and `material` can be filled from Catalog and Labelling. Publish `gtin13`? (Proposed: yes, GTINs are issued by Romp's own prefix; confirm once the GS1 prefix has arrived, otherwise omit.)
- [ ] **Q-SEO-4 Product description in meta.** The PDP template says "{Product Name} — {key fabric/feature}". Proposed: key fabric = the composition row with the highest percentage, key feature = the first sentence of the description if no composition. Confirm.
- [ ] **Q-SEO-5 INP in CI.** Lighthouse measures lab responsiveness (Total Blocking Time), not INP. Proposed: gate on TBT ≤ 200 ms and on LCP/CLS, and read real INP from field data after launch (S18). Confirm.
- [x] **Q-SF-5 (decided, D-4)** S9 acceptance is correct output on staging; Search Console and Bing submission are S19 (SEO-11).
- [ ] **Q-SEO-6 Which Lighthouse run mode.** Proposed: a CI job starting the storefront and API against seeded Testcontainers data, three runs per page, median must pass. This needs the API test image to run on the Linux runner only (machine policy, CLAUDE.md).
- [ ] **Q-SEO-7 Collection and Sale titles.** §14.3 gives "Sale/Collection" one template: `{Collection Name} Sale | Up to {X}% Off Kids Clothing — Romp`. For a collection that is not a sale (Essentials), "Sale" and a discount are false. Proposed: use that template only for sale views; other collections use the category template with the collection name. Confirm.
