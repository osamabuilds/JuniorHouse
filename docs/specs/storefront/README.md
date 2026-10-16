# Storefront

The public, server-rendered shop window (Sprint 9, "Sell: browse"): navigation, listing pages, search, the product page and the SEO baseline. It shows what Catalog published and what Warehouse has on the shelf; it changes neither. Nothing here needs a customer account (accounts start in S10, cart in S11).

**Status:** Specs approved (owner, 2026-10-16); plans awaiting approval · **Schema:** `STFR` (read model only) · **Code:** `src/api/src/Modules/Storefront/` and `src/web/projects/storefront/` · **Web:** `features/storefront/` · **Jira:** SCRUM-203 (sources SCRUM-21..27, 148, 150..153, 155, 157) · **Rules:** [constitution](../../CONSTITUTION.md), [CLAUDE.md](../../../CLAUDE.md) frontend and SEO sections · **Module plan:** [plan.md](plan.md) · **ADR:** [0018](../../adr/0018-storefront-module-read-model-and-edge.md)

## Features

| Feature | What it does | Requirements | Status |
|---|---|---|---|
| [design-foundation](design-foundation/spec.md) | Story 1 of the storefront design: tokens, self-hosted fonts, global styles, `rp-image` (4:5, blur-up, priority hero), icons, buttons and fields | NFR-01, NFR-17, NFR-19, SEO-07, SEO-10 | Implemented |
| [published-catalog](published-catalog/spec.md) | The storefront's own rebuildable read model of published products, prices, stock availability and images, kept current from Catalog and Warehouse; image derivatives by background job | NFR-01, NFR-02, FR-10 (display), SEO-07 | Approved |
| [navigation](navigation/spec.md) | Audience and age-bracket mega-menu, breadcrumbs, minimal home page, brand configuration | FR-01, FEAT-CAT-01 | Approved |
| [listings](listings/spec.md) | New Arrivals, Sale, Category and Collection views; size, colour, price (and age) filters; sorting; pagination; empty states | FR-02, FR-03, FR-05, FEAT-CAT-02..04, 07 | Approved |
| [search](search/spec.md) | Keyword search, autosuggest, synonyms as data, zero-result log, safe input handling, exact SKU lookup | FR-04, FR-05, FR-06, FEAT-CAT-05, 06, NFR-02 | Approved |
| [product-pages](product-pages/spec.md) | Gallery, price, SKU, variants and availability, sold-out state, size chart, composition and care | FR-07, FR-08, FR-09, FEAT-PDP-01..04 | Approved |
| [seo](seo/spec.md) | Server rendering, real status codes and redirects, meta templates, canonicals, Open Graph, structured data, sitemap, robots, edge cache headers, staging protection, Lighthouse budgets in CI | SEO-01, 03, 04, 05, 06, 08, 10, NFR-01 | Approved |

Order of work: published-catalog → navigation and listings → product-pages → search → seo (the SEO pieces are built into each page as it is written; `seo` owns the shared machinery and the CI gate, starts first as a thin layer and closes last). **Code starts after Sprint 8 is merged to `main`; S9 branches from `main`.**

## Scope of Sprint 9

**In:** everything an anonymous visitor needs to find a product and read its page, with the SEO baseline and the performance budgets enforced in CI, deployed to staging next to the admin app ([ADR 0017](../../adr/0017-staging-topology-and-deploy-pipeline.md)).

**Out (later sprints or not built):** cart, add-to-cart and the live stock check at add (S11, FR-10 as a check); Notify Me (FR-11; the sold-out state is shown but has no form yet); wishlist (S10); accounts, Child Profiles and the age-personalised homepage (S10, FEAT-CAT-10 is P2); Fit Finder and Virtual Try-On (descoped); reviews and ratings (P2, so no `AggregateRating`); the Merchant Center feed (S18); blog and guide pages; editorial lookbook tiles (P2); **the cross-sell rail (FEAT-PDP-06) and the provenance badge (FEAT-PDP-08), which go to the backlog (Q-SF-6)**.

## Module decisions (owner, 2026-10-16)

