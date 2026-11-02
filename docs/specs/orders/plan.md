# Orders: module plan (Sprints 12 and 13)

- **Specs:** [orders](orders/spec.md) · [checkout](checkout/spec.md) · [payments](payments/spec.md) · [cod-confirmation](cod-confirmation/spec.md) · [order-queue](order-queue/spec.md) · [order-lookup](order-lookup/spec.md) · **Module README:** [README.md](README.md) · **Reservations:** [warehouse/reservations](../warehouse/reservations/plan.md)
- **ADRs:** [0024 three modules](../../adr/0024-cart-orders-notifications-modules.md) · [0025 placement and reservation](../../adr/0025-order-placement-and-stock-reservation.md) · [0027 payment gateway port](../../adr/0027-payment-gateway-port-and-webhooks.md) · [0026 notifications and WhatsApp](../../adr/0026-notification-delivery-and-whatsapp.md) (the COD reply) · [0006 business numbers](../../adr/0006-business-number-generation.md) · [0011](../../adr/0011-security-and-coupling-baseline.md) · [0016 audit](../../adr/0016-audit-trail-through-the-outbox.md)
- **Status:** Plan approved by the owner with all recommendations (2026-11-02); tasks written. Owner answers (2026-11-02): payment methods are Cash on Delivery, Easypaisa, JazzCash and cards; one aggregator (name open); nothing ever blocks an order; no COD cap or block, flags only; customer may cancel after pick, before dispatch; order look-up ships in S13; COD can also be confirmed from WhatsApp, the page and the email (COD-12 to COD-19); all recommendations approved. **Still open:** which aggregator (Q-PAY-1), Terms and Privacy text (Q-CHK-5), the delivery-instruction field (Q-CHK-3; plan assumes one optional 200-character field).

## Module layout

`Romp.Modules.Orders.{Domain,Application,Infrastructure,Contracts}`, tests `Romp.Modules.Orders.Tests`. `OrdersModule : IModule`, DbContext `OrdrDbContext` (schema `ORDR`), `AddOutboxModule("ORDR", …)`, inbox `ORDR.INBX`.

| Feature | API folder | Storefront web | Admin web | Sprint |
|---|---|---|---|---|
| orders | `Orders/` | `features/orders/confirmation` (order placed page) | n/a | S12 |
| payments | `Payments/` | `features/orders/payment-return` ("confirming payment" page) | n/a | S12 |
| checkout | `Checkout/` | `features/orders/checkout` | n/a | S12 |
| cod-confirmation | `CodConfirmation/` | `features/orders/cod-confirm` (the signed-link page) | `features/orders/cod-confirmation` (due-now calls, flags) | S12 |
| order-queue | `OrderQueue/` | n/a | `features/orders/order-queue` | S13 |
| order-lookup | `OrderLookup/` | `features/orders/order-lookup` | n/a | S13 |

Build order (README): orders (placement core) → [reservations](../warehouse/reservations/plan.md) → payments (COD first, online behind the port) → checkout → cod-confirmation → order-queue → order-lookup.

## Domain model

