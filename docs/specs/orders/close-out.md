# Orders close-out (Sprint S12b)

- **Status:** Implemented 2026-11-09. The owner was away and asked for every task to be done, so the recommendations below were applied to the open questions; they stay open for the owner to change.
- **Jira:** to be created on approval (S12 close-out)
- **BRD sections:** §5.4, §8.4, §9.2
- **Requirement IDs:** FR-23, FR-27, FR-30, FR-SC-09, SEC-05, SEC-10, SEC-21, NFR-09, NFR-16, TC-CHK-11, TC-CHK-12, TC-CHK-13, TC-CHK-18

## Problem & intent
Sprint S12 (checkout) is half built. Placing orders, COD confirmation, stock reservation, the admin queue and guest look-up work. Online payment, a few safety checks and some pages are missing, so S12 cannot be called done and later sprints (fulfilment, returns) have nothing to take refunds through. This sprint finishes S12 against the fake gateway; the real gateway is wired in later when the owner has an account (see [third-party integrations](../../roadmap/third-party-integrations.md)).

This is not a new feature. The behaviour is already specified and approved in the feature specs linked below; this page lists what is being finished, adds nothing new except where marked `(new)`, and records what is still open.

## Scope: what gets built

| Item | Feature spec | Acceptance criteria | Task |
|---|---|---|---|
| Fake gateway, payment entities, tables | [payments](payments/spec.md) | PAY-9 | Y1 |
| COD payment row | [payments](payments/spec.md) | COD placement | Y2 |
| Start an online payment, return page state | [payments](payments/spec.md) | PAY-3, PAY-10 | Y3 |
| Signed, idempotent webhook | [payments](payments/spec.md) | PAY-1, 4, 5, 8, 9, 12 | Y4 |
| Decline and retry without double charge | [payments](payments/spec.md) | PAY-2, 3 | Y5 |
| Reconciliation job | [payments](payments/spec.md) | PAY-6 | Y6 |
| Circuit breaker, COD-only fallback | [payments](payments/spec.md) | PAY-7, PAY-14 | Y7 |
| Expire unpaid orders | [payments](payments/spec.md) | PAY-11 | Y8 |
| Webhook endpoint, payment-methods endpoint, matrix rows | [payments](payments/spec.md) | PAY-4, PAY-14 | Y9 |
| Refund call on the port, fake only | [payments](payments/spec.md) | PAY-13, PAY-16 (new) | Y1 |
| Production guard for the fake gateway | [payments](payments/spec.md) | PAY-15 (new) | Y9 |
| A customer reads only their own orders, paged | [orders](orders/spec.md) | ORD-13, ORD-14 | P8 |
| Session expiry and Back-and-resubmit | [checkout](checkout/spec.md) | CHK-5, CHK-10 | H3 |
| Placeholder Terms and Privacy pages | [checkout](checkout/spec.md) | Q-CHK-5, NFR-16 | H5 |
| Cart discount-hold confirmation (the cart task still open) | [carts](../cart/carts/spec.md) | existing | cart D6 |
| Close-out: architecture tests, build with no warnings, e2e on docker, "As built" sections, Jira | n/a | n/a | Z1, Z2 |

The wallets (Easypaisa, JazzCash) and cards are three payment methods behind the one port (approved in Q-PAY-2). The fake gateway simulates all three.

## Clean-up work (not features, no spec needed)
Done in the same sprint, each as its own small change:
1. Push and merge `SCRUM-148-seo` after reading the CI Lighthouse job (do not trust local Lighthouse numbers).
2. Commit and open a PR for the `SCRUM-order-lookup` work; rename the branch once it has a Jira key.
3. Fix the 8 e2e tests already failing on `main`, including the real 1024 px horizontal overflow (40 px).
4. Create the missing Jira issues for orders, cart and notifications.

## Acceptance criteria
The criteria are the ones in the table above, taken unchanged from the feature specs (PAY-1..12, ORD-13, ORD-14, CHK-5, CHK-10), plus the four new ones PAY-13..16. Sprint is done when:
- every Y, P8, H3, H5, cart D6 and Z task is ticked and has its named test;
- a card, Easypaisa and JazzCash order can be placed locally with the fake gateway (success, decline, timeout, lost callback), and the order ends in the right state exactly once;
- turning the fake gateway "down" makes checkout offer COD only and recovers on its own;
- the host refuses to start outside Development with the fake gateway enabled;
- backend tests, architecture tests and web tests pass in Docker (the local Windows policy blocks the integration tests; CI is the verification, and I say so when reporting).

## Non-functional constraints
As in the payments spec: hosted page only (SAQ-A), no card data stored or logged, webhook secrets from secrets, anonymous but signature-gated and rate-limited webhook endpoint, circuit breaker around every gateway call.

## Out of scope
Refund screens, rules and jobs (S17). The real gateway adapter (waits for the merchant account and the choice of aggregator). Online payment on the live site: it stays off until a real gateway is configured.

## Open questions
- [x] **Q-S12B-1 Refund call now?** *Applied (recommendation, owner away, 2026-11-09): added to the port and the fake only (PAY-13, PAY-16).* Adding the refund method to the port in S12b is a small change that spares S17 changing the port and its fake. It does depart from the payments spec's "Refunds out of scope". Recommendation: yes, port and fake only (PAY-13). Your decision.
- [x] **Q-S12B-2 Staging before a real gateway exists.** *Applied (recommendation, owner away, 2026-11-09): staging sells Cash on Delivery only until a real gateway is configured; the demo flag was not added.* With PAY-15, a staging server cannot run the fake gateway, so staging sells COD only and online payment cannot be tried there until the real gateway is configured. Recommendation: accept this; try online payment locally with the fake. The alternative is a deliberate "demo" flag allowed on staging only, which weakens the guard. Your decision.
- [ ] **Q-CHK-3 Delivery instruction field** (still unanswered since 2026-11-02; built as the plan assumed, see checkout "As built"). The plan assumes one optional field of up to 200 characters. Confirm or drop it.
- [ ] **Q-CHK-5 Terms and Privacy text.** Built: a placeholder notice until the owner publishes the real pages (see checkout "As built"). The text itself is still needed from the business. Who writes it, and when? (NFR-16 requires a disclosed return policy and clear pricing before launch.)
- [ ] **Q-PAY-1 Which aggregator** (carried over, does not block this sprint).
