# Pick lists

- **Status:** Draft, applied by delegation (owner away, 2026-11-09)
- **Jira:** to be created
- **BRD sections:** §5.15, §5.14
- **Requirement IDs:** FR-SC-10, NFR-SC-01, NFR-SC-05, FR-SC-09 (RSV-9)

## Problem & intent
The picker needs a list of what to take from which shelf, and the system needs proof that what was taken is what the order needs. A scan of the bin and a scan of the barcode on each unit is what stops "wrong size shipped".

## Requirements (quoted from the BRD)
> **FR-SC-10:** System shall generate a pick list per order (or batch of orders) and require scan-to-confirm at both pick and pack stages.

> **§5.15:** Pick List generated, grouped either by single-order (simple, MVP-appropriate at low volume) or batch-picked across multiple orders sharing a SKU (an efficiency gain worth adding once daily order volume justifies it — not MVP). Picker scans each bin and each GTIN to confirm the physical pick matches the pick list — this scan-to-confirm step is what prevents the classic 'wrong size shipped' complaint.

> **NFR-SC-01:** Barcode scan-to-confirm at pick/pack must resolve in under 300ms so it never becomes the bottleneck step at a pack station.

> **NFR-SC-05:** All inventory-adjusting actions (QC accept/reject, cycle-count adjustment, reverse-QC restock) must be attributable to a user/session and timestamped.

## User flow
A picker opens the queue, takes the oldest order, and gets its pick list in walking order. For each line: scan the bin, then scan each unit's GTIN. A wrong bin or GTIN is refused and nothing changes. When every unit is scanned the pick is complete: the reserved stock becomes a real decrement in the stock ledger, and the order moves to packing.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| PCK-1 | Given an allocated order, when its pick list is opened, then lines are in walking order (zone, aisle, shelf) with bin, SKU, size, colour and quantity | §5.15 |
| PCK-2 | Given the right bin and then the right GTIN, when scanned, then the unit is counted for that line | FR-SC-10 |
| PCK-3 | Given a GTIN that is not on this line, when scanned, then it is refused with a clear message and nothing is counted | §5.15 |
| PCK-4 | Given a scan of a bin other than the line's, when scanned, then it is refused and the picker is told where the line is | §5.15 |
| PCK-5 | Given a line of three units, when three correct scans are made, then the line is done; a fourth scan is refused (no over-pick) (new) | §5.15 |
| PCK-6 | Given any scan, when the server resolves it, then it answers in under 300 ms, using indexed lookups only | NFR-SC-01 |
| PCK-7 | Given every line done, when the pick is completed, then each allocated unit is decremented from its bin in the stock ledger exactly once and the reservation is converted | RSV-9, FR-SC-09 |
| PCK-8 | Given the completing call is repeated or the scan repeated after completion, when handled, then nothing is decremented twice (new) | NFR-09 |
| PCK-9 | Given two pickers open the same order, when both try to scan, then one holds the pick and the other is told who has it (new) | §5.15 |
| PCK-10 | Given any scan or completion, then it records the signed-in staff member and the time | NFR-SC-05 |
| PCK-11 | Given a pick list, when read, then it shows the order number only: no customer name, address or phone (new) | SEC-25 |
| PCK-12 | Given an order cancelled while being picked, when the next scan is made, then it is refused and the picker is told to put the items back (new) | FR-SC-09 |

## Non-functional constraints
A scan is one indexed query by bin code and one by GTIN against rows loaded for the pick list; no per-scan joins across modules. Every endpoint checks the staff permission on every call (SEC-08).

## Out of scope
Batch picking, partial shipment of an order, picking by voice or camera.

## Open questions
- [ ] **Q-PCK-1** Is each unit scanned, or may the picker scan a GTIN once and type the quantity? Recommendation: scan each unit (the BRD says "each GTIN"); a quantity shortcut can follow if it proves slow.
