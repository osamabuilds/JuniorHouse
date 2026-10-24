# SCRUM-24: Search, autosuggest and SKU lookup: technical plan

- **Spec:** [spec.md](spec.md) · **Module plan:** [../plan.md](../plan.md) · **ADR:** [0018](../../../adr/0018-storefront-module-read-model-and-edge.md) §6
- Open questions at their proposed default: Q-SRC-3 (an exact SKU opens its product with colour and size pre-selected; a style code opens the product), Q-SRC-4 (suggestions are products and categories; no recent-search storage).

## Domain model
- `SearchQuery` (value, pure): `Normalise(raw)` → NFKC, lower case, control and bidi characters removed, whitespace collapsed, trimmed, cut at 100 characters; tokenised on non-alphanumerics into at most 8 tokens of 1-40 characters. An empty result is a valid "no query".
- `SynonymExpansion`: each token is looked up in the synonym groups (loaded into memory, refreshed every 60 s or when a staff edit commits); a token expands to the OR of its group's terms. Multi-word terms ("shalwar kameez") are stored as phrases and matched on the normalised query before tokenising.
- `SkuQuery.TryParse(raw)`: after normalising, if the text matches the SKU shape (`romp-…`, or a style code) it is a lookup candidate.
- `MissRedactor`: drops a miss when it contains `@`, a run of 7+ digits, or a URL scheme, before it is stored (SRC-13).

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `SearchProductsQuery` | raw text, filter (same parsing as listings), page | page of tiles + options, or the empty state | SRC-1..3, 8, 14, 11 |
| `SuggestQuery` | raw text | up to 8 items: products (name, path, image) and categories | SRC-1, 7, 14 |
| `LookupSkuQuery` | raw text | `{ path, colourCode, sizeCode }` or none | SRC-4, 5, 6 |
| `RecordMissCommand` | normalised text | upsert (text, day) +1 in `SRCH_MISS`; fire-and-forget after the response, failure never affects the result | SRC-13 |
| `ListMissesQuery` (admin) | page | text, day, count, ordered by count | SRC-13 |
| `Create/Update/Delete SynonymGroup` (admin) | name, terms | group with terms; refreshes the in-memory set | SRC-12 |
| `PurgeMissesJob` | none (daily) | deletes rows older than `MissRetentionDays` | SRC-13 retention |

**Matching.** The expanded tokens become one `tsquery` built only from sanitised tokens (letters and digits, no operators), each token `:*` prefix-matched, OR across a token's synonyms, AND across tokens, passed as a parameter to `to_tsquery('simple', @q)` against `SRCH_VEC` (weights A name, B category/collection/colour names, C description). Ranking `ts_rank_cd` then `PUBL_DTE`. If the full-text result has fewer than 3 products, a second query uses `word_similarity` on `SRCH_TXT` (trigram, threshold from configuration, default 0.45) to tolerate a slip ("dungree"); results are merged, de-duplicated and capped. Only listable products are returned (SRC-14); the same page also returns the filters of `listings` (same parser, same-variant rule).

**SKU.** `SKU_NORM = @normalised` on the unique index; a style code matches `PRDT_MAIN.CODE` (unique). A hit on an unpublished or archived product is impossible because those rows are not in the model (SRC-6). A SKU of a sold-out variant still returns its path (SRC-14 exception).

## API endpoints
| Method | Route | Auth | Notes |
|---|---|---|---|
| GET | `/api/storefront/search?q=&…filters&page=` | `Storefront.Public` | 200 with results or empty state data; 400 only for malformed numbers |
| GET | `/api/storefront/search/suggest?q=` | `Storefront.Public` | in-memory cache 60 s per normalised prefix, bounded to 1,000 entries |
| GET | `/api/storefront/search/sku?code=` | `Storefront.Public` | 200 with target or 404 |
| GET | `/api/admin/storefront/search/misses?page=` | permission `storefront.search.manage` (Owner, Marketing, Catalogue), added to Sprint 8's permission lookups | paged |
| GET/POST/PUT/DELETE | `/api/admin/storefront/search/synonyms` | `storefront.search.manage` | paged list; changes audited by the Sprint 8 trail |

Ownership: n/a.

## Events
None.