| ID | Decision |
|---|---|
| P-1 | New backend module `Storefront`, read-model schema `STFR`. It consumes Catalog and Warehouse events and queries their `*.Contracts`; it never reads their tables. **The read model must be fully rebuildable from the Catalog and Warehouse contracts** (events can be lost or change shape). Events are full snapshots and the highest version wins. Availability on a product page may be a few seconds stale; the authoritative check is the cart's (S11). |
| P-2 | The Angular storefront renders per request (SSR, `RenderMode.Server`) and calls the API over the internal network. A short **edge cache of about 60 s** on anonymous HTML at Cloudflare is allowed, because seconds of stock and price staleness are acceptable (the cart re-checks). |
| P-3 | Search on PostgreSQL full-text plus trigram matching inside the read model. A **synonyms / spelling-variants table is data from day one** (Pakistani product terms and Roman Urdu variants such as frock, kurta, shalwar kameez). **Zero-result queries are logged** (query text only, no personal data). |
| P-4 | Public endpoints use an explicit anonymous policy with rate limiting and response caching. **Rate limiting uses the real client IP at Cloudflare and in the SSR layer** (forwarded headers trusted only from Cloudflare ranges). **No IP-based limits inside the API**, because every SSR-to-API call arrives from the SSR server's address. |
| P-5 | Image derivatives are generated by a **background job, never in a request**: WebP with a JPEG fallback, **content-hashed file names** for long-lived caching, widths in configuration. (This narrows CLAUDE.md's "WebP/AVIF" to WebP and JPEG for S9; AVIF can be added later by configuration.) |
| B-1 | **Brand configuration.** Brand name, tagline, logo and site name come from one configuration (meta titles, JSON-LD `Organization`, Open Graph `site_name`, header), because neither the brand name nor the domain is final. Renaming is a configuration change, never a code change. |
| D-1 | **Age in navigation (Q-SF-2).** The mega-menu shows audience, then age band, linking to the audience listing with age as a filter query parameter (consistent with Catalog D2). The filtered page's canonical is the audience page. No age-named indexable pages in S9. Age bands (proposed names: Toddler 1-3Y, Kids 4-8Y, Junior 9-12Y) are lookup data so names can change without breaking URLs. |
| D-2 | **Sold-out rule (Q-SF-3).** Listings and search exclude products with no in-stock variant. The product page stays live (200) with a Sold out state and `OutOfStock` structured data, and exact SKU lookup still reaches it. A product with some sizes sold out stays listed with those sizes greyed out. A category with nothing in stock shows the empty state with a link to New Arrivals. |
| D-3 | **Meta wording (Q-SF-4).** "Size Finder" is removed from every meta template and replaced with "size guide". BRD §14.3 is logged on the SCRUM-168 errata (item 7 already lists the wording). |
| D-4 | **Staging (Q-SF-5).** Sprint 9 acceptance is correct output on staging; Search Console submission is S19. Staging must sit behind Cloudflare Access or basic auth (`noindex` alone does not stop access to unreleased products). A test configuration renders robots, sitemap and canonicals for the production host so CI can assert them. |
| D-5 | **Backlog (Q-SF-6).** Cross-sell rail and provenance badge stay out of Sprint 9. |
| D-6 | **Home page (Q-SF-7).** Minimal: hero from New Arrivals, audience tiles, structured data and meta. Design direction: black and white base, premium, fashion-forward. The earlier marigold-gold mockup is not the reference. |
| D-7 | **Delivery (Q-SF-1).** Jira umbrella SCRUM-203. Sprint 7 and Sprint 8 are merged to `main` first (after green Linux CI) and S9 starts from `main`. Specs and plans can be written now; code starts after Sprint 8. |

## Open questions still carrying their proposed default

The owner's answers covered the module-level questions. The per-feature questions below were not answered; each spec lists them, and the plans follow the **proposed default** until you say otherwise. Two of them could change behaviour in a visible way, so please look at these first:

- [ ] **Q-SEO-2 Pagination canonical.** SEO-06 literally points paginated pages at page 1. Proposed default (a): filter and sort variants canonical to the base listing, each paginated page canonical to itself. The canonical rule is one function behind one setting, so switching to the literal reading is a one-line change.
- [ ] **Q-LST-2 What "Sale" means.** Proposed default: every in-stock product with a variant whose compare-at price is above its price.

All others (Q-NAV-2..5, Q-LST-3..7, Q-SRC-1..4, Q-PDP-1..4, Q-SEO-3..7, Q-PCT-1..4) are in their specs with a proposal; Q-SF-2/3 above resolved several of them (marked in each spec).

Back to the [spec index](../README.md).
