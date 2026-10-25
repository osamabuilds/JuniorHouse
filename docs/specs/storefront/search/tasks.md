# Tasks: search (Sprint 9c)

Jira: SCRUM-24 (FR-04), SCRUM-26 (FR-06), SCRUM-25 (FR-05) under umbrella SCRUM-203 · Plan: [plan.md](plan.md) · Spec: [spec.md](spec.md). Depends on published-catalog (read model) and listings (filter parser, tiles, empty state). Defaults followed: Q-SRC-3 (exact SKU opens its product with colour and size pre-selected; a style code opens the product), Q-SRC-4 (suggestions are products and categories, no recent-search storage). Tests carry `[Trait("Spec", "SRC-n")]`.

The read model has the unique `SKU_NORM` index but not yet the full-text or trigram columns, so task 1 adds them.

## Data

1. [x] Migration (schema `STFR`): `CREATE EXTENSION pg_trgm`; `SRCH_VEC tsvector` (weights A name, B category/collection/colour names, C description) and `SRCH_TXT` on `PRDT_MAIN`, filled by the projection; GIN indexes on both; `SRCH_SYNM_GRP`, `SRCH_SYNM_TERM` (unique `TERM`), `SRCH_MISS` (unique `QRY_TXT`,`MISS_DT`); seed the starter synonym list. New glossary words (`SYNM`, `MISS`, `QRY`) added to `docs/db/naming.md`. Proof: naming test passes, migration applies on Testcontainers, `EXPLAIN` shows the GIN indexes used (SRC-4, 8, 12). *Built: columns are `SRCH_TEXT` and `QRY_TEXT` (the glossary word for text is `TEXT`, not `TXT` as the plan wrote); `SRCH_VEC` is a generated column outside the EF model; existing rows are backfilled by the migration.*
2. [x] Projection writes `SRCH_VEC` and `SRCH_TXT` in the same upsert as the product row, and the rebuild fills them. Proof: projection test that a rename changes what is found (SRC-8).

## API

3. [x] Domain `Storefront.Domain/Search`: `SearchQuery.Normalise` and tokenise (NFKC, lower case, control and bidi characters out, cap 100 chars, at most 8 tokens of 1-40 chars), `SkuQuery.TryParse`, `MissRedactor`, `SynonymExpansion`. Proof: unit tests with `' OR 1=1 --`, `<script>`, `& | ! ( ) :*`, NUL, bidi marks, emoji, 10,000 characters (SRC-3, 5, 12, 13).
4. [ ] `SearchProductsQuery`: expanded tokens to one parameterised `to_tsquery('simple', @q)` with prefix matching, `ts_rank_cd` then `PUBL_DTE`; trigram fallback below 3 results; listable products only; filters via the listings parser; same page returns the filter options. Proof: Testcontainers tests for `dungaree`, `dungree`, name, category, collection, colour, hostile input, sold-out product excluded (SRC-1, 2, 3, 8, 14).
5. [ ] `LookupSkuQuery`: exact match on `SKU_NORM` (case and spaces forgiven) or style code; unpublished and archived give nothing; a sold-out variant still resolves. Proof: tests incl. `EXPLAIN` uses the unique index (SRC-4, 5, 6, 14).
6. [ ] `SuggestQuery`: up to 8 products and categories, 60 s cache per normalised prefix bounded to 1,000 entries. Proof: tests for caps, cache hit, no sold-out product (SRC-1, 7, 14).
7. [ ] `RecordMissCommand` after the response, upsert with `ON CONFLICT … MISS_CNT + 1`, redaction applied, failure swallowed; `PurgeMissesJob` for `MissRetentionDays`. Proof: tests for redaction (email, 7+ digits, URL), one row per text per day, purge, a failing write leaves the response intact (SRC-13).
8. [ ] Endpoints `GET /api/storefront/search`, `/search/suggest`, `/search/sku` with the `Storefront.Public` policy; 400 only for malformed numbers; ProblemDetails, never SQL or a stack trace. Proof: HTTP integration tests (CI) with hostile input, anonymous allowed (SRC-3, 4).
9. [ ] Speed test at 1,000 products: suggest under 300 ms, results under 1 s, constant query count. Proof: integration test (SRC-7, NFR-02).
10. [ ] Admin endpoints for synonym groups (paged list, create, update with concurrency token, delete) and the miss log, permission `storefront.search.manage` (added to Sprint 8's lookups when identity lands; until then the same open policy as other admin endpoints), edits audited as the other admin writes. Proof: HTTP tests for validation (length, charset, duplicate term across groups) and that a new row changes results without a restart (SRC-12, 13).

## Web (`features/storefront/search`, CONVENTIONS.md §1)

11. [ ] Replace the mock in `features/storefront/search-suggest` with the real service (`/api/suggest` through the SSR server, 200 ms debounce, cancellation); keep or fold the folder into `search` so web and API names match (`StructureSyncTests`). Proof: store and service specs.
12. [ ] Header search `<form role="search" method="get" action="/search">` with a labelled `<input type="search">`, enhanced to an ARIA combobox (`aria-expanded`, `aria-controls`, `aria-activedescendant`, arrows, Enter, Escape). Works with scripting off. Proof: component spec, axe, keyboard test (SRC-9).
13. [ ] `/search?q=` route and container: results like a listing under `Results for “…”`, `noindex, follow`, canonical to `/search?q=`, empty state with category suggestions and a 200; exact SKU hit redirects 302 to the product path with `?colour=&size=`; the echo is the normalised text. Proof: container spec, SSR HTML test (SRC-2, 3, 4, 11).
14. [ ] *(Deferred with `seo`: the limiter is shared with seo task 12.)* SSR server: rate limit for `/search` and `/api/suggest` (30 and 120 per minute per client, configurable), 429 with a plain message, keyed on the proxy-supplied client address only. Proof: server spec with a spoofed header from a non-proxy socket (SRC-10).
15. [ ] Admin app screen (last, may slip): "Search" page with Synonym groups and Zero-result searches tabs, labelled inputs, semantic tables. Proof: component specs.

## Close

16. [ ] Playwright `search.e2e.spec.ts` at seven viewports on the Docker stack: suggest, keyboard, results, SKU redirect, empty state, hostile input, no-JavaScript submit. Proof: the suite (SRC-1..9, 11).
17. [ ] `traceability.md` (`SRC-n` to tests), spec "As built", README status, ADR 0018 §6 updated with the size at which Postgres search is replaced.