## Data
`SRCH_SYNM_GRP` (`ID`, `NAME`, `ACT_IND`) and `SRCH_SYNM_TERM` (`ID`, `GRP_ID`, `TERM` varchar(60) lower-case, unique (`TERM`) so a term belongs to one group, `PHRS_IND`); `SRCH_MISS` (`ID`, `QRY_TXT` varchar(100), `MISS_DT`, `MISS_CNT`; unique (`QRY_TXT`,`MISS_DT`)). Seed migration inserts a starter list (frock/dress/gown, kurta/kameez/kurtha, shalwar kameez/shalwar qameez/salwar kameez, jumpsuit/romper/dungaree/dungarees, tee/t-shirt/tshirt, pajama/pyjama/nightwear, jacket/coat) which the owner reviews on staging (starter terms are English and Roman Urdu variants of products Romp sells; the owner extends them from the miss log). Indexes from the published-catalog plan (GIN on `SRCH_VEC`, GIN trigram on `SRCH_TXT`, unique `SKU_NORM`).

## NFR / security design
NFR-02: expected 5-30 ms per query at this size; the test asserts under 300 ms for suggestions and 1 s for results on 1,000 products. SQL injection is excluded structurally: the user text never reaches SQL text, only parameters, and the `tsquery` is built from tokens that already passed the character allow-list (the test sends `' OR 1=1 --`, `& | ! ( ) :*`, long strings, NUL, bidi marks and emoji and expects clean results or empty states). The echoed query is the normalised text, HTML-encoded by Angular. Rate limiting is at Cloudflare and the SSR server (P-4); the API additionally has a global concurrency limit, not a per-IP one. Misses are written after the response and never include IP, account or session.

## Frontend (`features/storefront/search`; admin screen in `src/web/projects/admin/.../features/storefront/search`)
- Header search form: `<form role="search" method="get" action="/search">` with a labelled `<input type="search">`; progressive enhancement adds the ARIA combobox listbox (`aria-expanded`, `aria-controls`, `aria-activedescendant`, Escape closes, arrow keys move, Enter follows) fed by the SSR server's `/api/suggest` route with a 200 ms debounce and request cancellation.
- `/search?q=` renders like a listing with the query as heading ("Results for “…”"), `noindex, follow`; empty state with category suggestions; exact SKU hit redirects 302 to the product path with `?colour=&size=`.
- Admin (small): a "Search" page with two tabs, Synonym groups (table with labelled inputs, add/remove terms) and Zero-result searches (paged table); semantic table markup, mobile-first. These are the last tasks of the sprint and may slip without blocking the storefront (the seed list works without them).

## Risks & alternatives considered
Stemming: the `simple` configuration avoids English stemming that mangles Roman Urdu; plural variants are covered by prefix matching and synonyms. A search engine is deferred with explicit triggers (ADR 0018). Pakistani spelling variety is handled as data, not code.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | Public GETs explicit; admin endpoints use a permission policy (Sprint 8 roles); every admin write is audited |
| Input validation | Normalise, cap, tokenise with a character allow-list; filters parsed as in listings; synonym terms validated (length, charset, no duplicates across groups) |
| Output encoding | Echo encoded by the framework; JSON responses; the `tsquery` text is a parameter |
| Outbound calls | none |
| Resource limits | 100 chars, 8 tokens, 8 suggestions, page ≤ 48, suggestion cache bounded, global concurrency limit, edge/SSR rate limits |
| Concurrency | Synonym edits use a concurrency token; miss counts use `ON CONFLICT … DO UPDATE SET MISS_CNT = MISS_CNT + 1` |
| Idempotency | Miss recording is additive and best-effort; admin edits are idempotent by id |
| Audit | Synonym edits audited (actor, group, before/after names); reads of the miss log not audited |
| Data protection | Miss text only: no IP, no account, no session; redactor drops email- or phone-like text; 180-day retention; search text is never written to ordinary logs |
| Secrets and dangerous configuration | Similarity threshold and limits validated at start-up |
| Error responses | ProblemDetails; no SQL or stack trace ever returned (tested with hostile input) |
| New dependencies | none (`pg_trgm` is a PostgreSQL contrib extension, installed in the migration) |
| Coupling | Only `STFR`; admin screen in the admin app calls its own admin endpoints |
| Abuse cases | Regex-denial and operator injection into `tsquery` (allow-list tokens); autosuggest flooding (edge and SSR limits, cache by prefix); poisoning the miss log with junk (one row per distinct text per day, 100-char cap, retention purge, redaction); enumerating unpublished SKUs (they are not in the model) |
