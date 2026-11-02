# Cart: module plan (Sprint 11)

- **Specs:** [carts](carts/spec.md) · [totals](totals/spec.md) · [discount-codes](discount-codes/spec.md) · **Module README:** [README.md](README.md)
- **ADRs:** [0024 three modules](../../adr/0024-cart-orders-notifications-modules.md) · [0025 placement and reservation](../../adr/0025-order-placement-and-stock-reservation.md) (the discount hold) · [0011 security baseline](../../adr/0011-security-and-coupling-baseline.md) · [0004 outbox](../../adr/0004-postgres-transactional-outbox.md) · [0023 customer sign-in](../../adr/0023-customer-sign-in-sessions-and-methods.md)
- **Status:** Draft, awaiting owner approval. Owner answers (2026-11-02): reserve at placement, not at add-to-cart; shipping fee varies by city and is computed on the order placement page; all recommendations approved. **Still open:** the city rate table and COD fee (Q-TOT-1), the per-line maximum (Q-CRT-3; the plan assumes 10, configurable).

## Module layout

`Romp.Modules.Cart.{Domain,Application,Infrastructure,Contracts}`, tests `Romp.Modules.Cart.Tests`. `CartModule : IModule`, DbContext `CartDbContext` (schema `CART`), `AddOutboxModule("CART", …)`, inbox `CART.INBX`.

| Feature (same name in every tree) | API folder | Storefront web | Admin web |
|---|---|---|---|
| carts | `Carts/` | `features/cart/carts` (cart page, mini-cart drawer, add-to-cart on the product page) | n/a |
| totals | `Totals/` | `features/cart/totals` (summary block shared by cart and checkout) | `features/cart/shipping` (city rate table) |
| discount-codes | `DiscountCodes/` | `features/cart/discount-codes` (code box) | `features/cart/discount-codes` (list, create, deactivate) |

Build order: carts → totals → discount-codes, as the README says.

## Domain model (no EF, no ASP.NET)

- **`Cart`** aggregate: `Owner` (exactly one of `CustomerId` or `GuestKeyHash`), `Lines`, `AppliedCodeId?`, `LastTouchedAt`, `ExpiresAt?`. Rules: at most one line per variant; quantity 1 to `MaxPerLine` (default 10); `AddLine` adds to an existing line; `SetQuantity` and `RemoveLine` by variant id; `MergeFrom(guest)` adds quantities and returns the notice list (variants capped at stock). A cart never stores a price or a name.
- **`CartOwner`** value: `CustomerId` or `GuestKey` (a 32-byte random value, only its SHA-256 is stored).
- **`PricedLine` / `PricedCart`** (read model values): variant, product, name, colour, size, SKU, unit price, compare-at, quantity, line total, flags (`PriceChanged`, `LowStock(available)`, `Unavailable`).
- **`OrderTotals.Calculate(lines, discount?, shipping)`**: one pure function (TOT-1 to TOT-7). Subtotal = Σ unit price × quantity; discount from `DiscountRule.Evaluate(code, lines, subtotal, now)` (percentage or fixed, eligible lines only, never above the eligible subtotal); shipping from `ShippingPolicy.FeeFor(cityId, subtotalAfterDiscount, paymentMethod)`; total = max(0, subtotal − discount) + shipping. Rounding: half away from zero to 2 decimals (Q-TOT-3), `decimal` throughout. Orders calls this same routine through the contract; there is no second copy.
- **`DiscountCode`**: code (stored uppercase, trimmed), type (percentage or fixed), value, minimum spend, expiry, usage cap, per-customer limit (default 1), category scope, includes-sale-items flag (default no), active flag. `Evaluate` returns `Accepted(amount)` or a `Rejection` with a reason code: `Unknown` (shown identically for unknown, mistyped and deactivated, DSC-9), `Expired`, `Exhausted`, `AlreadyUsed`, `BelowMinimum(amount)`, `NoEligibleLines`, `SecondCode`.
- **`Redemption`**: `Held → Confirmed | Released`, keyed by placement key (ADR 0025).
- **`ShippingPolicy`**: per-city fee table, default fee, optional free-shipping threshold, optional COD fee (Q-TOT-1: values come from the business).

## Application layer (MediatR; commands carry `ICartCommand`)

| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `GetCartQuery` | owner (from cookie or session), optional city | priced cart with totals and flags | CRT-1, 7, 8; TOT-* |
| `AddLineCommand` | variant id, quantity | priced cart | CRT-4, 5, 6, 11 |
| `SetQuantityCommand` / `RemoveLineCommand` | variant id, quantity | priced cart | CRT-1, 2, 11 |
| `MergeGuestCartCommand` (via `ICustomerSignedInHook`) | customer id, guest key | merged cart, notices | CRT-3 |
| `ApplyDiscountCommand` / `RemoveDiscountCommand` | code | priced cart or rejection | DSC-1 to 9, 13 |
| `PriceForCheckoutQuery` (contract) | owner, city, payment method | `PricedCart` | TOT-*, ORD-4 |
| `HoldRedemptionCommand` / `ReleaseRedemptionCommand` (contract) | placement key, code, customer key | held or refusal | DSC-10 |
| `ConfirmRedemptionHandler` / `ReleaseRedemptionHandler` (event) | `OrderPlacedEvent` / `OrderCancelledEvent` | state change; empties the cart | ADR 0025 |
| `SweepHeldRedemptionsJob` | n/a | releases holds older than `HoldMinutes` | ADR 0025 |
| `Create/Update/DeactivateDiscountCodeCommand`, `ListDiscountCodesQuery` | per spec | | DSC-11, 12, FR-57 |
| `SetShippingRateCommand`, `SetShippingSettingsCommand`, `ListShippingRatesQuery` | city, fee; defaults | | TOT-5 |

**Pricing a cart is constant-query:** one cart read, one bulk call `Catalog.Contracts.IVariantSalesQueries.GetManyAsync(variantIds)` and one bulk call `Warehouse.Contracts.IStockAvailabilityQueries.GetManyAsync(variantIds)` (both new, max 100 ids, no N+1), then the pure function. Cart never reads the storefront read model (D-3).

## API endpoints (`/api/cart`, anonymous policy; the owner is read from the session cookie or the `romp-guest` cookie, never from a route or body)

| Method | Route | Auth / role | Ownership check (SEC-05) |
|---|---|---|---|
| GET | `/api/cart?cityId=` | Public (cookie owner) | owner = cookie; no ids in the route |
| POST | `/api/cart/lines` `{variantId, quantity}` | Public + origin check | same |
| PUT | `/api/cart/lines/{variantId}` `{quantity}` | Public + origin check | variant id is catalogue data, not a resource of someone else; the cart is the caller's |
| DELETE | `/api/cart/lines/{variantId}` | Public + origin check | same |
| POST / DELETE | `/api/cart/discount` `{code}` | Public + origin check + rate limit `discount-apply` | same |
| GET | `/api/discounts`, `/api/discounts/{id}` | `discounts.manage` | staff |
| POST / PUT | `/api/discounts`, `/api/discounts/{id}` | `discounts.manage` | staff |
| POST | `/api/discounts/{id}/deactivate` | `discounts.manage` | staff |
| GET / PUT | `/api/shipping/rates`, `/api/shipping/rates/{cityId}`, `/api/shipping/settings` | `shipping.manage` | staff |

A guest with no cart cookie gets one created on the first add (`romp-guest`: random 32 bytes, base64url, `HttpOnly`, `Secure` outside local, `SameSite=Lax`, path `/`, 30 days). Request bodies are limited to 2 KB; prices and totals in a body are not part of the DTO and are ignored (CRT-6).

## Events

| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|
| `OrderPlacedEvent` v1 (`Orders.Contracts`) | Orders | Cart (confirm redemption, empty cart) | yes |
| `OrderCancelledEvent` v1 (`Orders.Contracts`) | Orders | Cart (release redemption) | yes |
| `DiscountCodeChangedEvent` v1 (ids only) | Cart | audit (ADR 0016 path) | yes |

Both consumed events enter through one mapper, `Orders/OrderEventMapper.cs`; each handler is idempotent through the inbox and by the placement key.

## Contracts (additive)

| Need | Provider | Change |
|---|---|---|
| Variant price, compare-at, sellable, product name and path, colour, size, SKU, category id, main image key, by variant ids | `Catalog.Contracts.IVariantSalesQueries` | **new**, reads the published data Catalog already owns |
| Available units for many variants | `Warehouse.Contracts.IStockAvailabilityQueries.GetManyAsync` | **new method**, same availability as the storefront (on-hand minus reserved) |
| Cities (for the rate table and the checkout city) | `Reference.Contracts.IGeographyQueries` | none |
| Sign-in hook | `Customers.Contracts.ICustomerSignedInHook` | **moved** from `Customers.Application` (ADR 0024) |
| Priced cart, redemption hold and release | `Cart.Contracts.ICheckoutPricing`, `IDiscountRedemptions`, `ICartOwnerAccessor`, `CartOwner` | **new**, used by Orders |

