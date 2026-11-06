# Hydration state: no flash when the browser takes over a server-rendered page

- **Status:** Approved by owner 2026-11-05 (recommendations for Q-HYD-1 and Q-HYD-2); implemented
- **Module:** Storefront (web only; no API change)
- **Requirement IDs:** NFR-01 (CLS <= 0.1), SEO-01 (content in the server HTML), SEO-10 (budgets in CI), NFR-17
- **Jira:** none yet

## Why

The server renders `/search`, listings and product pages with their full content. When the browser starts, its NgRx store is empty, so the first client render shows "Searching…" / "Loading…" instead of the results. The page collapses for a frame and the footer jumps (measured CLS 0.2 to 0.4 on `/search`; the Playwright case in `storefront-ux.e2e.spec.ts` is `test.fixme` for it). Listings and the product page use the same pattern. This breaks the CLS budget and looks broken on slow phones.

## Needed

- **HYD-1** After hydration, a server-rendered search, listing or product page keeps showing the same content. It never shows "Searching…" or "Loading…" for the request the server already answered.
- **HYD-2** CLS on `/search?q=...`, a listing and a product page is <= 0.1 (the `test.fixme` becomes a normal test).
- **HYD-3** Only an explicit whitelist of read-only catalogue slices is sent to the browser. Never customer, cart, wishlist, address, checkout or order data, and never anything read with the customer's cookies (SEC-25, NFR-27).
- **HYD-4** The extra HTML is bounded: a slice is sent only for the request being rendered (one result page), not the whole store.
- **HYD-5** A later client-side navigation (new filter, new search) still loads normally and shows the previous page until the new one arrives (existing behaviour).
- **HYD-6** A stale or missing transferred slice is harmless: the page falls back to loading as it does today.

## Edge cases (new)

- Server render returns 404/410/redirect: the status is part of the transferred result, so the browser does not re-request and flip the status.
- The result contains text the visitor typed (search query): TransferState escapes it, and it is shown as encoded text only, as today (SRC-3).
- Browser back/forward after the transferred state was consumed: falls back to a normal load (HYD-6).

## Open questions

- [x] **Q-HYD-1** Which slices: `listings`, `searchResults`, `productPages` only, or also `page` (content pages)? Recommendation: all four, they have the same flash. **Resolved: all four slices.**
- [x] **Q-HYD-2** Is a new ADR acceptable for the mechanism? Recommendation: yes, ADR 0028 (draft). **Resolved: yes.**

## As built

`core/state-transfer/` (whitelist, serialiser, seeding meta-reducer, `provideStateTransfer()`), wired in `app.config.ts`. Unit tests cover HYD-1, 3, 4, 6; the e2e CLS cases (`/`, `/girls`, `/new-in`, `/search?q=dress`, a product page) cover HYD-1, 2 and 5 against the rebuilt container. Pagination is already enforced by the API (page size cap), which bounds HYD-4; the server HTML for a search page is about 70 kB in total. A dedicated test that asserts the transferred payload size is not written yet.
