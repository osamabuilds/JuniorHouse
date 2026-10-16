# SCRUM-148: SEO baseline and performance budgets: technical plan

- **Spec:** [spec.md](spec.md) · **Module plan:** [../plan.md](../plan.md) · **ADR:** [0018](../../../adr/0018-storefront-module-read-model-and-edge.md)
- Open questions at their proposed default: Q-SEO-2 (**the canonical rule is a single function behind a setting; default (a): filter/sort variants canonical to the base listing, paginated pages canonical to themselves with prev/next links**), Q-SEO-3 (`gtin13` published once the GS1 prefix is real; omitted while a test prefix is in use), Q-SEO-4 (key fabric = highest composition row, else first sentence of the description), Q-SEO-5 (TBT as the lab stand-in for INP), Q-SEO-6 (three Lighthouse runs per page, median), Q-SEO-7 (the sale template only for sale views).

## Domain model
All pure functions in `features/storefront/seo/utils`, unit tested:
- `MetaTemplates`: `home`, `category`, `product`, `sale`, `collection`, `search`, `notFound` → `{ title, description }` from the §14.3 templates with the **size guide** wording (D-3) and the brand name from configuration; interpolation values are plain text trimmed to word boundaries (title ≤ 60, description ≤ 160). Product age group = the age bracket name; key fabric per Q-SEO-4; "Up to {X}% Off" = the biggest saving in the view, omitted when none.
- `CanonicalRule`: `canonicalFor(route, filter, page)` → absolute URL on `Site:PublicBaseUrl`; query keys in fixed order; implements the Q-SEO-2 setting (`pagination: self | base`).
- `JsonLd`: builders for `Organization`, `WebSite` (+ `SearchAction` `/search?q={search_term_string}`), `Product` + `Offer`/`AggregateOffer` (price PKR, `availability` `InStock`/`OutOfStock`, `url`, `sku`, `brand`, `image[]`, `material`, `gtin13` per Q-SEO-3), `BreadcrumbList`; `serialise()` escapes `<`, `>`, `&`, U+2028 and U+2029 so text cannot close the script element (SEOX-14).
- `RobotsPolicy`: staging ⇒ disallow all and `noindex`; production host ⇒ disallow `/cart`, `/checkout`, `/account`, `/search`, `/api`, `/media/original`; declares the sitemap; empty or filtered listings and search pages ⇒ `noindex`.

## Application layer (API)
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `GetSitemapIndexQuery` | none | number of pages, last modified per page | SEOX-9 |
| `GetSitemapPageQuery` | page (≤ 45,000 URLs each) | entries: path, last modified (`PRDT_MAIN.UPDT_DTE`), for products (including sold-out ones, PDP-16), audience, category, collection and the static pages that have at least one listable product | SEOX-9 |

The API returns paths only; the SSR server prepends the host and writes the XML with an XML writer (no string concatenation of user text).

## API endpoints
| Method | Route | Auth | Notes |
|---|---|---|---|
| GET | `/api/storefront/sitemap` | `Storefront.Public` | index data |
| GET | `/api/storefront/sitemap/{page}` | `Storefront.Public` | entries |

SSR-server routes (Express, before Angular): `/sitemap.xml` (index when more than one page, else the single urlset), `/sitemap-{n}.xml`, `/robots.txt`, `/healthz`, `/api/suggest` (proxy to the search endpoint with the rate limiter). `sitemap` responses cache 5 minutes in the SSR process and are served with `Cache-Control: public, s-maxage=300`.

## Events
None. A publish or unpublish changes the read model within seconds, and the sitemap within 5 minutes (SEOX-9 freshness target proposed: 5 minutes).

## Data
None of its own.

