# Hydration state: technical plan

- **Spec:** [spec.md](spec.md)
- **Module(s):** Storefront (web). No API, schema, endpoint or event changes.

## Cause (verified in the code)

`SearchPageContainer`, `ListingPageContainer` and `ProductPageContainer` dispatch `load` from an `effect()` after the first render. The slice starts as `{ key: null, result: null }`, so the first client render takes the `@else` branch ("Searching…" / "Loading…"). The HTTP transfer cache only saves the network request; it cannot fill the store before the first render. (Navigation and settings already avoid this with `TransferState` inside their effects; the result slices do not.)

## Approach (ADR 0028)

A **state-transfer meta-reducer** with a **whitelist**:

1. **Server:** a provider registers `TransferState.onSerialize(STATE_KEY, ...)` that reads the whitelisted slices (`listings`, `searchResults`, `productPages`, and per Q-HYD-1 `page`) from the store and writes only `{ key, result }` of each, with `loading` forced to `false`.
2. **Browser:** a meta-reducer runs on the store's init action, reads `STATE_KEY` from `TransferState` once, shape-checks it and merges those slices into the initial state. The key is then removed, so it is used exactly once.
3. The containers need no change: `result()` already matches `key` to the current request key, so a transferred result with the same key renders immediately. The `load` dispatch still runs, but its response comes from the HTTP transfer cache and the reducer keeps the result on screen meanwhile (as it already does for filter changes).

The meta-reducer is driven by a typed whitelist (`TRANSFERRED_SLICES`); a new slice needs one explicit line and a test. Nothing is transferred by default.

## Files

| File | Change |
|---|---|
| `src/app/core/state-transfer/state-transfer.ts` | whitelist, `STATE_KEY`, serialise function, meta-reducer factory |
| `src/app/core/state-transfer/state-transfer.providers.ts` | `provideStateTransfer()`: registers the meta-reducer and the server `onSerialize` hook |
| `src/app/app.config.ts` | add `provideStateTransfer()` next to `provideStore` |
| `e2e/journeys/storefront-ux.e2e.spec.ts` | remove the `test.fixme` for `/search`; add listing and product CLS cases |

## Tests (written first)

| Test | Proves |
|---|---|
| unit: serialise keeps only whitelisted slices and sets `loading=false` | HYD-3, HYD-4 |
| unit: serialise never includes `cart`, `wishlist`, `customerAuth`, `addresses`, `checkout`, `orderLookup` even if present; whitelist contains none of those names | HYD-3 (drift guard) |
| unit: meta-reducer seeds the slices once and removes the key; a second init is a no-op | HYD-1, HYD-6 |
| unit: missing or malformed transferred value leaves the initial state | HYD-6 |
| container spec: with a seeded slice the first render shows results, never the loading text (search, listing, product) | HYD-1 |
| e2e: `/search?q=...`, a listing, a product page: CLS <= 0.1, no console errors, no NG0500 | HYD-2 |
| e2e: a client-side filter change still shows the old page until the new one arrives | HYD-5 |
| e2e/server HTML: transferred state holds the result for the rendered request only and is under 100 kB | HYD-4 |

## NFR / security

- NFR-01: removes the layout shift. SEO-01 unchanged (the server HTML is the same).
- HYD-3 is the main risk, because store state can hold personal data. The whitelist is explicit, a test fails if a customer slice appears in it, and what is sent is the same public catalogue data already in the server HTML. No cookies, tokens or customer API results are read for it.
- Payload: one result page per slice, only for the current route, asserted under 100 kB.

## Risks & alternatives

- **Per-effect TransferState (like navigation):** the dispatch happens after the first render, so it is too late; would need route resolvers for every page. Rejected.
- **Route resolvers that fetch before render:** block navigation on the network and change the loading UX on client navigation. Rejected.
- **Transfer the whole store:** would leak customer slices. Rejected (HYD-3).
- **Fixed-height skeletons:** hide the shift but the page still re-renders and flashes after hydration. Possible later; not a fix.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | n/a, no endpoint; only public catalogue data crosses; customer slices excluded by whitelist and a test |
| Input validation | n/a, no new input; the transferred value is shape-checked (key string, result object) before merging, else ignored |
| Output encoding | TransferState escapes script-breaking characters; search text still shown as encoded text (SRC-3) |
| Outbound calls | none |
| Resource limits | one page of results per slice; size cap asserted in a test |
| Concurrency | n/a (state is read once at init) |
| Idempotency | key removed after first read; a second init changes nothing |
| Audit | n/a |
| Data protection | whitelist plus negative test; nothing about children, orders or addresses is serialised (SEC-25, NFR-27) |
| Secrets and dangerous configuration | none |
| Error responses | status stays in the transferred result; contract unchanged |
| New dependencies | none (`@angular/core` TransferState, `@ngrx/store` meta-reducers) |
| Coupling | `core/state-transfer` knows slice names only; features do not import it |
| Abuse cases | (1) someone adds `cart` to the whitelist: the negative test fails; (2) hostile search text containing `</script>`: TransferState escaping plus a test; (3) forged transferred value in a cached page: only shape-checked public data, and the next navigation reloads from the API |

## Tasks (proposed order, each test-first)

1. Unit tests, then `state-transfer.ts` (whitelist, serialise, meta-reducer).
2. `provideStateTransfer()` wired in `app.config.ts`.
3. Container specs: seeded slice renders without loading text (search, listing, product).
4. E2E: un-fixme `/search`, add listing and product CLS cases; run with `STOREFRONT_BASE_URL=http://localhost:4000`.
5. Rebuild the docker storefront, record CLS before/after in the PR.
6. Update this spec, the search and listings READMEs, and the memory hand-off.
