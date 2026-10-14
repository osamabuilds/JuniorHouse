# Bins: shelf locations

- **Status:** Approved (2026-10-14; plan next)
- **Jira:** SCRUM-200 (epic SCRUM-17) 
- **BRD sections:** §5.13, §12.11 (WarehouseBins)
- **Requirement IDs:** NFR-SC-05, NFR-17, NFR-19, NFR-SC-07
- **Module context:** [README](../README.md)

## Problem & intent

Stock cannot be shelved or found without named places to put it. Warehouse staff need a short, scannable list of bins that follows the simple zone-aisle-shelf scheme the BRD calls sufficient for MVP, and a bin must never disappear while stock sits in it.

## Requirements (quoted from the BRD)

> **§5.13:** Accepted, tagged stock is assigned a bin/shelf location (simple zone-aisle-shelf scheme is sufficient at MVP scale; a full slotting algorithm is a later optimisation).

> **§12.11:** Warehouse (WMS) core tables: WarehouseBins, …

## User flow

1. Staff open **Warehouse → Bins** and add a bin by entering zone, aisle and shelf. The system builds the bin code from those three parts (for example `A-03-2`) and the code is what is printed on the shelf and scanned. (new: code format)
2. The list shows bins with their zone, aisle, shelf, active flag and units on hand, paged and filterable by zone.
3. Staff can deactivate an empty bin, and reactivate it later. A bin that holds stock cannot be deactivated. (new)

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| BIN-1 | Given a zone, aisle and shelf, when a bin is created, then it has a unique code built from the three parts and appears in the list. | §5.13 |
| BIN-2 | Given a bin with the same code already exists, when another is created, then it is refused with a clear message and nothing is stored. | (new) |
| BIN-3 | Given zone, aisle or shelf outside the allowed pattern (letters/digits, bounded length), when a bin is created, then it is refused. | (new, security) |
| BIN-4 | Given a bin that holds stock, when staff try to deactivate it, then it is refused; given an empty bin, it can be deactivated and reactivated. | (new) |
| BIN-5 | Given a deactivated bin, when putaway or an adjustment targets it, then it is refused (checked in those features). | (new) |
| BIN-6 | Given the bin list, when it loads, then it is paged (size 1..100), ordered by a unique key, loads without N+1 queries, and is a semantic, keyboard-operable screen. | NFR-17, NFR-19, CONVENTIONS.md |
| BIN-7 | Given any bin creation or status change, when it commits, then it is attributable and timestamped. | NFR-SC-05 |

## Non-functional constraints

NFR-17, NFR-19, NFR-SC-05, NFR-SC-07. Security baseline per the plan (to be written after approval).

## Out of scope

Slotting algorithms, bin capacity or dimensions, multiple warehouses, bin deletion, moving stock between bins (Q4).

## Open questions

- [ ] **Q-BIN-1: Code format.** The BRD says "zone-aisle-shelf" only. Default: zone is 1-3 letters/digits, aisle 1-3 digits, shelf 1-2 letters/digits, joined by hyphens, upper-case. Who decides: owner (this is what gets printed on the shelf). Blocks: the validation rule and the bin label.
- [x] **Q-BIN-2: Single warehouse.** Answered by BRD §10.4.1 ("a single physical location at launch; multi-warehouse routing is Phase 2+"): no warehouse column; adding one later is an additive migration.
