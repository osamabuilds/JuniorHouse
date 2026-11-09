# Allocations

- **Status:** Draft, applied by delegation (owner away, 2026-11-09)
- **Jira:** to be created
- **BRD sections:** §5.15, §5.13, §5.14
- **Requirement IDs:** FR-SC-09 (RSV-9), FR-SC-10

## Problem & intent
A confirmed or paid order must be tied to specific shelf locations so a picker knows exactly where to walk, and so older stock sells first. Allocation must never promise units that are not physically there, and must never double-promise a unit to two orders.

## Requirements (quoted from the BRD)
> **§5.15:** Order placed and confirmed (COD confirmation call passed, or card payment captured) → order enters the fulfilment queue. Allocation: system reserves specific bin-located units against the order lines (FIFO by putaway date is the simple, correct default so older stock sells first).

> **FR-SC-09:** System shall reserve stock on order placement and release the reservation automatically if the order is cancelled before pick is confirmed.

## User flow
Orders raises `OrderConfirmedEvent` (cash on delivery) or `OrderPaidEvent` (online). Fulfilment creates one fulfilment order and asks Warehouse to allocate its lines to bins, oldest putaway first. The order appears in the pick queue. If the order is cancelled first, the allocation is released and nothing is picked.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| ALC-1 | Given a confirmed cash on delivery order, when the event arrives, then one fulfilment order exists with each line allocated to one or more bins | §5.15 |
| ALC-2 | Given a paid online order, when the event arrives, then the same happens | §5.15 |
| ALC-3 | Given a variant in two bins with different putaway dates, when allocating, then the bin with the older putaway is used first | §5.15 FIFO |
| ALC-4 | Given the first bin holds fewer units than the line needs, when allocating, then the rest comes from the next oldest bin and the line shows both locations (new) | §5.15 |
| ALC-5 | Given the same event is delivered twice, when handled, then there is still one fulfilment order and one allocation (new) | NFR-09 |
| ALC-6 | Given an order cancelled before any unit is picked, when the cancellation arrives, then its allocation is released, its pick list is closed and nothing remains to pick | FR-SC-09 |
| ALC-7 | Given an order cancelled after some units were picked, when the cancellation arrives, then the picked units are listed as a put-back task and the rest is released (new) | §5.15, `WasPicked` |
| ALC-8 | Given a bin whose physical count is lower than the system count (drift), when allocation finds less than reserved, then the short line is marked "awaiting restock" instead of failing the whole order (new) | §5.15 edge case |
| ALC-9 | Given two orders competing for the last unit in a bin, when both allocate at once, then exactly one gets it and the other takes another bin or waits (new) | NFR-08 |
| ALC-10 | Given an allocation, when read, then on-hand stock has not changed: units leave stock only when a pick is confirmed (new) | RSV-9 |
| ALC-11 | Given an order that is already picked, packed or shipped, when a second confirmation event arrives, then nothing changes (new) | NFR-09 |

## Non-functional constraints
Allocation is a single transaction in Warehouse with the bin rows locked in a fixed order (no deadlocks). Fulfilment's event handlers sit behind the inbox. No personal data in any event or log (SEC-25): the pick list shows the order number, never the customer.

## Out of scope
Batch allocation across orders, allocation to a damaged or quarantine grade, courier choice.

## Open questions
- [ ] **Q-ALC-1** How is "putaway date" known per bin? Recommendation: the date of the oldest putaway movement that still has stock in that bin, read from the stock ledger inside Warehouse. (Plan decides the query.)
