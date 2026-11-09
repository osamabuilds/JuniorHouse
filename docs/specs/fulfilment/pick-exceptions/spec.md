# Pick exceptions

- **Status:** Draft, applied by delegation (owner away, 2026-11-09)
- **Jira:** to be created
- **BRD sections:** §5.15, §5.14, §8.3
- **Requirement IDs:** FR-SC-11, NFR-SC-05

## Problem & intent
Sometimes the shelf does not hold what the system says. The picker must be able to say so, and the system must not pretend the item was picked. The customer should hear about a delay from us, not discover it later.

## Requirements (quoted from the BRD)
> **FR-SC-11:** System shall allow a picker to flag a pick exception (system/physical stock mismatch) instead of force-completing a pick.

> **§5.15 edge case:** pick list references a bin whose physical count doesn't match system count (a missed cycle-count drift) → picker must be able to flag a pick exception rather than force-completing against stock that isn't actually there; the order line falls back to 'delayed / awaiting restock' with the customer notified, per v8.0's fault-tolerant-notification principle (Section 8.3).

> **§5.14:** cycle count finds a discrepancy → logged as a StockAdjustment with a mandatory reason code (damage, miscount, theft, found-stock) — never a silent overwrite of the quantity.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| PEX-1 | Given a line whose shelf is short, when the picker flags an exception with a reason and the count found, then the line is marked "awaiting restock" and the picks already scanned stay counted | FR-SC-11 |
| PEX-2 | Given a flagged line, when the picker continues, then the other lines of the order can still be picked | §5.15 |
| PEX-3 | Given a flagged line, when raised, then a delayed-line event is published so the customer is told (ids and codes only) | §5.15, §8.3 |
| PEX-4 | Given an exception, when staff open the exception queue, then they see the order, the SKU, the bin, the system count, the count found, the picker and the time | FR-SC-11 |
| PEX-5 | Given an exception, when raised, then a reason is required (not found, short, damaged) and the found count is a whole number between 0 and the allocated quantity (new) | §5.15 |
| PEX-6 | Given an exception, then the system stock is not overwritten: a cycle-count task or adjustment suggestion is raised for staff, and the bin is flagged so it is not allocated from until reviewed (new) | §5.14 |
| PEX-7 | Given every line of an order is flagged, when the last is flagged, then the order is held as "awaiting restock" and leaves the pick queue (new) | §5.15 |
| PEX-8 | Given stock arrives for the delayed line, when staff resolve the exception, then the line is allocated again and the order returns to the pick queue (new) | §5.15 |
| PEX-9 | Given the customer would rather cancel the delayed line, when staff cancel it, then the order total and the refund path are handled by Orders (S16/S17); here the line is only removed from picking (new) | §5.15 |
| PEX-10 | Given any exception, then it records the signed-in staff member and the time | NFR-SC-05 |

## Out of scope
The refund of a cancelled line (S17); the customer message wording (a Notifications template).

## Open questions
- [ ] **Q-PEX-1** May a picker mark a line "found elsewhere" (scan a different bin that has the unit)? Recommendation: no in S14; the exception goes to staff, who can move stock with a normal adjustment.