## Data (schema `CART`, names per `docs/db/naming.md`)

| Table | Notes |
|---|---|
| `CART_MAIN` | `CUST_ID` (plain reference) or `GST_KEY_HASH`; `DSCT_CODE_ID` nullable FK; `EXPR_DTE`; check constraint: exactly one of `CUST_ID`, `GST_KEY_HASH`; partial unique index per customer and unique on guest hash; index on `EXPR_DTE` for the purge job |
| `CART_LINE` | `CART_ID` FK, `VRNT_ID` (plain), `QTY`; unique `(CART_ID, VRNT_ID)`; check `QTY > 0` |
| `DSCT_CODE` | normalised unique `CODE`; `DSCT_TYPE_ID` lookup (`PCT`, `FIXD`); `VAL numeric(12,2)`; `MIN_SPND_AMT`; `EXPR_DTE`; `USE_CAP`; `CUST_USE_CAP` default 1; `SALE_INCL_IND`; `ACT_IND` |
| `DSCT_SCOP` | `(DSCT_CODE_ID, CTGY_ID)` for category scope; FK indexed |
| `DSCT_RDMP` | `DSCT_CODE_ID` FK, `PLCE_KEY` unique, `ORDR_NO` nullable, `CUST_KEY` (customer id or SHA-256 of the normalised mobile for guests), `RDMP_STS_ID` lookup (`HELD`, `CONF`, `RLSD`), `AMT`; index `(DSCT_CODE_ID, CUST_KEY)` |
| `DSCT_TYPE_LKP`, `RDMP_STS_LKP` | standard lookup shape, seeded in the migration |
| `SHIP_CITY_RATE` | `CITY_ID` (plain reference to Reference), `FEE_AMT numeric(12,2)`; unique `CITY_ID`; `ACT_IND` |
| `SHIP_SETG` | one row: `DFLT_FEE_AMT`, `FREE_THR_AMT` null, `COD_FEE_AMT` null; row seeded with a zero default that a health warning flags in production until the business sets it |
| `OUTB_MSG`, `INBX` | module |

Audit columns and `xmin` on every transactional table; foreign keys declared and indexed inside the schema; no cross-schema foreign keys. Cap enforcement (DSC-10): `HoldRedemption` locks the code row (`SELECT … FOR UPDATE`), counts `Held + Confirmed` for the code and for the customer key, and inserts the hold in the same transaction. No table is large enough to need partitioning at launch volume (carts are purged by the expiry job).

**New glossary words** (added to `docs/db/naming.md` with the migration; the naming test enforces): `CART`, `DSCT`, `RDMP`, `SCOP`, `SHIP`, `RATE`, `SETG`, `GST`, `HLD`/`HELD` (as `HELD`), `RLSD`, `CONF` (check existing), `THR`, `SPND`, `INCL`, `SALE`, `CAP`, `TOUC`. Reused: `CUST`, `ID`, `QTY`, `AMT`, `VAL`, `EXPR`, `DTE`, `ACT`, `IND`, `CITY`, `PRDT`, `VRNT`, `CTGY`, `STS`, `LKP`.

## Policies, configuration and the web edge

- Options `Cart:*`, validated at start: `MaxPerLine` 10, `GuestCartDays` 30, `HoldMinutes` 30, `MaxLines` 50, `DiscountAttempts` 10 per `DiscountWindowMinutes` 15. A missing shipping default is allowed in Development and flagged by a health check elsewhere.
- Rate limiter policy `discount-apply` partitioned by client address and by owner (customer id or guest hash): 10 attempts per 15 minutes, then a 15-minute wait (Q-DSC-5). It sits beside the host-wide limiter and not in the `strict` group.
- Cart pages are `noindex`, `Cache-Control: private, no-store`, listed in `robots.txt` (SEO-05); the browser calls the API through same-origin `/customer-api/cart/**` pass-through routes in the SSR server, which forward cookies and the origin header like the sign-in routes.
- **Merge on sign-in:** `Cart` implements `ICustomerSignedInHook`; sign-in already reads `romp-guest` (ADR 0023) and calls the hook with the customer id and guest key; the guest cart merges (add quantities, capped at available stock, with a notice list), then the guest cart row is deleted and the cookie is cleared by the sign-in response. A failing hook is logged and skipped (existing behaviour), and the guest cart simply waits.

