# 0018 — Storefront module, rebuildable read model, search, images and the edge

- Status: Accepted (decisions P-1..P-5, B-1, D-1..D-7 approved by the owner 2026-10-16; the plan and the technology choices marked **(choice)** approved 2026-10-25)
- Date: 2026-10-16

## Context

Sprint 9 puts the catalogue in front of anonymous visitors. ADR 0002 forbids reading another module's tables; the BRD (§12.6) wants listings and search served from a denormalised read model; SEO-01 needs full HTML on every request; stock and prices change after build. The product is small (hundreds of products, thousands of variants) and runs on one VPS behind Cloudflare (ADR 0003, 0017).

## Decision

1. **A new module `Storefront`** (schema `STFR`; `Domain`, `Application`, `Infrastructure`; no `Contracts` project because no other module consumes it). It owns only a read model. It never writes to Catalog, Warehouse or Reference and never reads their tables.
2. **The read model is a projection that can always be rebuilt from contracts.** Producers expose a paged **snapshot query** in their `*.Contracts` (Catalog: `IPublishedProductQueries`; Warehouse: `IStockAvailabilityQueries`; Reference: list-all lookup queries). Each snapshot carries a **monotonic version** (Catalog: a content version bumped in the same transaction as any storefront-relevant change; Warehouse: the ledger sequence of the variant's last entry). Events remain triggers that name what changed (ADR 0011: ids, no content); the consumer fetches the **full snapshot** through the contract and applies it only if its version is **higher** than the stored one. Lost, duplicated and reordered events are therefore harmless, and a snapshot of a product that is no longer published removes it.
3. **Three ways the projection is repaired:** the event consumers (normal path, seconds); a **reconciliation job every 10 minutes** that compares (id, version) lists from the contracts with the read model and refetches differences (self-heals lost events); and a **rebuild command** (`rebuild-storefront`, an operator mode of the host like `migrate`, also run automatically when the read-model schema version changes) that pages through the contracts, upserts, stamps each row with the run id and then sweeps rows not seen. Old rows keep serving until the sweep, so a rebuild has no downtime.
4. **Rendering and the edge.** The Angular storefront renders per request (SSR, `RenderMode.Server`). The browser talks only to the SSR server (page routes plus a few of its own: `/sitemap.xml`, `/robots.txt`, `/api/suggest`); the .NET API is reachable only from the SSR server on the internal network. Anonymous HTML responses carry `Cache-Control: public, s-maxage=60` (configurable) and no cookies, so a Cloudflare cache rule may hold them for about a minute. The API sets no per-IP limits (every call arrives from the SSR server).
5. **Real client IP and rate limits.** Cloudflare rate-limiting rules are the first line. nginx (ADR 0017) restores the client address from `CF-Connecting-IP` only for connections from Cloudflare's published ranges (realip module) and passes it to the SSR server as one header; the SSR server trusts that header only from the proxy's address and limits search and suggestions per client in its own middleware (small token bucket, no new dependency; Redis-backed when a second SSR node exists, S19). Anything else uses the socket address.
6. **Search is PostgreSQL** full-text (`simple` configuration, weighted: name, then category/collection/colour, then description) plus `pg_trgm` for slips, inside the read model, with synonym groups from a data table expanded at query time. Replace it with a search engine when any of: more than ~10,000 products, p95 search above 400 ms in the load test (S18), or a need for relevance tuning that SQL cannot express. As built, a query ranks at most 200 matches and sorts and pages them in memory, which is another reason to move once the catalogue passes the size above.
7. **Images:** derivatives are made by a **background worker** (rows in a table acting as the queue, `FOR UPDATE SKIP LOCKED`, retry with backoff), never in a request. **WebP with a JPEG fallback**, widths from configuration, **content-hashed names** under `/media/…` served `public, max-age=31536000, immutable`. A listing shows a product only when its primary image has every derivative. **(choice)** The encoder is **SkiaSharp** (MIT) behind an `IImageResizer` port; alternatives are NetVips (libvips, LGPL native library) and ImageSharp (Six Labors Split License, paid for most commercial use). Metadata is stripped, EXIF orientation applied, decode size capped (pixel count) against decompression bombs.
8. **Brand and host are configuration** (B-1): `Brand:*` (name, tagline, logo, site name) and `ROMP_PUBLIC_HOST`/`Site:PublicBaseUrl` are read by the SSR server only; the API is host- and brand-agnostic. The API returns data; the SSR layer builds titles, tags, JSON-LD, the sitemap XML and robots.txt.
9. **Staging protection:** the staging storefront sits behind Cloudflare Access (preferred) or nginx basic auth, in addition to `noindex`; health paths and the CI probe are exceptions.
10. **Performance gate:** Lighthouse CI on home, one category and one product against a seeded fixture on the Linux runner; budgets per CLAUDE.md; total blocking time stands in for INP in the lab.

## Alternatives considered

- **Query Catalog and Warehouse directly per request** (ADR 0002 forbids cross-module reads; joins on every request misses NFR-02).
- **Events as the only source** (loses data when an event is lost or changes shape; rejected by the owner).
- **Elasticsearch or Meilisearch now:** a second stateful service on one VPS for a few hundred products; revisit at the triggers above.
- **Prerender at build time:** stale stock/price and no new-product pages without a deploy.
- **Per-IP limits inside the API:** meaningless behind the SSR server (owner decision P-4).
- **Resize on first request / an open resize endpoint:** slow first view and a denial-of-service surface (owner decision P-5).
- **AVIF in the first release:** much slower encoding for a small catalogue; can be enabled by configuration later.

## Consequences

- Catalog and Warehouse gain small additive contract changes in Sprint 9 (snapshot queries, version fields, list-all lookups); no existing consumer changes.
- The read model denormalises on purpose (names, codes, prices, availability on the product and variant rows) and is the only place that does; transactional tables stay 3NF.
- Availability and price on a page can be up to about a minute stale (edge cache plus event delay); the cart (S11) is authoritative.
- The deploy adds a storefront container and an nginx server block for the bare domain (ADR 0017).
- A native image library enters the API image; it cannot be loaded on the owner's machine under Windows Application Control, so its tests run in the Linux container and CI.
