# Tasks: listings (Sprint 9a)

Plan: [plan.md](plan.md) · Spec: [spec.md](spec.md). Depends on published-catalog tasks 8-13. Defaults followed: Q-LST-2 (Sale = in-stock with compare-at above price), Q-SEO-2 (paginated pages self-canonical). Tests carry `[Trait("Spec", "LST-n")]`.

1. [ ] Domain: `ListingScope`, `ListingFilter` (parse, clamp, allow-lists, value cap 20), `ToCanonicalQuery()` in fixed key order. Proof: domain unit tests incl. hostile input (LST-11, 16).
2. [ ] Same-variant match rule as a pure predicate plus the SQL form. Proof: tests with size+colour+price on different variants of one product (LST-1, 2).
3. [ ] `SearchListingQuery`: tiles projected in SQL, one second query for primary-image derivatives via `Contains`, `COUNT(*) OVER()`, sorts ending in unique id. Proof: Testcontainers tests for each scope kind, sort stability across pages, sold-out exclusion and partly-sold-out inclusion (LST-4, 7, 8, 9, 15).
4. [ ] `GetListingOptionsQuery` (sizes, colours, price range, two aggregates). Proof: test that options only list values present in scope (LST-1, 2).
5. [ ] `GetEmptyStateQuery` suggestions. Proof: test (LST-5, 6).
6. [ ] Endpoints `GET /api/storefront/listings` and `/listings/options` with the `Storefront.Public` policy, `pageSize` cap 48, 400 on malformed numbers, 404 unknown code or past-last page. Proof: HTTP integration tests (CI): bad input, `page=999999999`, 1e309, markup, no stack trace (LST-11, 14).
7. [ ] Query-count and speed test at 1,000 products x 8 variants under 200 ms, constant queries. Proof: integration test (LST-13).
8. [ ] Covering index `PRDT_VRNT (SIZE_CODE, CLR_CODE, PRDT_ID) WHERE AVLB_QTY > 0` (in the published-catalog migration if not yet cut). Proof: `EXPLAIN` assertion in the speed test.
9. [ ] Web: `features/storefront/listings` skeleton by CONVENTIONS.md §1; models, `listings-api.service`, NgRx store (filter change resets to page 1), barrel. Proof: store and service specs.
10. [ ] Routes (`/new-in`, `/sale`, `/collections/:code`, `/:gender`, `/:gender/new-in`, `/:gender/sale`, `/:gender/:category`) with resolver, TransferState, SSR render, real 404 for unknown codes, 301 to canonical query. Proof: route and resolver specs; SSR test for status codes (LST-10, 11, 14).
11. [ ] Components: `product-tile` (reuse `rp-product-card`/`rp-image`), `filter-panel` (real GET form, fieldset/legend, works without JS), `sort-select`, `active-filters`, `pagination`, `empty-state`. Proof: component specs, axe, no-JS fetch test (LST-3, 6, 12).
12. [ ] Replace the mock catalogue on home/listing pages with the API; keep the mock only as a test fixture. Proof: the existing SCRUM-207 specs updated and passing.
13. [ ] Playwright e2e: filter, clear, sort + page 2, empty state, past-the-end 404, at seven viewports. Proof: `listings.e2e.spec.ts` (LST-1..6, 12, 14).
14. [ ] Update spec to what was built; README status.
