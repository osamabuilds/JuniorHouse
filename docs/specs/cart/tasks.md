# Cart (Sprint 11): tasks, with the shared foundations for Sprints 11 to 13

Ordered, checkable steps. Each names the test that proves it. Test-first: write the failing test, then make it pass. Run .NET builds and tests in the Linux SDK container (Windows Application Control blocks locally built DLLs; CI is the other check) and say so when reporting. After the module is done: rebuild the docker stack and tell the owner what to click (owner request 2026-11-02).

Plan: [plan.md](plan.md) (approved 2026-11-02) · Specs: [carts](carts/spec.md), [totals](totals/spec.md), [discount-codes](discount-codes/spec.md) · ADRs 0024, 0025.

## Foundations (shared by Cart, Orders, Notifications; no behaviour change, existing tests are the proof)

- [x] **F1** (as built: interface in `Romp.BuildingBlocks.Application/Captcha`, adapters and `AddCaptcha` in `Romp.BuildingBlocks.Web/Captcha`, config section `Captcha:Turnstile`, tests moved to `Romp.BuildingBlocks.Web.Tests`) Move `ICaptchaVerifier` and its Turnstile and development adapters to a shared building block; Customers uses it from there. *Proof:* existing `Romp.Modules.Customers.Tests` and the customer endpoint tests stay green; `ModuleBoundaryTests`.
- [x] **F2** Move `PhoneNumber` to `Romp.BuildingBlocks.Domain`; Customers references it from there. *Proof:* `PhoneNumberTests` moved with it and green; Customers tests green.
- [x] **F3** Move the origin check to `Romp.BuildingBlocks.Web` as `OriginCheckFilter` (allowed origin from `Site:PublicBaseUrl`, else the module's public URL, else the request host); `CustomerOriginCheckFilter` becomes a thin use of it. *Proof:* existing `CustomerAuthEndpointsTests` (origin cases) green; new `OriginCheckFilterTests`.
- [x] **F4** Add `SignedLink` (purpose-bound HMAC-SHA256 token, expiry optional, constant-time compare, base64url) to `Romp.BuildingBlocks.Web`. *Proof:* `SignedLinkTests` (valid, wrong purpose, tampered, expired, truncated).
- [x] **F5** Move `ICustomerSignedInHook` to `Customers.Contracts`; add `ICurrentCustomerAccessor` there. *Proof:* sign-in tests and `SignedInHooksTests` green; `ModuleBoundaryTests`.
- [x] **F6** Identity migration seeds permissions `orders.view`, `orders.manage`, `discounts.manage`, `shipping.manage`, `notifications.view`, `notifications.resend` (additive); roles screen shows them. *Proof:* `PermissionSeedTests`, existing roles tests.
- [x] **F7** `Cart`, `Orders`, `Notifications` added to `ModuleNames`, the constitution table and `docs/db/naming.md` section 4 (schemas `CART`, `ORDR`, `NTFY`). *Proof:* `ModuleBoundaryTests`, `StructureSyncTests`, `GlossaryTests`.

## Cart module skeleton

- [x] **K1** Projects `Romp.Modules.Cart.{Domain,Application,Infrastructure,Contracts}` and `Romp.Modules.Cart.Tests`, `CartModule : IModule`, solution entries, `Program.cs` registration, `CartDbContext` (schema `CART`), outbox and inbox, first migration. *Proof:* `CartMigrationTests` (Testcontainers), architecture tests.

## carts

- [x] **C1** `Cart` aggregate and `CartOwner`: add, set quantity, remove, one line per variant, `MaxPerLine`, merge with notices. *Proof:* `CartTests`, `CartMergeTests` (CRT-3, CRT-11).
- [x] **C2** (as built: `VariantSale` carries the planned variant id the product page shows, the SKU and `LabellingVariantId`, which is the id Warehouse keys stock by) Contracts `Catalog.Contracts.IVariantSalesQueries.GetManyAsync` (published data only, max 100 ids) and `Warehouse.Contracts.IStockAvailabilityQueries.GetManyAsync`. *Proof:* `VariantSalesQueriesTests` and `StockAvailabilityManyTests` with query-count assertions (no N+1).
- [x] **C3** `CART_MAIN` and `CART_LINE` mappings and migration (exactly-one-owner check, unique indexes, expiry index). *Proof:* `CartMigrationTests`, `NamingConventionTests`, `GlossaryTests`.
- [x] **C4** Pricing read: `GetCartQuery` builds the priced cart (flags `PriceChanged`, `LowStock`, `Unavailable`) with a constant number of queries. *Proof:* `GetCartQueryTests` incl. query count (CRT-1, 7, 8).
- [x] **C5** Commands `AddLine`, `SetQuantity`, `RemoveLine` with stock and sellable checks. *Proof:* `CartCommandTests` (CRT-2, 4, 5, 6).
- [x] **C6** Guest identity: `romp-guest` cookie (random, hashed in the table, `HttpOnly`, `SameSite=Lax`, 30 days) and the owner accessor `ICartOwnerAccessor`. *Proof:* `CartOwnerAccessorTests` (CRT-9).
- [x] **C7** Endpoints `GET /api/cart`, `POST/PUT/DELETE /api/cart/lines...` with origin check and size limit; matrix rows. *Proof:* `CartEndpointsTests` incl. "prices in the body are ignored" (CRT-6), authorization matrix test.
- [ ] **C8** `MergeGuestCartCommand` registered as `ICustomerSignedInHook`; sign-in clears the guest cookie. *Proof:* `CartMergeHookTests`, `CustomerAuthEndpointsTests.SignIn_MergesGuestCart` (CRT-3).
- [ ] **C9** Expiry: purge job for expired carts. *Proof:* `CartPurgeJobTests` (CRT-10).
- [ ] **C10** Storefront `features/cart/carts` (NgRx store, cart page, mini-cart drawer, quantity stepper, empty state, live region), real Add to bag on the product page, same-origin `/customer-api/cart/**` pass-through in `server.ts`, `noindex` and `no-store`, `robots.txt`. *Proof:* store and component specs, `cart.e2e.spec.ts` with axe (CRT-1, 2, 4, 7).

## totals

- [ ] **T1** `ShippingPolicy`, `DiscountRule`-free `OrderTotals.Calculate` as one pure function (rounding half away from zero, never negative, shipping once per order, deterministic). *Proof:* `OrderTotalsTests` incl. property-style cases (TOT-1, 2, 4, 6, 7).
- [ ] **T2** `SHIP_CITY_RATE` and `SHIP_SETG` tables, seed (zero default), commands and queries, health warning while the default is zero in production. *Proof:* `ShippingRateTests`, `ShippingMigrationTests` (TOT-5).
- [ ] **T3** Admin endpoints `/api/shipping/...` with `shipping.manage`; matrix rows. *Proof:* `ShippingEndpointsTests`, authorization matrix test.
- [ ] **T4** `ICheckoutPricing` in `Cart.Contracts` (priced cart, city, method) used by the cart read and, later, by Orders. *Proof:* `CheckoutPricingTests` (TOT-3).
- [ ] **T5** Storefront `features/cart/totals` summary block (subtotal, discount, shipping, total; "calculated at checkout" until a city is chosen). *Proof:* component specs.
- [ ] **T6** Admin `features/cart/shipping` (city rate table with search and inline edit, defaults form), `*hasPermission`. *Proof:* component and store specs, `shipping.e2e.spec.ts`.

## discount-codes

- [ ] **D1** `DiscountCode` and `DiscountRule.Evaluate` with every rejection reason (generic `Unknown` for unknown, mistyped, deactivated). *Proof:* `DiscountRuleTests` (DSC-1 to 7, 9, 12).
- [ ] **D2** `DSCT_CODE`, `DSCT_SCOP`, `DSCT_RDMP`, lookups and migration. *Proof:* `DiscountMigrationTests`, naming and glossary tests.
- [ ] **D3** Apply and remove on the cart, second code refused, code dropped with a notice when rules stop holding. *Proof:* `ApplyDiscountTests` (DSC-4, 13).
- [ ] **D4** `discount-apply` rate limiter (10 per 15 minutes per address and per owner). *Proof:* `DiscountRateLimitTests` (DSC-8).
- [ ] **D5** Redemption `HoldAsync` / `ReleaseAsync` (row lock, caps, per-customer limit, guests by mobile hash) and the sweeper. *Proof:* `RedemptionConcurrencyTests` against PostgreSQL: two holds for the last redemption, one wins (DSC-10); `SweepHeldRedemptionsTests`.
- [ ] **D6** Event handlers `OrderPlacedEvent` (confirm, empty cart) and `OrderCancelledEvent` (release) through `OrderEventMapper`, behind the inbox. *Proof:* `RedemptionEventHandlerTests` incl. redelivery.
- [ ] **D7** Admin endpoints `/api/discounts` create, change, deactivate, list (paged), audited; `discounts.manage`. *Proof:* `DiscountAdminEndpointsTests`, `DiscountAuditTests` (DSC-11), matrix test.
- [ ] **D8** Storefront code box (cart and checkout) with specific inline reasons; admin `features/cart/discount-codes`. *Proof:* component specs, `discount.e2e.spec.ts`.

## Close the loop

- [ ] **L1** Architecture tests green (only the known `zz-live-demo` failure); full solution build with 0 warnings; storefront and admin unit tests green; e2e for cart, shipping and discounts green on the docker secure stack.
- [ ] **L2** Spec "As built" sections updated; Jira issues created and linked in the spec headers; `docs/specs/README.md` statuses.
- [ ] **L3** Rebuild the docker stack and tell the owner the URLs and what to try (add to bag, change quantity, apply a code, set a city rate in the admin).