- **`Order`** aggregate. Identity: surrogate id, **order number** (ADR 0006 shape with prefix `RMP` and a Luhn check digit: `RMP-2026-00123-4`; allocated by the atomic upsert on `ORDR.ORDR_NO_SEQ`; a typo is caught by the check digit, and guessing the next number is harmless because look-up needs a second factor, Q-ORD-3), **placement key** (unique). Snapshots taken at placement and never rewritten: contact (name, email, normalised mobile, WhatsApp opt-in flag and time), delivery address (recipient, phone, lines, city and province ids and names, postal code, optional note), lines (variant, product, SKU, name, colour, size, unit price, quantity, line total), amounts (subtotal, discount and code, shipping fee, total), payment method.
- **Status** (lookup, Q-ORD-4): `PNDC` Pending Confirmation (COD), `CNFD` Confirmed, `PYPN` Payment Pending (online), `PAID` Paid, `CNCL` Cancelled, `DSPT` Dispatched, `DLVD` Delivered. Dispatched and Delivered are placeholders written by S15. Allowed moves (a transition table in the domain, tested exhaustively): PNDC → CNFD | CNCL; PYPN → PAID | CNCL; PAID → CNFD-equivalent fulfilment states later; CNFD → CNCL | DSPT; PAID → CNCL | DSPT; DSPT → DLVD. Cancelling a Dispatched or Delivered order is refused with a message pointing to returns (ORD-10). Cancelling after pick but before dispatch is allowed (owner): the event carries the placement key; Warehouse releases the reservation and (S14) creates the put-back task.
- **`OrderStatusHistory`**: append-only rows (from, to, reason code, actor kind and id: staff id, `customer`, `system`, `gateway`; time; short note).
- **`Payment`** (one per order for now): method (`COD`, `EASY`, `JAZZ`, `CARD` lookup), status (`INIT`, `PEND`, `PAID`, `FAIL`, `EXPR`, `REVW`), amount, gateway reference (unique when present), window expiry.
- **`CodConfirmation`** state on the order: due time, attempts (number, outcome, staff, time, note), outcome lookup (`CONF` confirmed by staff, `NOAN` no answer, `REFU` refused, `CWAP` confirmed by customer on WhatsApp, `CWEB` confirmed by customer on the page or email link, `CCAN` cancelled by customer on WhatsApp), reminder sent time, first-order and high-value flags, repeat-refuser flag.
- **`CodPolicy`** (pure): first call due `FirstCallDelayMinutes` (60) after placement; next attempt `RetryGapMinutes` (120) after a no-answer; `MaxAttempts` 2; after the last no-answer, `FinalWaitHours` (24) before auto-cancel, during which the customer can still confirm themselves; the WhatsApp request goes out `WhatsAppConfirmAfterMinutes` (60) after placement only if opted in and still unconfirmed. Everything is configuration (COD-9, FR-32).
- **`CodRisk`** per normalised mobile: refusal and no-show counts, flagged when `RefusalFlagThreshold` (2) is reached; flags only, never a block (COD-5, COD-6). Customer cancellations on WhatsApp and the page do not count as refusals (COD-15).
- **`OrderPlacement`** domain service: given a `PricedCart`, contact, address, method and key, produces the order or the list of problems; no I/O.
- **`LookupThrottle`** (pure, same shape as Customers' `SignInThrottle` but its own code in this module): CAPTCHA from the 3rd failure, rate limit 20 attempts an hour per address (Q-OLK-3).

## Application layer

| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `PlaceOrderCommand` | owner, `Idempotency-Key`, contact, address (saved id or new), method, city, code | order number and confirm token, or refusal (stock short, code rejected, empty cart) | ORD-1 to 8, 11, 12; CHK-1 to 8, 10, 12 |
| `GetCheckoutSummaryQuery` | owner, city, method | priced cart, available methods, saved addresses | CHK-4, 9; TOT-* |
| `ListPaymentMethodsQuery` | n/a | methods with COD-only when the circuit is open | PAY-7 |
| `StartOnlinePaymentCommand` | order number | redirect URL from the port | PAY-1 to 3 |
| `HandlePaymentWebhookCommand` | provider, raw body, headers | state change or rejection | PAY-1, 4, 5, 8, 12 |
| `ReconcilePaymentsJob` | n/a | completes lost callbacks | PAY-6 |
| `ExpireUnpaidOrdersJob` | n/a | cancels past the window | PAY-11 |
| `CancelOrderCommand` | order, reason, actor (staff or customer) | cancelled order | ORD-9, 10; OQ-4 |
| `RecordCodOutcomeCommand` | order, outcome, actor | new state | COD-3, 4, 8 |
| `CustomerRespondsToCodCommand` | token or `Orders.Contracts.ICodCustomerResponses` | confirm or cancel once | COD-12, 14 to 16, 19 |
| `CodConfirmationJob` | n/a | auto-cancel; raises reminder events | COD-2, 9, 13 |
| `ListOrdersQuery` / `GetOrderQuery` | filters, page | staff list and detail (constant queries) | OQ-1, 7, 9 |
| `ClearCodFlagCommand` | mobile | cleared flag (audited) | COD-10 |
| `LookupOrderQuery` / `ViewOrderByLinkQuery` | number + mobile (+ CAPTCHA) / signed token | masked status view or generic mismatch | OLK-1 to 7 |

**Placement follows ADR 0025 exactly**: key lookup → `ICheckoutPricing` → discount hold → stock reserve → one `Orders` transaction (order, lines, history, payment, outbox event) → confirm by event. The handler holds no foreign transaction.

## API endpoints

| Method | Route | Auth / role | Ownership check (SEC-05) |
|---|---|---|---|
| GET | `/api/orders/checkout?cityId=&method=` | Public (cart owner from cookie) | cart is the caller's |
| GET | `/api/orders/payment-methods` | Public | n/a |
| POST | `/api/orders` | Public + origin check + `Idempotency-Key` header | cart is the caller's; saved address checked against the signed-in customer through `ICustomerAddressQueries` (another customer's address id behaves as not found) |
| POST | `/api/orders/{number}/pay` | Public + signed order token | token binds order number |
| POST | `/api/orders/payments/webhook/{provider}` | anonymous, signature-gated, rate-limited | n/a (SEC-10) |
| POST | `/api/orders/cod/confirm`, `/api/orders/cod/cancel` | signed token (purpose `cod-confirm`) | token binds one order |
| POST | `/api/orders/lookup` | Public + CAPTCHA after failures + rate limit | number + mobile second factor |
| GET | `/api/orders/view?token=` | signed token (purpose `order-view`) | token binds one order |
| GET | `/api/admin/orders`, `/api/admin/orders/{id}` | `orders.view` | staff |
| POST | `/api/admin/orders/{id}/cod-outcome`, `/cancel`, `/clear-flag` | `orders.manage` | staff; optimistic concurrency (`xmin`, OQ-8) |

