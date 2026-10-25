# Wishlist: technical plan

- **Spec:** [spec.md](spec.md) · **Module:** Customers · **Module plan:** [../plan.md](../plan.md)

## Domain model
`WishlistItem` (`Id`, `CustomerId`, `ProductId` (Catalog product id as the storefront read model exposes it), `SavedPrice` decimal PKR at the time of saving). Rules: one item per customer and product; at most `MaxWishlistItems` (200). A pure `WishlistFlags` function turns (saved price, current price, available) into the flags `PriceDropped`, `OnSale`, `SoldOut`, `Unavailable` (WSL-5, 6).

## Application layer
| Command / Query | Output | Covers |
|---|---|---|
| `ListWishlistQuery` (paged) | items + product summary + flags | WSL-5, 6, 8 |
| `GetWishlistIdsQuery` | product ids on the list (max 200), for the heart state | WSL-1 |
| `AddWishlistItemCommand` | idempotent add (stores the current price taken from the read model, never from the client) | WSL-1, 4 |
| `RemoveWishlistItemCommand` | remove by product id | WSL-1 |
| `MergeGuestWishlistCommand` | add up to 50 product ids, skipping duplicates and unpublished products | WSL-3 |
| `WishlistSignedInHook : ICustomerSignedInHook` | calls the merge when the guest state carries ids | WSL-3, ACT-14 |
| `IStorefrontCatalogQueries.GetProductSummariesAsync(ids)` | one bulk call per page | WSL-5, 8 |

Prices come only from the read model (SEC-09 spirit): `SavedPrice` is set server-side.

## API endpoints
| Method | Route | Auth | Ownership check (SEC-05) |
|---|---|---|---|
| GET | `/api/customers/me/wishlist?page&pageSize` | `Customer` | filter `CUST_ID` |
| GET | `/api/customers/me/wishlist/ids` | `Customer` | filter `CUST_ID` |
| PUT | `/api/customers/me/wishlist/{productId}` | `Customer` | customer from `ICurrentCustomer`; unique key makes repeats safe |
| DELETE | `/api/customers/me/wishlist/{productId}` | `Customer` | `WHERE CUST_ID AND PRDT_ID` (another customer's row cannot match) |
| POST | `/api/customers/me/wishlist/merge` | `Customer` | body is a list of product ids only |

(The spec's "wishlist item id" in SEC-05 is the product id within the caller's own list; there is no globally addressable item id, so there is nothing to enumerate.)

## Events
None.

## Data
`CUST.CUST_WSHL`: `ID`, `CUST_ID` FK, `PRDT_ID` bigint, `SAVD_PRIC_AMT` numeric(12,2), audit, `xmin`. `UQ_CUST_WSHL_CUST_ID_PRDT_ID`; index on `CUST_ID`. Saved date is `INSR_DTE`.

## NFR / security design
- **Cacheability:** product/listing pages are unchanged and stay in the shared edge cache. The heart renders in its off state on the server; after hydration the browser calls `GET …/wishlist/ids` through `/customer-api` and sets the state. A tap sends `PUT/DELETE`; guests get the prompt (WSL-2).
- **Guest items:** held in the browser (`localStorage` key with at most 50 product ids, no personal data); on sign-in the merge endpoint adds them once and the browser clears the key. With the Sprint 10 stand-in the sign-in step is the dev switch; the merge endpoint and hook are tested directly.
- Listing joins no tables of other modules: summaries arrive in one contract call per page; page size 1..100 (WSL-8).

## Frontend
Storefront `features/customers/wishlist`: `/wishlist` page (`WishlistPage` design: item rows with price, "Sale"/"Price dropped"/"Sold out" notes, Remove, and **Move to bag** shown as a link to the product page until S11 wires the cart), a `WishlistHeartComponent` (button, `aria-pressed`, name "Save {product} to wishlist") used on the product page and listing tiles, a guest prompt dialog (focus trap, Esc), header wishlist link with count. `noindex`, `no-store`, robots disallow.

## Risks & alternatives
Saving the colour as well as the product was rejected (BRD says product; default Q-WSL-2). Storing guest wishlists server-side rejected (needs identity and cookies; out of scope).

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | `Policies.Customer`; all statements filter `CUST_ID`; merge only touches the caller's list; ownership tests across two customers |
| Input | product ids positive and ≤ 50 per merge; unknown/unpublished ids ignored; no price accepted from client |
| Output encoding | Angular encoding; product links built from slugs returned by the contract |
| Outbound calls | none |
| Resource limits | 200 items, 50 merge ids, page size 1..100, `strict` not needed (authenticated) but default limiter applies |
| Concurrency | unique index; add is `INSERT … ON CONFLICT DO NOTHING` |
| Idempotency | add, remove and merge repeat-safe |
| Audit | audit columns |
| Data protection | wishlist reveals interests only; no personal data in logs/events; money as decimal |
| Secrets and config | `MaxWishlistItems` validated |
| Error responses | problem details |
| New dependencies | none |
| Coupling | `Storefront.Contracts.IStorefrontCatalogQueries` only (bulk, DTOs); no read of `STFR` tables |
| Abuse cases | (1) A script adding thousands of items: 200 cap + rate limit. (2) A client sending a low price to fake a "price drop": price is server-set. (3) Merging another customer's guest list: the list is just ids from the caller's own browser; it adds to the caller's list only |