## Frontend (storefront, design system is the single source; admin keeps its look)

Build from `components/CartDrawer`, `CartPage`, `CartEmpty`, `Field`, `Button`, `ProductDetail` (add to bag). `features/cart/carts/{containers,components,store,models,services}` with NgRx (CONVENTIONS): `CartPageContainer`, `MiniCartContainer`, `CartLineComponent`, `QuantityStepperComponent`, `CartSummaryComponent`; actions `loadCart`, `addLine`, `setQuantity`, `removeLine`, `applyCode`, `removeCode`. The product page's purchase panel (S9) gains the real Add to bag that opens the drawer; Notify Me stays for sold-out variants. A line flagged `LowStock` or `Unavailable` shows its reason in text, not only colour; totals update inside an `aria-live="polite"` region. The route `/cart` is server-rendered (so the page works without script) and `noindex`. Open question carried from the design system (does the bag icon also link to `/cart`?) is raised again, not decided here. Admin: lazy routes `cart/discount-codes` (paged list, form, deactivate) and `cart/shipping` (city rate table with search and inline edit), each behind `*hasPermission`.

## Security & coupling review

| Area | How this module meets it |
|---|---|
| Authorization | Cart endpoints are anonymous by design; the owner comes only from the session or guest cookie (CRT-9); admin endpoints in `MapSecuredGroup` with `discounts.manage` / `shipping.manage` checked server-side (SEC-08); matrix rows added to the authorization matrix test |
| Input validation | FluentValidation per command: variant id > 0, quantity 1..`MaxPerLine`, code 3..32 chars `[A-Z0-9-]` after normalising, city id must exist; DTOs separate from entities; price or total fields do not exist on request DTOs (CRT-6) |
| Output encoding | JSON only; no raw SQL beyond quoted-identifier statements (`FOR UPDATE`, the redemption count); admin text rendered by Angular encoding |
| Outbound calls | none (contracts only) |
| Resource limits | body 2 KB; `MaxLines` 50; contract calls max 100 ids; admin lists paged 1..100; `discount-apply` limiter; guest-cart creation rate-limited per address with the host-wide limiter; expired carts purged in batches |
| Concurrency | unique indexes (one cart per owner, one line per variant, one redemption per placement key); code row lock for caps; `xmin` concurrency token on cart and code; stock is checked, not reserved, here (ADR 0025) |
| Idempotency | adding the same variant adds quantity (documented, not a duplicate line); redemption hold, confirm and release keyed by placement key; event handlers behind the inbox |
| Audit | audit columns; discount create, change and deactivate go through the audit trail (DSC-11, FR-59) |
| Data protection | no personal data stored except the guest-mobile hash in redemptions; no cart contents in logs; events carry ids only |
| Secrets and dangerous configuration | none new; cookie `Secure` forced outside Development; `AllowInsecureCookies` stays Development/Testing only |
| Error responses | problem details only; one generic reason for unknown, mistyped and deactivated codes (DSC-9); other customers' carts cannot be addressed at all |
| New dependencies | none |
| Coupling | contracts only (`IVariantSalesQueries`, `IStockAvailabilityQueries`, `IGeographyQueries`, `ICustomerSignedInHook`); one event mapper for `OrderPlacedEvent`/`OrderCancelledEvent`; no cross-schema foreign keys; architecture tests updated |
| Abuse cases | (1) a script guesses codes: limiter plus identical reason for every miss; (2) a shopper edits the request to send a price or a negative quantity: ignored / rejected; (3) two checkouts race for the last redemption: row lock and unique key, one wins; (4) a stolen guest cookie: it grants only a basket, never an address or payment; (5) cart creation flood: host-wide limiter plus 30-day purge |

## Risks

- **Catalog's variant query is new** and must read published data only; if Catalog changes its tables the contract absorbs it.
- **Stale price in a long-lived cart** is handled by pricing on every read and flagging `PriceChanged` (CRT-8).
- **Shipping rates are unknown** until the business supplies them; the table and screen are built, the data is theirs.
- **Integration tests cannot load locally built DLLs** on the developer machine (Application Control): run them in the Linux SDK container or CI, as for earlier sprints.