The signed-in customer's own order history is S15 (FR-36). Responses on the public endpoints never include the full address or phone (masked), and a wrong number and a wrong mobile return the same body, status and similar timing (OLK-2).

## Events (ids only; `Orders.Contracts`, schema version 1)

| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|
| `OrderPlacedEvent` (order id, number, placement key, method code, cart ref) | Orders | Warehouse (confirm reservation), Cart (confirm redemption, empty cart), Notifications (confirmation for COD) | yes |
| `OrderPaidEvent` | Orders | Notifications (confirmation for online orders, OCF-3) | yes |
| `OrderCancelledEvent` (order id, number, placement key, reason code, was-picked flag) | Orders | Warehouse (release), Cart (release), Notifications (cancel notice) | yes |
| `CodConfirmationRequestedEvent` (order id, due time) | Orders | Notifications (WhatsApp confirm request when opted in) | yes |
| `OrderConfirmedEvent` | Orders | later sprints (fulfilment) | yes |
| `OrderPaymentSuspectEvent` (order id; amount mismatch or bad webhook) | Orders | staff alert path (log and counter) | yes |

No event carries a name, address, phone, email or note (ADR 0011). Notifications loads what it needs through `Orders.Contracts.IOrderNotificationQueries` when it sends.

## Contracts (additive)

| Need | Provider | Change |
|---|---|---|
| Priced cart, redemption hold and release | `Cart.Contracts` | **new** (cart plan) |
| Reserve and release stock | `Warehouse.Contracts.IStockReservations` | **new** (reservations plan) |
| Saved addresses of the signed-in customer | `Customers.Contracts.ICustomerAddressQueries` | exists |
| Provinces, cities and "city belongs to province" | `Reference.Contracts.IGeographyQueries` | exists |
| Current customer id | `Customers.Contracts.ICurrentCustomerAccessor` | **new** (ADR 0024) |
| CAPTCHA | `Romp.BuildingBlocks.Captcha` | **moved** (ADR 0024) |
| Signed links, origin check | `Romp.BuildingBlocks.Web` | **new / moved** (ADR 0024) |
| What Notifications may read about an order; a customer's reply to the COD request | `Orders.Contracts.IOrderNotificationQueries`, `ICodCustomerResponses` | **new**, provided here |

## Data (schema `ORDR`, names per `docs/db/naming.md`)

