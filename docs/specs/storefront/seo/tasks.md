# Tasks: seo (Sprint 9c)

Jira: SCRUM-148 (SEO-01), SCRUM-150 (SEO-03), SCRUM-151 (SEO-04), SCRUM-152 (SEO-05), SCRUM-153 (SEO-06), SCRUM-155 (SEO-08), SCRUM-157 (SEO-10) under umbrella SCRUM-203 · Plan: [plan.md](plan.md) · Spec: [spec.md](spec.md). Defaults followed: Q-SEO-2 (filter and sort variants canonical to the base listing, paginated pages self-canonical, one setting), Q-SEO-3 (`gtin13` only once the GS1 prefix is real), Q-SEO-4, Q-SEO-5 (TBT stands in for INP), Q-SEO-6 (three runs, median), Q-SEO-7 (sale template only for sale views). Tests carry `[Trait("Spec", "SEOX-n")]`.

Already in place and to be folded in, not redone: `shared/seo/seo.service.ts` (title, description, canonical, Open Graph, Twitter, `BreadcrumbList`, Organization and WebSite data) and the status handling in the listing, product and page containers. Task 1 moves it to `features/storefront/seo` and widens it. PDP-14 (Product JSON-LD) is closed here.

**Deferred (owner decision 2026-10-25): the whole `seo` feature is built after the rest of the storefront, once pages and a deployment exist. Nothing below is started. PDP-14 (Product JSON-LD) stays open until then.**

## Web utilities (pure, `features/storefront/seo/utils`)

1. [ ] Move `SeoService` into `features/storefront/seo`, add the `BRAND` token (from `BRAND_*`) and a `SITE_URL` read from `ROMP_PUBLIC_HOST`; barrel exposes only `SeoService`, `ResponseService`, `BRAND`. Existing specs keep passing. Proof: existing `seo.service.spec.ts` plus brand-swap spec (SEOX-7, 16).
2. [ ] `MetaTemplates` for home, category, product, sale, collection, search and not-found with the §14.3 copy, "size guide" wording, brand from config, word-boundary truncation (title ≤ 60, description ≤ 160), key fabric per Q-SEO-4. Proof: unit tests per template and per truncation edge (SEOX-6, Q-SEO-7).
3. [ ] `CanonicalRule.canonicalFor(route, filter, page)` with the `pagination: self | base` setting and fixed query-key order. Proof: unit tests incl. unknown keys dropped (SEOX-5, 7).
4. [ ] `JsonLd` builders: `Product` + `Offer`/`AggregateOffer` (PKR, availability, url, sku, brand, image[], material, `gtin13` per Q-SEO-3), `BreadcrumbList`, `Organization`, `WebSite`; `serialise()` escapes `<`, `>`, `&`, U+2028, U+2029. Proof: unit tests, including a product named `</script><script>`, and a check against the Schema.org required and recommended fields for `Product`, `Offer`, `BreadcrumbList` (SEOX-3, 4, 14).
5. [ ] `RobotsPolicy` (staging ⇒ none; production ⇒ disallow list; empty or filtered listing and search ⇒ `noindex`). Proof: unit tests (SEOX-10, 15).

## Pages

6. [ ] `SeoService.apply(pageType, data)` used by every container: home, listing, search, product, content page, not-found; `og:image` is the primary 1280 JPEG, absolute, with width and height; Twitter `summary_large_image`; category and home use a configured default image. Proof: container specs and an SSR HTML test per page type (SEOX-6, 8).
7. [ ] Product page: Product + Offer + BreadcrumbList JSON-LD and meta from the templates (replaces the plain title set in product-pages task 13). Proof: SSR HTML test parses the JSON-LD (SEOX-3, PDP-14).
8. [ ] Site-wide Organization + WebSite(SearchAction) on every page, BreadcrumbList on every page below home; no `AggregateRating`. Proof: crawler test over all page types (SEOX-4).
9. [ ] `ResponseService` and a status audit of every route type: 200, 301 (moved, case, trailing slash, unknown query keys), 404, 410, never a 200 "not found"; empty listing 200 with `noindex`. Proof: SSR test per route type (SEOX-2, 15).

## API and SSR server

10. [ ] `GetSitemapIndexQuery` and `GetSitemapPageQuery` (paths and last-modified only; products incl. sold-out, audiences, categories, collections and static pages with at least one listable product; ≤ 45,000 per page) and endpoints `GET /api/storefront/sitemap` and `/sitemap/{page}`. Proof: Testcontainers tests: unpublished absent, publish adds, no filter variants; HTTP test (SEOX-9, 5).
11. [ ] SSR routes `/sitemap.xml` (index or urlset), `/sitemap-{n}.xml`, `/robots.txt`, `/healthz`: XML via a writer, host from config only (never the `Host` header), 5-minute single-flight cache, `s-maxage=300`. Proof: server specs incl. forged `Host` header (SEOX-9, 10, 7).
12. [ ] Edge and proxy trust: `Cache-Control: public, s-maxage=EDGE_CACHE_SECONDS, max-age=0` on anonymous HTML, 404/410 at 30 s, 5xx and challenge `no-store`, no `Set-Cookie`; client address from `X-Client-IP` only when the socket equals `TRUSTED_PROXY`; token-bucket limiter shared with search task 14. Proof: server specs incl. spoofed header ignored (SEOX-19, 20).
13. [ ] Noindex and host guards: `NOINDEX=true` adds `X-Robots-Tag: noindex, nofollow` and the robots meta on storefront and admin; start-up refuses `NOINDEX=true` with a host in `deploy/production.hosts`; staging Access or basic-auth switch recorded. Proof: server specs; Playwright run with `ROMP_PUBLIC_HOST=shop.example.test` asserts robots, sitemap, canonical, `og:url` and JSON-LD URLs (SEOX-11, 17, 18).

## Budgets

14. [ ] `src/web/lighthouserc.json` (three runs per URL, mobile: SEO = 1, accessibility ≥ 0.95, performance ≥ 0.9, LCP ≤ 2500, CLS ≤ 0.1, TBT ≤ 200), `@lhci/cli` dev dependency, a Linux CI job starting the compose stack with a deterministic fixture (40 products, 8 categories and collections, derivatives). Proof: the job fails on a deliberately broken budget, passes on main (SEOX-12). CI only on this machine.
15. [ ] Structure checks in the crawler test: exactly one `<main>`, one `<h1>`, no skipped heading levels, alt on every image, for every page type. Proof: the test (SEOX-1, 13).

## Close

16. [ ] Playwright `seo.e2e.spec.ts` (no-JavaScript fetch of each page type: content, links, tags, JSON-LD; sitemap lists a published product and drops an unpublished one; robots; brand swap). Proof: the suite on the Docker stack (SEOX-1..11, 16).
17. [ ] Manual Rich Results Test on staging recorded in `traceability.md`; spec "As built", README status; BRD §14.3 wording logged on SCRUM-168.