## NFR / security design
- **Server rendering (SEOX-1, 2, 13):** every route uses `RenderMode.Server`; data is loaded in route resolvers before render and passed by TransferState; the app uses hydration with event replay. Status and headers are set through `RESPONSE_INIT`: 301 for moved and non-canonical paths or query strings, 404 for unknown or unpublished, 410 for archived, 200 for empty listings with `noindex`, 5xx never cached. A crawler test fetches each page type without JavaScript and checks content, links, tags and JSON-LD, one `<main>`, one `<h1>`, no heading skips and alt on every image.
- **Tags (SEOX-6..8):** a single `SeoService` applies title, description, canonical, robots, Open Graph (`og:title/description/image/image:width/image:height/url/type/site_name`) and Twitter `summary_large_image` for every page type; the OG image is the primary product image's 1280 WebP/JPEG derivative (`og:image` points at the JPEG for WhatsApp compatibility), absolute and public; category/home use a configured default image.
- **Edge cache (SEOX-19):** anonymous HTML gets `Cache-Control: public, s-maxage=EDGE_CACHE_SECONDS, max-age=0`; no `Set-Cookie` is ever emitted by the storefront in S9; 404/410 use `s-maxage=30`; 5xx and the staging protection challenge use `no-store`. The Cloudflare cache rule ("cache everything for the storefront host, respect origin") is documented in `deploy/` and checked by a smoke test (`cf-cache-status` appears on a second request).
- **Real client IP and limits (SEOX-20, P-4):** nginx uses `set_real_ip_from` with the Cloudflare ranges file and `real_ip_header CF-Connecting-IP`, and forwards the result as `X-Client-IP` after deleting any inbound value; the SSR server trusts `X-Client-IP` only when the socket address equals `TRUSTED_PROXY`; its token-bucket limiter keys on that address (search 30/min, suggest 120/min, pages 300/min per client, configurable). The Cloudflare range file is refreshed by a scheduled workflow that opens a PR when the published list changes.
- **Staging protection (SEOX-17):** Cloudflare Access policy on the staging storefront host (owner's email, one-time PIN) with a service token for the CI smoke test; fall-back nginx basic auth switch from an env file. The `NOINDEX` flag adds `X-Robots-Tag` and the robots meta (SEOX-11).
- **Test configuration (SEOX-18):** `ROMP_PUBLIC_HOST=shop.example.test`, `NOINDEX=false` in CI e2e; a Playwright test asserts robots.txt, sitemap, canonical, OG URL and JSON-LD URL all use that host. Start-up refuses `NOINDEX=true` together with a host listed as production in `deploy/production.hosts` (empty until S19) so a production deploy can't ship noindexed by mistake.
- **Performance budgets (SEOX-12):** `lighthouserc.json` in `src/web`: three runs per URL (home, one category, one product) on mobile emulation, assertions SEO = 1, accessibility ≥ 0.95, performance ≥ 0.9, LCP ≤ 2500 ms, CLS ≤ 0.1, TBT ≤ 200 ms; a GitHub Actions job on Linux starts the compose stack with a deterministic fixture (`Development` seeder: 40 products, 8 categories/collections, images with derivatives) and fails the build on any miss. The same fixture backs the e2e suite.
- **Branding (SEOX-16):** `BRAND_*` in the SSR environment load into an injection token at server start; a test swaps the brand and greps the rendered output of every page type.

## Frontend (`features/storefront/seo`)
`SeoService` (title, meta, canonical link, robots, OG, Twitter, JSON-LD script element via the renderer), `ResponseService` (status, redirect, headers), `BrandConfig`, utilities above, `components/breadcrumbs-json` is not needed (the breadcrumb component emits its trail to `SeoService`). The web barrel exposes only `SeoService`, `ResponseService`, `BRAND`.

## Risks & alternatives considered
Angular's Express engine must be configured to trust only the proxy; wrong trust settings would silently key the limiter on the proxy address, so a test sends a spoofed header from a non-proxy socket and expects it ignored. Rich Results validation is by Google's tool, which cannot run in CI: the plan adds a schema check against the Schema.org property lists (`Product`, `Offer`, `BreadcrumbList` required and recommended fields) and a manual Rich Results Test on staging before sign-off, recorded in the sprint's traceability.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | Public GET endpoints explicit; no admin surface; staging behind Cloudflare Access or basic auth |
| Input validation | Query strings parsed and rebuilt in canonical form; unknown keys dropped with a 301; `Host` header never used to build URLs (only `Site:PublicBaseUrl`) |
| Output encoding | JSON-LD escaped for script context; meta values through the `Meta` service; XML through an XML writer; robots text from a fixed template plus configured values |
| Outbound calls | SSR → API on the internal URL only (config); no third-party scripts, fonts or analytics |
| Resource limits | Sitemap pages ≤ 45,000 URLs, cached 5 min; rate limits per real client IP; response size caps |
| Concurrency | n/a (stateless); sitemap cache is a single-flight refresh |
| Idempotency | n/a (GET) |
| Audit | n/a |
| Data protection | No cookies, no analytics identifiers, no personal data in tags or JSON-LD |
| Secrets and dangerous configuration | `NOINDEX`/host/trusted-proxy validated at start-up with the production-host guard; the Access service token lives in GitHub environment secrets only |
| Error responses | Real 404/410/5xx pages with no internals; never a 200 "not found" (tested for every route type) |
| New dependencies | `@lhci/cli` (dev, Apache-2.0); none at runtime |
| Coupling | Reads data via the API contracts of its own module; templates and canonical logic are pure and shared by all pages |
| Abuse cases | Host-header injection into canonicals or the sitemap (host from config only); cache poisoning through unkeyed headers/query (unknown keys dropped before caching, `Vary` not used, one representation per URL); spoofed client IP to evade limits (proxy-only trust, tested); script-breaking product text in JSON-LD (escaping tested with `</script>`); accidental indexing of staging or noindex of production (two guards) |