| Table | Notes |
|---|---|
| `ORDR_MAIN` | `ORDR_NO` unique, `PLCE_KEY` unique, `ORDR_STS_ID`, `CUST_ID` null, contact and delivery snapshot columns, `WHTS_OPTN_IND` and `WHTS_OPTN_DTE`, amounts `numeric(12,2)`, `PAY_METH_ID`, `COD_DUE_DTE`, `COD_RMND_DTE`, flags `FRST_ORDR_IND`, `HI_VAL_IND`, `RFSL_FLG_IND`, `PLCD_DTE`; **partitioning key noted: `PLCD_DTE` by month (BRD §12.6)**, not applied until volume asks; indexes on status plus date, on `CUST_ID`, on `CONT_PHON`, on `COD_DUE_DTE` where pending |
| `ORDR_LINE` | `ORDR_ID` FK, snapshot columns, unique `(ORDR_ID, VRNT_ID)` |
| `ORDR_HIST` | append-only (trigger forbids update and delete, as the stock ledger) |
| `ORDR_NO_SEQ` | ADR 0006 counter |
| `PYMT_MAIN`, `PYMT_WBHK` | payment and the webhook log (provider event id unique, `SGNT_VRFD_IND`, body hash, received and processed times) |
| `COD_ATMP` | order, attempt number, outcome lookup, staff id, time, note |
| `COD_RISK` | normalised mobile unique, counts, flag, cleared by and when |
| `LOOK_ATMP` | look-up attempts (address hash, success flag, time; indexed, purged daily like `CUST_LGIN_ATMP`) |
| Lookups (standard shape, seeded in the migration): `ORDR_STS_LKP`, `PYMT_METH_LKP`, `PYMT_STS_LKP`, `COD_OTCM_LKP`, `CNCL_RSN_LKP` (`STAF`, `CODN` no answer, `CODR` refused, `CUST`, `PYFL` payment failed, `PYEX` payment expired) |
| `OUTB_MSG`, `INBX` | module |

Audit columns and `xmin` on every transactional table; foreign keys declared and indexed inside the schema; no cross-schema foreign keys (variant, product, city, province and customer ids are plain references). The mobile number is stored in clear text on the order and in `COD_RISK` because staff must call it; access is limited by permission and it is never logged or put in an event (SEC-25).

**New glossary words** (to add with the migration): `ORDR`, `PYMT`, `WBHK`, `COD`, `ATMP`, `RISK`, `LOOK`, `PLCE`, `PLCD`, `RMND`, `RFSL`, `HI`, `FRST`, `OTCM`, `RSN`, `CNCL`, `HIST`, `SGNT`, `VRFD`, `CONT`, `DLVR`, `PSTL`, `WHTS`, `OPTN`, `METH`, `SEQ`. Reused: `CUST`, `LINE`, `NAME`, `EML`, `PHON`, `CITY`, `PROV`, `STS`, `LKP`, `AMT`, `QTY`, `DTE`, `IND`, `NO`.

## Policies, configuration and the web edge

- Options `Orders:*`, validated at start: `PaymentWindowMinutes` 30, `CodFirstCallDelayMinutes` 60, `CodRetryGapMinutes` 120, `CodMaxAttempts` 2, `CodFinalWaitHours` 24, `WhatsAppConfirmAfterMinutes` 60, `RefusalFlagThreshold` 2, `HighValueFlagAmount` (business sets; flag only), `LinkSigningKey` (secret; a missing or default value stops the host outside Development), `Payments:{Provider}:WebhookSecret`, reconciliation 5 minutes / 10 minutes / 24 hours, circuit breaker 5 failures per minute, 30 seconds half-open.
- **Storefront SSR:** checkout, order-placed, payment-return, COD confirm and look-up pages are `noindex`, `private, no-store`, in `robots.txt`; the browser calls same-origin `/customer-api/orders/**` pass-through routes (cookies and origin forwarded; the webhook path is not proxied through the storefront, the provider calls the API host).
- **Admin:** lazy routes `orders/queue`, `orders/queue/:id`, `orders/cod-due`, behind `*hasPermission`; admin keeps its look.

## Frontend (storefront; design system `CheckoutPage`, `OrderConfirmation`, `Field`, `Button`)

