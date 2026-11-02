# Payments

- **Status:** Draft
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

## Non-functional constraints
PCI scope stays at SAQ-A: hosted payment page only, no card data touches Romp. Webhook secrets from configuration/secrets, never committed; the webhook endpoint is anonymous but signature-gated and rate-limited, and is excluded from the CORS/cookie assumptions. The circuit breaker covers the gateway call and the status query. Everything runs locally against the fake gateway.

## Out of scope
Refunds (S17), saved cards, BNPL (post-launch), gift cards (P2). Bank transfer is covered by Q-PAY-2.

## Open questions
- [ ] **Q-PAY-1 Which provider(s)?** Options researched 2026-11-02 (vendor pages, verify terms directly): (a) one aggregator (Safepay, PayFast) covering cards plus both wallets under one merchant account and one integration; (b) direct JazzCash and Easypaisa merchant gateways plus a separate card gateway (bank or aggregator), i.e. three integrations. Recommendation: (a) for launch. The real adapter waits for the merchant account; the port, fake adapter and tests do not.
- [x] **Q-PAY-2 Methods at launch** (owner, 2026-11-02): Easypaisa, JazzCash, Cash on Delivery, and bank / debit cards. Wallets (Easypaisa, JazzCash) are therefore in scope for S12, not post-launch: each is a payment method behind the same `IPaymentGateway` port, with its own signed callback (SEC-10), reconciliation (FR-30) and circuit breaker (SEC-21). Bank/debit cards: via an aggregator or bank gateway, see Q-PAY-1. Clarified (owner): "bank" means debit and credit cards only, no manual bank transfer.
- [ ] **Q-PAY-3 Reconciliation schedule** (every 5 minutes for payments older than 10 minutes, for 24 hours)? Recommendation as stated.
- [ ] **Q-PAY-4 Hosted page or embedded fields** (depends on the gateway). Recommendation: hosted page.
- [ ] **Q-PAY-5 Circuit breaker thresholds** (open after 5 failures in 1 minute, half-open after 30 seconds)? Recommendation as stated.
