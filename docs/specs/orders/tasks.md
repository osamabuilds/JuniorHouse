# Orders (Sprints 12 and 13): tasks

Ordered, checkable steps; test-first; .NET in the Linux SDK container. Plan: [plan.md](plan.md) (approved 2026-11-02) · ADRs 0024, 0025, 0026, 0027 · Depends on: cart tasks F1 to F7 and T4, D5, D6; [reservations](../warehouse/reservations/tasks.md) R1 to R6. After each sprint's work: rebuild docker and tell the owner what to try.

## Skeleton

- [x] **O1** Projects `Romp.Modules.Orders.{Domain,Application,Infrastructure,Contracts}` and `Romp.Modules.Orders.Tests`, `OrdersModule : IModule`, `OrdrDbContext` (schema `ORDR`), outbox and inbox, solution and `Program.cs` entries. *Proof:* `OrdersMigrationTests`, architecture tests.

## orders (S12)

- [x] **P1** `Order` aggregate, status transition table, history rows, snapshots, order number with check digit (ADR 0006 shape, prefix `RMP`). *Proof:* `OrderTests`, `OrderStatusTransitionTests` (exhaustive), `OrderNumberTests` (ORD-5, 6, 10).
- [x] **P2** `ORDR_MAIN`, `ORDR_LINE`, `ORDR_HIST` (append-only trigger), `ORDR_NO_SEQ`, lookups; migration. *Proof:* `OrdersMigrationTests`, `OrderHistoryAppendOnlyTests`, naming and glossary tests.
- [x] **P3** Contracts: `Orders.Contracts` events (`OrderPlacedEvent`, `OrderPaidEvent`, `OrderCancelledEvent`, `CodConfirmationRequestedEvent`, `OrderConfirmedEvent`, `OrderPaymentSuspectEvent`), `IOrderNotificationQueries` (done; `ICodCustomerResponses` comes with the COD confirmation work). *Proof:* `OrdersContractShapeTests` (ids only, no personal data fields).
- [x] **P4** `PlaceOrderCommand` following ADR 0025: key lookup, `ICheckoutPricing`, discount hold, stock reserve, one transaction, outbox event, compensation. *Proof:* `PlaceOrderTests` (ORD-1, 4, 7, 11, 12), `PlaceOrderFailureTests` (failure after reserve releases both), `PlaceOrderConcurrencyTests` (double submit gives one order, last unit gives one winner: ORD-1, ORD-2, TC-CHK-16, 17, 19).
- [x] **P5** Out-of-stock and code-rejected refusals keep the cart and name the short variants. *Proof:* `PlaceOrderRefusalTests` (ORD-3).
- [x] **P6** Notification outage does not affect placement. *Proof:* `PlaceOrderOutboxTests.NotificationDown_OrderStillPlaced` (ORD-8).
- [ ] **P7** `CancelOrderCommand` (before dispatch; after dispatch refused), emits `OrderCancelledEvent` with reason and was-picked flag. *Proof:* `CancelOrderTests` (ORD-9, 10).
- [ ] **P8** Staff and customer reads paged with constant queries; another customer's order is not found. *Proof:* `OrderQueryTests` incl. query count (ORD-13, 14).

## payments (S12)

- [ ] **Y1** `IPaymentGateway` port, `FakeGateway` (configurable success, decline, timeout, signed callbacks), `PaymentMethod` and `Payment` entities, `PYMT_MAIN`, `PYMT_WBHK` and lookups migration. *Proof:* `PaymentTests`, `PaymentsMigrationTests`.
- [ ] **Y2** COD payment: order placed as Pending Confirmation with a COD payment row. *Proof:* `CodPlacementTests` (PAY COD path, COD-1).
- [ ] **Y3** `StartOnlinePaymentCommand`, return page state ("confirming payment"), never paid on the return alone. *Proof:* `StartPaymentTests`, `PaymentReturnTests` (PAY-10).
- [ ] **Y4** `HandlePaymentWebhookCommand`: signature before parsing, store with `SignatureVerified`, idempotent on gateway reference, amount compared, timestamp checked. *Proof:* `PaymentWebhookTests` (PAY-1, 4, 5, 8, 9, 12).
- [ ] **Y5** Decline keeps the cart and allows retry; retry never charges twice. *Proof:* `PaymentDeclineTests`, `PaymentRetryTests` (PAY-2, 3).
- [ ] **Y6** `ReconcilePaymentsJob` (5 minutes, older than 10 minutes, 24 hours). *Proof:* `ReconcilePaymentsTests` (PAY-6).
- [ ] **Y7** Circuit breaker around the gateway; payment methods endpoint says COD only while open. *Proof:* `PaymentCircuitTests` (PAY-7).
- [ ] **Y8** `ExpireUnpaidOrdersJob` (30 minutes) cancels with reason `PYEX`. *Proof:* `ExpireUnpaidOrdersTests` (PAY-11).
- [ ] **Y9** Webhook endpoint (anonymous, signature-gated, rate-limited, 64 KB cap) and `GET /api/orders/payment-methods`; matrix rows. *Proof:* `PaymentEndpointsTests`, authorization matrix test.

## checkout (S12)