`features/orders/checkout/{containers,components,store,models,services}` with NgRx: `CheckoutPageContainer` (single page, guest by default: contact, address or saved address, delivery summary with the city fee, payment method, WhatsApp opt-in checkbox, discount code box reused from Cart, review and Place order; this is the single-page shape the design system's `CheckoutPage` shows, Q-CHK-2), `AddressPickerComponent`, `PaymentMethodsComponent`, `OrderSummaryComponent`. The place-order button disables on press and the key is generated once per page load (CHK-10). The order-placed page shows the number, the COD next steps and the **Confirm my order** button (COD-12). Terms and Privacy links point at placeholder pages until the business supplies the text (Q-CHK-5). Errors are announced; every field has a label; the page works with the server-rendered form before script runs.

## Security & coupling review

| Area | How this module meets it |
|---|---|
| Authorization | Admin endpoints in `MapSecuredGroup` with `orders.view` / `orders.manage` checked server-side on every request (SEC-08); public endpoints are anonymous by design and bound to the cart owner, a signed token or the number + mobile pair; the webhook is anonymous but signature-gated; matrix test rows for every endpoint; saved-address ids and order ids never resolve across customers (SEC-05, ORD-13) |
| Input validation | FluentValidation on every command: contact lengths and charsets, Pakistani mobile rule (shared normaliser from the accounts feature, copied by contract not by reference to Customers internals: a small shared value object in `BuildingBlocks.Domain`), province and city must exist and match, quantity and amounts never read from the body, `Idempotency-Key` 16..64 chars `[A-Za-z0-9_-]` |
| Output encoding | JSON only; email and WhatsApp text built in Notifications with encoded values; no raw SQL beyond quoted-identifier statements (sequence upsert, status filters, triggers) |
| Outbound calls | the payment gateway only, through `IPaymentGateway`, base URL from configuration, allow-listed provider, timeouts and circuit breaker (ADR 0027); no calls built from user input |
| Resource limits | body 8 KB on place order; lines at most `Cart:MaxLines`; admin lists paged 1..100; webhook body at most 64 KB; rate limits on place order, lookup and webhook; look-up attempts per address per hour |
| Concurrency | unique placement key and order number; conditional reservation statement (ADR 0025); `xmin` on orders for staff conflicts (OQ-8); `FOR UPDATE SKIP LOCKED` on the jobs' claim queries so two instances never process one order twice |
| Idempotency | placement key; webhook idempotent on gateway reference; COD responses idempotent (second tap says "already handled"); jobs idempotent by state; consumers behind the inbox |
| Audit | `ORDR_HIST` append-only with actor; every staff action also through the audit trail (FR-59, ADR 0016); security events logged for bad signatures, mismatched amounts and failed look-ups |
| Data protection | money `decimal`; no personal data in events, logs or errors (SEC-25); public views mask the address and phone; card data never touches Romp (hosted page); gateway body stored only as a hash and the needed fields |
| Secrets and dangerous configuration | webhook secrets and `LinkSigningKey` from secrets, never committed; the host refuses to start outside Development with a missing or default signing key or with the fake gateway enabled |
| Error responses | problem details only; generic look-up mismatch; refusals name the short variants but nothing about other orders |
| New dependencies | `Microsoft.Extensions.Http.Resilience` (MIT, shared with Notifications; version pinned in `Directory.Packages.props` at implementation, at least two weeks old); nothing else |
| Coupling | contracts only; events through one mapper per consumer; Orders references no other module's Application or Infrastructure; architecture tests updated; Notifications depends on Orders, never the reverse |
| Abuse cases | (1) double click or Back and resubmit: one order (key); (2) tampering with prices or the discount: ignored, recomputed; (3) enumerating orders by number: second factor, same response, CAPTCHA, rate limit; (4) forged payment webhook: rejected, logged, no state change; (5) a replayed or altered webhook amount: idempotent, amount compared; (6) forged WhatsApp reply: signature, sender-number match, message match; (7) a mail scanner opening the confirm link: the link opens a page, only a button press confirms; (8) mass fake COD orders: flags and the staff call, nothing blocks (owner decision); (9) reading another customer's saved address by id: not found |

## Risks

- **Placement is the hardest code in the project.** ADR 0025 describes it; the integration tests against PostgreSQL (concurrent last unit, double submit, failure after reserve, sweeper) are written first and are the acceptance gate for S12.
- **Real payments wait** for the owner's aggregator and merchant account; the port, the fake and every flow are tested now.
- **Hold sweeper versus a long outbox outage** (ADR 0025): mitigated and alerting.
- **COD abuse is accepted by owner decision** (no cap, no block); the queue flags are the only control, so staff workload is the cost to watch.
- **Integration tests cannot load locally built DLLs** on the developer machine (Application Control): the Linux SDK container and CI run them, and results say so.
