# Stock reservations

- **Status:** Draft
- **Jira:** to be created on approval (sprint S12)
- **BRD sections:** §5.4, §12.3.2, §12.7, warehouse §5.13
- **Requirement IDs:** FR-SC-09, FR-28, FR-10, NFR-08, TC-CHK-17

## Problem & intent
Once a shopper has placed an order, the units must be theirs, even though nothing has been picked yet, and the storefront must stop offering stock that is spoken for. When an order is cancelled or never confirmed, the units must come back without staff action.

## Requirements (quoted from the BRD)
> **FR-SC-09:** System shall reserve stock on order placement and release the reservation automatically if the order is cancelled before pick is confirmed.

> **FR-28:** The system shall re-validate stock at the point of order placement (not only at add-to-cart) and block the order with a clear message if stock was depleted in the interim, resolving concurrent-checkout races deterministically (first successful payment wins).

> **NFR-08:** Stock-decrement and order-placement shall be transactional/atomic to prevent overselling under concurrency (see FR-28).

## User flow
Orders asks Warehouse (through its contract) to reserve a list of variant quantities for an order. All lines are reserved or none. The reservation is released on cancellation, on COD auto-cancel, and (for card) when the payment window expires unpaid. Available = on-hand − reserved; the storefront availability stream (ADR 0018) carries the new figure.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| RSV-1 | Given enough stock for every line, when reserving, then all lines are reserved and available drops by the quantities | FR-SC-09 |
| RSV-2 | Given one line short, when reserving, then nothing is reserved and the response names the short variants and the available count | FR-28 |
| RSV-3 | Given two concurrent reservations for the last unit, when both run, then exactly one succeeds | TC-CHK-17, NFR-08 |
| RSV-4 | Given the same order id reserves twice, when repeated, then the second call is a no-op returning the same result (idempotent) | FR-29 (new) |
| RSV-5 | Given a cancelled order, when released, then available rises by the reserved quantities, once only | FR-SC-09 |
| RSV-6 | Given a card order unpaid after the payment window, when the window expires, then the reservation is released by a background job | FR-30 (new) |
| RSV-7 | Given a reserved variant, when the storefront availability is read, then it reflects on-hand minus reserved with a higher version so it cannot regress | ADR 0018, PCT-5 |
| RSV-8 | Given a cycle count or adjustment that would take on-hand below reserved, when posted, then it is flagged for review rather than leaving a negative availability (new) | warehouse §5.14 |
| RSV-9 | Given a pick is confirmed (S14), then the reservation converts to a ledger decrement (hook defined here, built in S14) | FR-SC-09 |

## Non-functional constraints
The reserve is a single conditional update per variant inside one transaction, with lines locked in a fixed order (variant id) to avoid deadlocks. Reservations table has its own schema ownership in `WHSE`; Orders never reads it directly. Ledger rules from ADR 0012 still hold.

## Out of scope
Holds at add-to-cart (see Q-RSV-1), allocation to bins and FIFO (S14), partial fulfilment.

## Open questions
- [x] **Q-RSV-1** (owner, 2026-11-02): reserve at order placement only.
- [ ] **Q-RSV-2 Card payment window.** How long may a card order hold stock while the shopper completes 3-D Secure? Recommendation: 30 minutes, then release and mark the order Payment Expired.
- [ ] **Q-RSV-3 COD pending window.** The reservation lasts as long as the COD confirmation workflow (see [cod-confirmation](../../orders/cod-confirmation/spec.md)); confirm no separate expiry.
- [ ] **Q-RSV-4 ADR needed:** the Orders-to-Warehouse call and its failure/compensation design (plan step).
