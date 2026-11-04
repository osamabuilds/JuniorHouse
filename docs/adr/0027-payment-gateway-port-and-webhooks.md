# 0027 — Payment gateway port, signed webhooks and reconciliation

- Status: Proposed (owner approved one aggregator for cards, Easypaisa and JazzCash; which one is still open, Q-PAY-1)
- Date: 2026-11-02

## Context

Launch methods are Cash on Delivery, Easypaisa, JazzCash and debit or credit cards (Q-PAY-2). The owner is evaluating aggregators (Safepay, PayFast or another) and has no merchant account yet. The sprint plan requires every integration to be built behind an interface with a fake first. Payment state must never change on an unverified message (SEC-10), a paid order must never be lost (FR-30), a retry must never charge twice, and a gateway outage must not stop COD sales (SEC-21).

## Decision

1. **A port, `IPaymentGateway`,** in `Orders.Application`: `CreateSessionAsync(order reference, amount, method, return URLs)` returning a hosted-page redirect; `VerifyCallback(raw body, headers)` returning a verified, parsed result or a rejection; `GetStatusAsync(gateway reference)` for reconciliation. One implementation per provider; a **fake** ships first and drives every test and the local stack. The real adapter is written when the owner names the provider; the port is shaped to fit an aggregator that lists several methods under one merchant account, so Easypaisa, JazzCash and cards are values of `PaymentMethod`, not three integrations.
2. **Hosted page only** (Q-PAY-4). No card data, wallet PIN or OTP touches Romp, which keeps PCI scope at SAQ-A. Nothing from the payload except what is needed (gateway reference, amount, status, timestamp) is stored, plus a hash of the raw body (PAY-9).
3. **The webhook decides, the browser return does not** (PAY-10). `POST /api/orders/payments/webhook/{provider}` is anonymous but: verifies the signature over the raw body before parsing; rejects unsigned or invalid ones with 401, stores them with `SignatureVerified = false` and logs a security event; rejects an old timestamp where the provider signs one (PAY-12); is idempotent on the gateway reference, so a repeated delivery changes the order once (PAY-5); compares the amount to the order total and flags a mismatch for review instead of confirming (PAY-8); is rate-limited. The secret is configuration, never committed.
4. **Reconciliation** (FR-30, Q-PAY-3): a background job runs every 5 minutes, takes card payments still pending after 10 minutes (for up to 24 hours), asks `GetStatusAsync`, and completes the order exactly once through the same command the webhook uses.
5. **Circuit breaker** around every gateway call (Q-PAY-5: open after 5 failures in 1 minute, half-open after 30 seconds), using `Microsoft.Extensions.Http.Resilience` (shared with ADR 0026). While open, `GET /api/orders/payment-methods` omits the online methods and says COD only (PAY-7), and closes again after recovery.
6. **Payment window** (Q-RSV-2): an online-payment order holds its reservation for 30 minutes; if still unpaid, the job in `Orders` cancels it as `PAYMENT_EXPIRED` and the normal cancellation event releases stock and the discount (PAY-11).
7. **Order state changes only through commands** (`MarkPaid`, `MarkPaymentFailed`, `ExpirePayment`) that the webhook, the reconciler and the fake all call; there is no second path.

## Alternatives considered

- **Direct JazzCash and Easypaisa merchant APIs plus a card gateway:** three contracts, three secrets, three reconciliation jobs. Possible later behind the same port if fees justify it.
- **Mark paid on the browser return:** forgeable. Rejected by SEC-10.
- **Store the gateway payload in full:** puts payment data in our database for no benefit.

## Consequences

- The sprint ends with COD working end to end and online payment working against the fake; going live with real payments needs only the adapter, the merchant account, the webhook secret and a sandbox run.
- The webhook path and its URL must be registered with the provider; that is a deployment step recorded in the runbook.
