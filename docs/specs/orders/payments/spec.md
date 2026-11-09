# Payments

- **Status:** Approved (owner approved all recommendations, 2026-11-02; plan awaiting approval)
- **Jira:** to be created on approval (sprint S12)
- **BRD sections:** §5.4, §8.4, §9.2, §12.3.4
- **Requirement IDs:** FR-27, FR-30, SEC-10, SEC-21, NFR-09, SEC-25, TC-CHK-11, TC-CHK-12, TC-CHK-13

## Problem & intent
Take card payment safely and COD simply, never mark an order paid on an unverified message, never lose a paid order, and keep selling (COD) when the card gateway is down.

## Requirements (quoted from the BRD)
> **FR-27:** The system shall support Cash on Delivery and card payment (Visa/Mastercard) at minimum, routed through a PCI-DSS-compliant, SSL-encrypted gateway.

> **FR-30:** If a payment gateway callback fails or times out after a successful charge, the system shall reconcile via a scheduled/retry job rather than losing the paid order or double-charging on a user retry.

> **SEC-10:** All inbound payment-gateway webhooks/callbacks shall be signature-verified against the gateway's published secret before an order is marked paid; unsigned or invalid-signature callbacks shall be rejected and logged as a security event.

> **SEC-21:** Calls to third-party dependencies (payment gateway, WhatsApp API, courier API) shall be wrapped in a circuit-breaker pattern; if a channel is down, the system shall degrade gracefully (e.g. offer COD-only checkout if the card gateway is unreachable) rather than blocking the entire checkout flow.

> **NFR-09:** Order-placement idempotency (FR-29) and payment reconciliation (FR-30) shall be enforced at the architecture level, not left to client-side prevention alone.

## User flow
BRD §5.4. Card: place order → redirect to the gateway's hosted page → 3-D Secure/OTP → return to Romp → success or decline. The browser return is only a display hint; the signed webhook (and the reconciliation job) decides the order state. COD: see [cod-confirmation](../cod-confirmation/spec.md).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| PAY-1 | Given a successful test card, when the signed webhook arrives, then the payment is authorised and the order is confirmed | TC-CHK-11 |
| PAY-2 | Given a declined card, when the decline arrives, then a clear reason is shown, the cart is preserved and retry is allowed | TC-CHK-12 |
| PAY-3 | Given the gateway does not respond, then the order is not marked paid, the shopper sees a retry / contact-support state, and a retry never charges twice | TC-CHK-13, FR-30 |
| PAY-4 | Given a webhook with a missing or invalid signature, then it is rejected, stored with `SignatureVerified = false`, a security event is logged, and the order state does not change | SEC-10 |
| PAY-5 | Given the same valid webhook delivered twice, then the order changes state once (idempotent on the gateway reference) | NFR-09 |
| PAY-6 | Given a charge succeeded but the callback was lost, when the reconciliation job runs, then it asks the gateway for the status and completes the order exactly once | FR-30 |
| PAY-7 | Given the card gateway fails repeatedly, when the circuit opens, then checkout offers COD only, with a message, and closes again after the gateway recovers | SEC-21 |
| PAY-8 | Given a webhook amount different from the order total, then it is flagged for review and the order is not confirmed (new) | SEC-09 |
| PAY-9 | Given raw gateway payloads, then only a hash and the needed fields are stored; never card numbers, never logged | SEC-25, §12.3.4 |
| PAY-10 | Given the browser returns from the gateway before the webhook, then the page shows "confirming payment" and updates when the state changes, never "paid" on the return alone | SEC-10 (new) |
| PAY-11 | Given an unpaid card order past the payment window, then the order expires and the reservation is released | RSV-6 (new) |
| PAY-12 | Given a webhook replayed with an old timestamp, then it is rejected when the gateway supports timestamps (new) | SEC-10 |

### Added for S12b (2026-11-09, awaiting owner approval)

