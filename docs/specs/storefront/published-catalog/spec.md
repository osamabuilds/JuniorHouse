# SCRUM-TBD: Published catalog (storefront read model)

- **Status:** Approved (owner, 2026-10-16; open items below carry their proposed default)
- **Jira:** SCRUM-203 (Sprint 9 umbrella)
- **BRD sections:** §12.6 (read model), §14.1, §5.1
- **Requirement IDs:** NFR-01, NFR-02, FR-10 (display only), SEO-07

## Problem & intent
Listings, search and product pages must answer fast and must not join Catalog, Warehouse and Reference tables on every request, which module rules forbid anyway. The storefront needs its own copy of what is for sale (products, variants, prices, images, availability), updated when Catalog or Warehouse change, and images sized for phones on 4G.

## Requirements (quoted from the BRD)
> **NFR-01:** Listing and PDP pages shall achieve largest-contentful-paint within 2.5s on 4G for 90% of requests.

> **NFR-02:** Search/filter results shall return within 1 second under normal load.

> **BRD §12.6 (read model):** Catalog listing/search reads are served from a denormalized read-model (Redis for hot product/PDP data at MVP; Elasticsearch/Azure Cognitive Search once the catalog and query complexity grow, per FEAT-CAT and NFR-01/02) rather than joining Products/ProductVariants/Categories/Images live on every request.

> **SEO-07:** The ProductImages.AltText column (Section 12.3.2) shall be a required, non-empty field at publish time, not optional — every product image needs descriptive alt text.

## User flow
1. Staff publish, change, unpublish or archive a product (Sprint 7). Catalog emits `ProductPublishedEvent`, `ProductChangedEvent`, `ProductUnpublishedEvent`, `ProductArchivedEvent`, `SizeChartChangedEvent`.
2. Warehouse emits stock events (`StockLevelChangedEvent`, `StockPutawayConfirmedEvent`). Labelling facts arrive through Catalog's variant data (ADR 0014).
3. The storefront consumes each event, reads the details through the owning module's contract, and updates its read model and the cached pages for the old and new path.
4. Image derivatives are produced for a new or changed image before the product shows it.
5. A visitor request is answered from the read model only.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| PCT-1 | Given a product is published, then it appears in the read model within 5 seconds (target; measured in the integration test) with name, description, path, gender, category, variants (colour, size, SKU, price, compare-at price), images with alt text and composition and care | SEO-07, FEAT-PDP-01 |
| PCT-2 | Given a product is unpublished or archived, then it leaves listings, search and the sitemap and its page answers 404 (unpublished) or 410 (archived) | CLAUDE.md status codes |
| PCT-3 | Given a variant's available quantity changes, then the product's availability flags per variant update, and a size reaching zero shows as unavailable | ALR-2 (Warehouse spec) |
| PCT-4 | Given a price or compare-at price change, then listings and the page show the new price and no cached copy of the old one survives past the invalidation | SEC-09 (display follows the server value) |
| PCT-5 | Given the same event is delivered twice, out of order, or an event is lost, then events are full snapshots, the highest version wins, and the read model ends in the same state | P-1, ADR 0004 |
| PCT-6 | Given the read model is empty, lost or stale, then a rebuild from the Catalog and Warehouse contracts alone (no event history) restores it, without downtime to Catalog and while the old rows keep serving until the rebuild completes | P-1 |
| PCT-7 | Given an image, then WebP derivatives with a JPEG fallback exist at the configured widths, produced by a background job and never inside a request; file names carry a content hash so they can be cached for a long time; the product is not shown with a missing derivative (it waits, or falls back to the next size); the original is never served to visitors | P-5, NFR-01, SEO-07 |
| PCT-10 | Given a changed image, then its new derivatives get new hashed names and the old ones are removed after the cache lifetime | P-5 `(new)` |
| PCT-11 | Given a product's stock changed a few seconds ago, then the page may still show the previous availability; nothing on the storefront presents availability as a reservation | P-1 (authoritative check is the cart, S11) |
| PCT-8 | Given a listing or product request, then the read model answers with a constant number of queries (no N+1) | CONVENTIONS.md |
| PCT-9 | Given a published product with no stock event ever received, then it shows as "not yet available" rather than failing | Catalog D1 `(new)` |

## Non-functional constraints
NFR-01 (data access is a few indexed reads), NFR-02 (filter/search under 1 s), ADR 0011 baseline (no personal data in this module; events carry ids), cache invalidation per path on `ProductChangedEvent`. Read-model tables follow `docs/db/naming.md` in schema `STFR`; the module is denormalised on purpose and the plan records why (CLAUDE.md database rules).

## Out of scope
Any write to Catalog or Warehouse; reservation of stock (S12); reviews; the Merchant Center feed (S18); personalised ordering.

## Open questions
- [x] **Q-PCT-1 (decided, P-5)** Formats WebP + JPEG fallback, widths in configuration (proposed default 320, 640, 960, 1280 px), background job, hashed names. The generating tool (libvips-based container tool, or a licensed library) is chosen in the plan.
- [ ] **Q-PCT-2 How exact is availability on a public page?** Proposed default: in stock / low stock (threshold from configuration, default 5) / sold out; the exact quantity is never published.
- [x] **Q-PCT-3 (decided, P-1)** A few seconds of staleness is acceptable; 5 s is the target, not a promise.
- [ ] **Q-PCT-4 Redis or Postgres for the read model.** Proposed default: Postgres tables are the read model (searchable, rebuildable); Redis only caches rendered fragments and counts.
