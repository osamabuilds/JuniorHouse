# Stock ledger: where it is and how many

- **Status:** Approved (2026-10-14; plan next)
- **Jira:** SCRUM-98 and SCRUM-79 (movement ledger with before/after is in SCRUM-79's scope), epic SCRUM-17
- **BRD sections:** §5.13, §5.14, §7.8, §9.10.3, §12.11 (StockByBin)
- **Requirement IDs:** FR-SC-08, FR-54, NFR-SC-05, NFR-SC-06, NFR-FT-08, NFR-17, NFR-19
- **Module context:** [README](../README.md)

## Problem & intent

Pick staff need to know where a unit physically is, and the business needs to know how many it has. Both must come from one trustworthy source that can be audited line by line and never silently overwritten, and other modules (the storefront read model in Sprint 7, orders later) must learn about changes through events.

## Requirements (quoted from the BRD)

> **§5.14:** WMS holds the single source of truth for physical StockQuantity per SKU+bin; the storefront's cached/read-model stock (v8.0 Section 12.7) is refreshed from WMS via the same domain-event mechanism already specified for Products/ProductVariants.

> **§5.13:** … separate from 'how many do we have' (both matter: pick staff need location, not just count).

> **NFR-SC-05:** All inventory-adjusting actions … must be attributable to a user/session and timestamped.

> **NFR-SC-06:** Stock-visible-on-storefront must reflect a putaway-confirmed WMS state within the same eventual-consistency bound already set in v8.0 Section 12.7 (a few seconds), not longer.

> **FR-54:** Admin users shall manage stock per variant with low-stock/stock-out alerting.

## User flow

1. Every stock change (putaway, adjustment, cycle-count adjustment) appends one movement to the ledger: variant, grade, bin, signed quantity, kind, reference, acting person, time.
2. Staff open **Warehouse → Stock** and see stock per variant (SKU, name, on hand, by grade) and can expand a variant to see its bins and quantities, or search by bin to see what is in it.
3. Each change to a variant's totals publishes `StockLevelChangedEvent` (variant id, on-hand, available, per-grade) through the outbox. `StockPutawayConfirmedEvent` (putaway spec) is the first-time "now available" signal.
4. The "reserved" quantity is not built yet. Available equals on-hand until reservations arrive (FR-SC-09, later). (new)

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| LED-1 | Given any stock change, when it commits, then exactly one movement row is appended with kind, signed quantity, bin, variant, grade, reference, actor, time and the balance before and after, and no movement is ever updated or deleted. | TC-STK-01, NFR-SC-05, §5.14 |
| LED-2 | Given movements, when stock by bin is read, then it equals the sum of the movements for that variant, grade and bin; a consistency test proves the stored balance and the ledger agree. | §5.14 (new) |
| LED-3 | Given a movement that would make a bin balance negative, when it is attempted, then it is refused atomically, even under concurrent attempts. | (new) |
| LED-4 | Given a committed change, when the outbox dispatches, then `StockLevelChangedEvent` is delivered within **5 seconds p95** on the test stack (target proposed in Q8), and a delivery failure never rolls back the movement. | NFR-SC-06, NFR-FT-08 |
| LED-5 | Given a variant, when its stock is read, then on-hand and available are shown per grade and in total, and available equals on-hand (no reservations yet). | §5.14 |
| LED-6 | Given a variant with stock in several bins, when staff view it, then each bin and its quantity is listed so pick staff know where it is. | §5.13 |
| LED-7 | Given a bin, when staff search by bin code, then the variants and quantities in it are listed. | §5.13 (new) |
| LED-8 | Given the stock list, when it loads, then it is paged (1..100), ordered by a unique key, loads without N+1 queries (one query for bins per page, not per row), and the screen is semantic and keyboard-operable. | NFR-17, NFR-19, CONVENTIONS.md |
| LED-9 | Given the ledger history of a variant, when staff view it, then movements are listed newest first, paged, with kind, quantity, bin, actor, time and reference. | NFR-SC-05 (new) |
| LED-10 | Given a request carrying a quantity or total from the client, when the server processes it, then it recomputes balances itself and ignores client-supplied totals. | SEC-09 spirit (new, security) |

## Non-functional constraints

NFR-SC-05, NFR-SC-06, NFR-FT-08 (events through the outbox in the same transaction), NFR-17, NFR-19. Quantities are whole units (`integer`, never fractional). The ledger is a transactional table that will grow large; its partitioning key is stated in the plan (BRD §12.6).

## Out of scope

Reservations and allocation (FR-SC-09, Sprint 12), decrement on pick (Sprint 14), bin-to-bin moves (Q4), valuation or costing, the storefront read model itself (Sprint 7).

## Open questions

- [x] **Q2 (module): grade in the ledger.** Default: movements and balances carry a grade (`Accepted` | `Concession`).
- [x] **Q8: "a few seconds".** The BRD does not give a number. Default: 5 seconds p95 from commit to event delivery on a single-host deployment. Who decides: owner. Blocks: LED-4's numeric target.
- [ ] **Q-LED-1: Reserved quantity placeholder.** Default: no reserved column yet; added with the reservation feature (additive migration), so the schema stays honest about what exists.