- [x] **H1** `GET /api/orders/checkout` summary and `POST /api/orders` with `Idempotency-Key`, origin check, saved address ownership via `ICustomerAddressQueries`, city and province validation. *Proof:* `CheckoutEndpointsTests` (CHK-1 to 4, 7, 8, 12), `AnotherCustomersAddress_IsNotFound` (SEC-05).
- [x] **H2** Nothing ever blocks an order on account state. *Proof:* `CheckoutNeverBlockedTests` (unverified email, unverified mobile, guest: CHK-6).
- [ ] **H3** Session expiry and Back-and-resubmit. *Proof:* `CheckoutSessionTests` (CHK-5, 10).
- [x] **H4** Storefront `features/orders/checkout` (single page per the design system `CheckoutPage`, saved address picker, city fee shown, payment methods, WhatsApp opt-in checkbox, discount code box, one generated key per page load, disabled button on press), order-placed page, payment-return page, pass-through routes, `noindex`/`no-store`/`robots.txt`. *Proof:* store and component specs, `checkout.e2e.spec.ts` with axe (CHK-1 to 4, 9, 11). (Built: checkout and order-placed pages; the old "Terms and Privacy" links point at routes the content pages supply; discount code box is on the bag, not repeated on checkout.)
- [ ] **H5** Placeholder Terms and Privacy pages until the business supplies text. *Proof:* route spec; noted in the spec "As built".

## cod-confirmation (S12)

- [ ] **D1** `CodPolicy` and `CodRisk` (pure): due times, attempts, auto-cancel rule, flags, reminder timing. *Proof:* `CodPolicyTests`, `CodRiskTests` (COD-2, 5, 6, 9).
- [ ] **D2** `COD_ATMP`, `COD_RISK`, lookups migration. *Proof:* `CodMigrationTests`.
- [ ] **D3** `RecordCodOutcomeCommand` (confirmed, no answer, refused), audited, refusal counts. *Proof:* `RecordCodOutcomeTests` (COD-3, 4, 8, 10).
- [ ] **D4** `CodConfirmationJob`: auto-cancel after the final wait, raises `CodConfirmationRequestedEvent` once per eligible order (opted in, unconfirmed, after the wait). *Proof:* `CodConfirmationJobTests` (COD-2, 13, 17).
- [ ] **D5** Customer confirm and cancel by signed token (page and email button) and by `ICodCustomerResponses` (WhatsApp): once only, "already handled" afterwards, link opens a page and only the button confirms, cancel is not a refusal. *Proof:* `CustomerCodResponseTests` (COD-12, 14, 15, 19), `CodLinkTests` (tampered, wrong purpose, finished order).
- [ ] **D6** Storefront `features/orders/cod-confirm` page. *Proof:* component spec, `cod-confirm.e2e.spec.ts`.
- [ ] **D7** First-order and high-value flags set at placement; cleared when S15 data shows a delivery. *Proof:* `CodFlagsTests` (COD-6, 7).

## order-queue (S13)

- [ ] **Q1** `ListOrdersQuery` and `GetOrderQuery` with filters (status, method, date, COD due now), paged, constant queries, notification status linked. *Proof:* `OrderQueueQueryTests` incl. query count (OQ-1, 7, 9).
- [ ] **Q2** Staff actions (record outcome, cancel with reason, clear flag) with `xmin` conflicts and audit. *Proof:* `OrderQueueCommandTests` (OQ-2, 3, 4, 5, 8).
- [ ] **Q3** Endpoints with `orders.view` / `orders.manage`; phone visible only to those roles and never logged. *Proof:* `OrderQueueEndpointsTests`, authorization matrix (OQ-6, 10).
- [ ] **Q4** Admin `features/orders/order-queue` and `cod-due` screens (dispatch action hidden until S15), `*hasPermission`. *Proof:* component and store specs, `order-queue.e2e.spec.ts`.

## order-lookup (S13)

- [ ] **L1** `LookupThrottle` (pure) and `LOOK_ATMP`. *Proof:* `LookupThrottleTests`, migration test (OLK-3, 4).
- [ ] **L2** `LookupOrderQuery`: normalised mobile compare, constant-time, indistinguishable mismatch, masked view. *Proof:* `LookupOrderTests` (OLK-1, 2, 6, 7), `LookupTimingTests` (same status and body shape).
- [ ] **L3** Signed emailed link opens the status page. *Proof:* `ViewOrderByLinkTests` (OLK-5).
- [ ] **L4** Endpoints with CAPTCHA after failures and rate limit. *Proof:* `LookupEndpointsTests` (OLK-3, 4).
- [ ] **L5** Storefront `features/orders/order-lookup` (Track order page, `noindex`, WCAG). *Proof:* component specs, `order-lookup.e2e.spec.ts` with axe (OLK-9).

## Close the loop

- [ ] **Z1** Architecture tests green (only the known `zz-live-demo` failure); full build with 0 warnings; unit and e2e green on the docker secure stack; spec "As built" sections; Jira issues created and linked.
- [ ] **Z2** After S12: rebuild docker, tell the owner to place a COD order as a guest and a signed-in customer, see Available drop, press Confirm my order. After S13: the queue, look-up and the WhatsApp fake.
