# 0028 — Transfer whitelisted store slices from server render to browser

- Status: Proposed (draft, awaiting owner approval)
- Date: 2026-11-05

## Context

Server-rendered pages hydrate with an empty NgRx store, so the first client render shows a loading state instead of the content the server already produced. This causes a layout shift (CLS 0.2 to 0.4 on `/search`) against NFR-01. The HTTP transfer cache avoids a second request but not the empty first render.

## Decision

1. A meta-reducer seeds the browser store, once, from Angular `TransferState`; the server writes the same key at serialisation time.
2. **Only slices on an explicit whitelist are transferred** (`listings`, `searchResults`, `productPages`, and `page` if approved), and only their `key` and `result`, with `loading` forced to `false`.
3. Customer, cart, wishlist, address, checkout and order state is never transferred. A unit test fails if any of those names appears in the whitelist.
4. The transferred value is shape-checked before use; anything unexpected is ignored and the page loads as it does today.
5. The transferred key is consumed on first read.

## Alternatives considered

Per-effect TransferState (too late: dispatch happens after first render), route resolvers (block navigation, change loading UX), transferring the whole store (leaks personal data), fixed-height skeletons (hide the shift, still flash).

## Consequences

- Adding a new server-rendered result slice needs one whitelist line and a test.
- The page HTML grows by one result page of public catalogue data, which duplicates what is already in the markup. Accepted for CLS 0.
