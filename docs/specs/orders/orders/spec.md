# Orders

- **Status:** Approved (owner approved all recommendations, 2026-11-02; plan awaiting approval)
- **Jira:** to be created on approval (sprint S12)
- **BRD sections:** §5.4, §9.1, §12.3.3, §12.8
- **Requirement IDs:** FR-28, FR-29, FR-33, FR-31 (order number), NFR-08, NFR-09, SEC-09, FR-SC-09, TC-CHK-16, TC-CHK-17, TC-CHK-19

## Problem & intent
Placing an order must be exactly-once, must never oversell, must never trust client money, and must leave a faithful record (prices, address, status history) that later changes cannot rewrite.

## Requirements (quoted from the BRD)
> **FR-28:** The system shall re-validate stock at the point of order placement (not only at add-to-cart) and block the order with a clear message if stock was depleted in the interim, resolving concurrent-checkout races deterministically (first successful payment wins).

> **FR-29:** The system shall treat order placement as idempotent: duplicate/rapid-repeat submissions (double-click, browser back-and-resubmit) shall not create duplicate orders or duplicate charges.

> **FR-31 (order number part):** The system shall generate a unique order number and send order confirmation via email and, where the customer has opted in, WhatsApp.

> **FR-33:** The system shall allow order cancellation only while the order has not been dispatched.

> **NFR-08:** Stock-decrement and order-placement shall be transactional/atomic to prevent overselling under concurrency (see FR-28).

> **NFR-09:** Order-placement idempotency (FR-29) and payment reconciliation (FR-30) shall be enforced at the architecture level, not left to client-side prevention alone.

> **SEC-09:** The server shall always recalculate order totals (item prices, discounts, shipping) from its own product/pricing data at payment time; client-submitted prices/totals shall never be trusted or persisted directly.

> **FR-SC-09:** System shall reserve stock on order placement and release the reservation automatically if the order is cancelled before pick is confirmed.

## User flow
Place order (from checkout): the server re-reads the cart, re-prices it, re-checks stock, reserves stock, writes the order, its lines (unit price snapshot), the shipping/contact snapshot and the first status-history row, records the discount redemption, empties the cart, and writes the outbox event, all consistent. The shopper sees the confirmation page with the order number. Cancellation: the customer (account) or staff cancels while allowed; reservations are released; a status row and an event are written.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| ORD-1 | Given a client that sends a key with each submit, when the same key arrives twice (double-click, back-and-resubmit), then one order exists and the second call returns the first order | TC-CHK-16, TC-CHK-19, FR-29 |
| ORD-2 | Given the last unit and two simultaneous placements, when both run, then exactly one succeeds and the other is blocked before payment with a clear message | TC-CHK-17, FR-28 |
| ORD-3 | Given stock depleted between cart and placement, when placing, then the order is blocked, nothing is reserved or charged, and the cart is kept | FR-28 |
| ORD-4 | Given any client-sent amount, when placing, then it is ignored and every price, discount, shipping fee and total comes from the server | SEC-09 |
| ORD-5 | Given a placed order, when later prices, the address book or the shipping policy change, then the order is unchanged (unit prices, shipping fee and address are snapshots) | §12.3.3 |
| ORD-6 | Given a placed order, then it has a unique, non-guessable-by-increment-alone order number per ADR 0006 and a status history row | FR-31 |
| ORD-7 | Given the order transaction fails at any step, then no order, reservation or redemption exists (all or nothing) | NFR-08 |
| ORD-8 | Given the notification subsystem is down, when an order is placed, then the order still succeeds and the event waits in the outbox | NFR-FT-08, TC-NOTIF-07 |
| ORD-9 | Given a not-yet-dispatched order, when it is cancelled, then its reservations are released, a status row is written and the discount redemption is released | FR-33, FR-SC-09 |
| ORD-10 | Given a dispatched order, when cancellation is attempted, then it is refused with a reason pointing to returns | FR-33 |
| ORD-11 | Given an empty cart or a cart of unsellable lines, when placing, then it is refused (new) | FR-28 |
| ORD-12 | Given a guest order, then name, email, mobile and address are stored with the order and nothing about them is required to be verified | customers ACT-30/32 |
| ORD-13 | Given a signed-in customer, when requesting an order by id, then another customer's order behaves as not found | SEC-05 |
| ORD-14 | Given many orders, when listed (staff or customer), then the list is paged and takes a constant number of queries | CONVENTIONS |

## Non-functional constraints
NFR-08/NFR-09 enforced in the database (unique idempotency key per cart/customer scope, row lock or conditional update on the reservation), not only in code. Order tables are expected to grow large: the plan names the partitioning key (BRD §12.6). No full address or phone in logs (SEC-25). Money `numeric(12,2)`.

## Out of scope
Pick/pack/dispatch statuses beyond a placeholder for "dispatched" (S14/S15), returns (S16/S17), payments detail (see [payments](../payments/spec.md)).

## Open questions
- [x] **Q-ORD-1 Cancellation window** (owner, 2026-11-02): a customer may cancel after pick is confirmed but before dispatch. Cancelling a picked order releases the reservation and creates a put-back task for the warehouse (S14 picks up the task; until then the order simply shows Cancelled and staff are notified).
- [x] **Q-ORD-2 Can a signed-in customer cancel by themselves** from their account, or must they contact support? Recommendation: yes for COD-pending and unpicked orders; card-paid cancellations create a refund task (refund money flow is S17). **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-ORD-3 Order number format.** ADR 0006 covers business numbers; confirm a prefix and shape (for example `RMP-` plus a sequence with a check digit) and that guessing the next number is harmless because look-up needs a second factor. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-ORD-4 Status set.** Proposed lookup: Pending Confirmation, Confirmed, Cancelled, Payment Pending, Paid, Dispatched, Delivered (later sprints add the rest). Confirm names. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-ORD-5 Stock decrement vs reservation.** Does placement decrement on-hand or only reserve (available = on-hand − reserved) until pick? Recommendation: reserve only; on-hand decreases at pick/dispatch in the ledger (S14). See [reservations](../../warehouse/reservations/spec.md). **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-ORD-6 Partition key** for the orders table: confirm placement date by month (BRD §12.6). **Approved (owner, 2026-11-02: approve all recommendations).**