| ID | Given / When / Then | Source |
|---|---|---|
| PAY-13 | Given the gateway port, then it also exposes a refund call (amount, original gateway reference, idempotency key) implemented by the fake gateway only. Nothing in S12b calls it: no refund screen, no refund rule. S17 uses it. (new) | FR-SC-19 (prepared, built in S17) |
| PAY-14 | Given the payment-methods request, then it lists only methods that are enabled in configuration and whose circuit is closed; with no online method enabled it returns COD only. (new) | SEC-21 |
| PAY-15 | Given any environment other than Development, then the host refuses to start with the fake gateway enabled; with no real gateway configured, online methods stay off and the site sells COD only. (new) | ADR 0011 |
| PAY-16 | Given the same refund request sent twice with one idempotency key, then the fake gateway records one refund. (new) | NFR-09 |

## Non-functional constraints
PCI scope stays at SAQ-A: hosted payment page only, no card data touches Romp. Webhook secrets from configuration/secrets, never committed; the webhook endpoint is anonymous but signature-gated and rate-limited, and is excluded from the CORS/cookie assumptions. The circuit breaker covers the gateway call and the status query. Everything runs locally against the fake gateway.

## Out of scope
Refund rules, screens and refund jobs (S17; only the port call and the fake come in S12b, PAY-13), saved cards, BNPL (post-launch), gift cards (P2). Bank transfer is covered by Q-PAY-2.

## Open questions
- [ ] **Q-PAY-1 Which provider(s)?** *Owner approved the recommendation (2026-11-02): ONE aggregator covering cards, Easypaisa and JazzCash under one merchant account. Which aggregator (Safepay, PayFast or another) is still the owner's choice; the plan builds only the `IPaymentGateway` port and a fake, so the choice does not block the sprint.* Options researched 2026-11-02 (vendor pages, verify terms directly): (a) one aggregator (Safepay, PayFast) covering cards plus both wallets under one merchant account and one integration; (b) direct JazzCash and Easypaisa merchant gateways plus a separate card gateway (bank or aggregator), i.e. three integrations. Recommendation: (a) for launch. The real adapter waits for the merchant account; the port, fake adapter and tests do not.
- [x] **Q-PAY-2 Methods at launch** (owner, 2026-11-02): Easypaisa, JazzCash, Cash on Delivery, and bank / debit cards. Wallets (Easypaisa, JazzCash) are therefore in scope for S12, not post-launch: each is a payment method behind the same `IPaymentGateway` port, with its own signed callback (SEC-10), reconciliation (FR-30) and circuit breaker (SEC-21). Bank/debit cards: via an aggregator or bank gateway, see Q-PAY-1. Clarified (owner): "bank" means debit and credit cards only, no manual bank transfer.
- [x] **Q-PAY-3 Reconciliation schedule** (every 5 minutes for payments older than 10 minutes, for 24 hours)? Recommendation as stated. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-PAY-4 Hosted page or embedded fields** (depends on the gateway). Recommendation: hosted page. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-PAY-5 Circuit breaker thresholds** (open after 5 failures in 1 minute, half-open after 30 seconds)? Recommendation as stated. **Approved (owner, 2026-11-02: approve all recommendations).**

## As built (S12b, 2026-11-09)

Everything in the acceptance table is built and tested against the **fake gateway**; online methods stay switched off unless `Orders:GatewayProvider` is set. Where the build differs from the plan or ADR 0027:

| Topic | What was built | Why |
|---|---|---|
| Tables | `PAYM_MAIN` (one row per order), **`PAYM_ATMP`** (one row per gateway session, new), `PAYM_WBHK` (every message received) and three lookups `PAYM_STS_LKP`, `PAYM_RVW_LKP`, `WBHK_OTCM_LKP`. The plan wrote `PYMT_*`; the glossary's `PAYM` wins. New glossary words: `RVW`, `PAID`, `VRFD`, `URL`. | An attempt table lets a late message about an old try still be matched, so a second charge is seen and flagged (PAY-3). |
| Exactly once | Every message about a payment is handled under a database row lock on that payment (`SELECT ... FOR UPDATE`), inside the Orders transaction. The webhook, the reconciliation job and the expiry job all end in one place (`PaymentSettler`), and `Payment.Settle` decides whether money counts. | A repeated or racing message, or two jobs at once, cannot count the money twice (PAY-5, FR-30). Proved with 40 simultaneous identical callbacks, 40 different callbacks, and a randomised run of every operation. |
| One page per order | `StartOnlinePayment` hands back the open session instead of making a second one; the gateway call is idempotent on the attempt reference (`<order number>-A<n>`); the database allows each attempt number once, so two racing starts save one. A retry opens a new attempt only after the earlier one failed. | No double charge on a double click, a timeout and retry, or two tabs (PAY-3). |
| Duplicate charge | If a second attempt is somehow charged after one already paid, it is recorded, the payment is flagged `DUPL` and staff get `OrderPaymentSuspectEvent`; the order is not confirmed twice. The refund itself is S17. | The gateway, not Romp, makes the charge; the safety net is to see it. |
| Decline | The order stays Payment Pending and may be retried up to `Orders:PaymentMaxAttempts` (3) while the window is open; the last decline cancels it as `PYFL` and gives stock and discount back. | PAY-2 says "cart preserved and retry allowed": the order keeps its stock reserved for the window instead of the shopper re-entering everything. |
| Circuit breaker | Own small `GatewayCircuit` (injected clock, so tested without waiting) with the approved numbers (5 failures in 60 s, one trial after 30 s) around create-session, status and refund. ADR 0027 named `Microsoft.Extensions.Http.Resilience`; that package wraps an `HttpClient`, and the fake and the future adapters are behind a port, so the breaker sits on the port. | Same behaviour, testable, and not tied to one HTTP stack. |
| Methods offered | `GET /api/orders/payment-methods` and the checkout summary list only methods that are enabled, with a gateway switched on and its circuit closed. The summary also says when payments are paused, so the page can tell the shopper (PAY-7, PAY-14). | |
| Production guard | `OrdersGatewayOptionsValidator` (run at start) refuses `fake` outside Development and Testing; with no provider the host starts and sells Cash on Delivery only (PAY-15). `OrdersOptions.IsValid` refuses online methods with no gateway. | |
| Refund call | `IPaymentGateway.RefundAsync` exists on the port and the fake (idempotent on its key, PAY-13, PAY-16). Nothing calls it yet. | S17 |
| Expiry | `ExpireUnpaidOrdersJob` runs every minute and **asks the gateway about every open attempt first**: a paid order is never cancelled, and when the gateway cannot answer the order is left for the next run. Reconciliation runs every 5 minutes. Both work through every due item in batches of 50, up to 1,000 per run. | A paid order must never be cancelled (FR-30). |
| Late money | Money arriving for an order that has expired or been cancelled is held for review (`LATE`) with an alert; the order is not reopened. | A refund is due (S17). |
| Webhook | `POST /api/orders/payments/webhook/{provider}`: anonymous, rate limited, 64 KB cap, signature checked over the raw bytes first, `401` for a missing, wrong or stale signature (recorded as unverified, with a security log line), `200` for a repeat. Only the provider that is switched on answers; any other name is `404`. | SEC-10, PAY-4, PAY-5, PAY-12 |
| Customer pages | `/checkout/pay` opens the hosted page; `/checkout/payment-return` follows the stored state every 3 s (up to about 2 minutes) and says paid, failed (with a retry), expired, cancelled, under review, or "not heard back yet". It shows "paid" only when the server says so (PAY-10). The signed token in the address names one order. | |
| Demo page | With the fake gateway, `/customer-api/orders/payments/fake/<ref>` is a pretend hosted page with buttons: Pay now, Decline the card, Pay but lose the callback (to watch reconciliation work). | Lets the owner try every path locally. |

**Not built here:** the real gateway adapter (waits for the merchant account and the aggregator choice, Q-PAY-1); the refund flow (S17).

**Still open:** Q-S12B-2 (staging sells Cash on Delivery only until a real gateway is configured, as the spec recommends) and Q-PAY-1.
