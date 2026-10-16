# SCRUM-24: Search, autosuggest and SKU lookup

- **Status:** Approved (owner, 2026-10-16; open items below carry their proposed default)
- **Jira:** SCRUM-24 (FR-04), SCRUM-26 (FR-06), SCRUM-25 (FR-05, zero results); umbrella SCRUM-203
- **BRD sections:** §4.1 FEAT-CAT-05, 06, 07; §12 (SKU lookup O(1))
- **Requirement IDs:** FR-04, FR-05, FR-06, NFR-02, SEC (input handling, TC-CAT-08)

## Problem & intent
Parents search by what they have in mind ("dungaree", "eid kurta") or by a code from a tag or message. Search must feel instant, forgive typing slips, return the exact item for a SKU, and treat every character a visitor types as data, never as a command.

## Requirements (quoted from the BRD)
> **FR-04:** The system shall provide keyword search with autosuggest and safely handle/escape special or malicious input without error leakage.

> **FR-05:** The system shall show an explicit, non-broken empty state for zero-result search/filter combinations and for categories with zero live products.

> **FR-06:** The system shall support exact-match SKU/reference-number lookup.

> **NFR-02:** Search/filter results shall return within 1 second under normal load.

> **FEAT-CAT-05 Search with autosuggest:** Keyword search across title/category/SKU.

## User flow
1. A search box is in the header of every page. Typing shows up to 8 suggestions (products and categories); choosing one opens it; pressing Enter opens a results page.
2. The results page is a normal listing (filters, sort, pagination as in [listings](../listings/spec.md)) for the query.
3. A query that is exactly a SKU (or a product code) goes straight to that product.
4. No results shows an empty state with the query repeated safely, spelling help if available and category suggestions.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| SRC-1 | Given the keyword `dungaree`, then autosuggest appears and the results include matching products | TC-CAT-06 |
| SRC-2 | Given gibberish, then an empty state with category suggestions is shown, the layout is intact and the status is 200 with `noindex` | TC-CAT-07 |
| SRC-3 | Given `' OR 1=1 --`, `<script>alert(1)</script>`, very long input, control characters or Unicode oddities, then the input is handled as plain text, results are empty or correct, no error text, stack trace or SQL appears, and the echoed query is HTML-encoded | TC-CAT-08 |
| SRC-4 | Given an exact SKU, then the product is the top or only result and the lookup uses an index, not a scan | TC-CAT-09, FR-06, §12 |
| SRC-5 | Given an exact SKU in lower case or with surrounding spaces, then it still finds the product | FR-06 `(new)` |
| SRC-6 | Given a SKU of a variant of a product that is unpublished or archived, then nothing is found (no leak of unpublished products) | `(new)` |
| SRC-7 | Given autosuggest, then it answers within 300 ms server-side under normal load and the results page within 1 second | NFR-02 |
| SRC-8 | Given a query, then matching covers product name, category, collection, colour and SKU, and tolerates one slip (for example `dungree`) | FEAT-CAT-05 |
| SRC-9 | Given the suggestion box, then it is an ARIA combobox operable by keyboard (arrow keys, Enter, Escape), with a text label, and works as a plain GET form when scripting is off | NFR-19, CLAUDE.md |
| SRC-10 | Given repeated rapid requests from one client, then autosuggest and search are rate limited per real client IP at Cloudflare and in the SSR layer (forwarded headers trusted only from Cloudflare ranges) and answer 429 politely, never errors; the API itself applies no IP limit, since every call arrives from the SSR server | P-4 |
| SRC-12 | Given a synonym or spelling variant held in the synonyms table (for example `frock` ↔ `dress`, `kameez` ↔ `shalwar kameez`, Roman Urdu spellings), then a search for either finds the same products; adding a row needs no deployment | P-3 |
| SRC-13 | Given a search with zero results, then the query text is logged for the business (normalised, truncated to 100 characters, no IP, no account, no session id; text that looks like an email address or a phone number is dropped), and the log can be read by staff to add synonyms | P-3, SEC-25 |
| SRC-14 | Given search and autosuggest, then products with no in-stock variant are not returned, except that an exact SKU or product code still opens the product page | D-2 |
| SRC-11 | Given a search results page, then it is `noindex, follow` with a canonical to itself without paging noise (search result pages are not for Google) | SEO-06 `(new)` |

## Non-functional constraints
NFR-02 (≤ 1 s); parameterised queries only, query length capped (100 characters), result and suggestion counts capped; search text is logged only for zero-result queries, as in SRC-13 (queries can contain personal data); response caching of suggestions per normalised prefix.

## Out of scope
Search analytics and "popular searches" (S18), personalised ranking, an admin screen for synonyms (S9 seeds the table by migration and staff edit it through the admin API only if the plan finds it cheap; otherwise a later sprint), voice or image search, search within the admin app.

## Open questions
- [x] **Q-SRC-1 (decided, P-3)** PostgreSQL full-text search plus trigram similarity over the read model; the ADR states the size at which it is replaced.
- [x] **Q-SRC-2 (decided, P-3)** A synonyms / spelling-variants table exists as data from day one, with Roman Urdu and Pakistani product terms; zero-result queries are logged.
- [ ] **Q-SRC-3 What does "SKU lookup" mean for customers?** SKUs follow `ROMP-<style>-<colour>-<size>` (Q-SKU-1), a variant code. Proposed: an exact SKU opens the product page with that colour and size pre-selected; a product code (style) opens the product. Confirm.
- [ ] **Q-SRC-4 Suggestion contents.** Products and categories (proposed), also recent searches (needs browser storage and is a privacy decision), no.
