# Adjustments: reason-coded stock corrections

- **Status:** Approved (2026-10-14; plan next)
- **Jira:** SCRUM-79 (FR-54, epic SCRUM-12 Admin back office; its scope excludes receiving, shelving and counts, which this sprint builds); cycle-count adjustments come from SCRUM-17's scope
- **BRD sections:** §5.14, §7.8, §12.11 (StockAdjustments)
- **Requirement IDs:** FR-54, FR-59/FR-60 (audit and roles, enforced in S8), NFR-08, NFR-SC-05, NFR-17, NFR-19, SEC-08; test cases TC-STK-01, 04, 05, 06, 07, 09 (Jira comment on SCRUM-79)
- **Module context:** [README](../README.md)

## Problem & intent

Physical stock drifts: units are damaged, lost, miscounted or found. Staff must be able to correct the system, but every correction has to say why and who, so the books can be trusted and disputes settled.

## Requirements (quoted from the BRD)

> **§5.14:** Edge case: cycle count finds a discrepancy → logged as a StockAdjustment with a mandatory reason code (damage, miscount, theft, found-stock) — never a silent overwrite of the quantity, for the same auditability reason as v8.0's return-window override (Section 5.6).

> **NFR-SC-05:** All inventory-adjusting actions (QC accept/reject, cycle-count adjustment, reverse-QC restock) must be attributable to a user/session and timestamped.

> **FR-54:** Admin users shall manage stock per variant with low-stock/stock-out alerting.

## User flow

1. Staff open **Warehouse → Adjustments** and start a new adjustment: scan or pick the GTIN, pick the bin, choose the grade, enter a quantity change (plus or minus), pick a reason code and add a short note.
2. The system appends a ledger movement of kind `Adjustment` linked to an adjustment record, and updates the totals. The adjustment is never edited.
3. The list shows adjustments with variant, bin, change, reason, who and when, paged and filterable by reason, variant and date.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| ADJ-0 | Given an adjustment of −3 with a reason, when it commits, then the bin stock falls by 3 and the movement row records actor, reason, the change and the before/after balance. | TC-STK-01 (SCRUM-79 AC-1) |
| ADJ-1 | Given an adjustment without a reason code, when it is submitted, then it is refused. | TC-STK-04, §5.14 |
| ADJ-2 | Given the reason codes, when they are listed, then they are the lookup rows `DAMAGE`, `MISCOUNT`, `THEFT`, `FOUND_STOCK` (seeded by migration, active flag, sort order). | §5.14 |
| ADJ-3 | Given reason `FOUND_STOCK` with a negative change, or `DAMAGE`/`THEFT` with a positive change, when it is submitted, then it is refused; `MISCOUNT` may go either way. | (new) |
| ADJ-4 | Given a negative change larger than the stock in that bin and grade, when it is submitted, then it is refused and nothing changes; the database also carries a check constraint so a balance can never be negative. | TC-STK-05 (SCRUM-79 AC-5) |
| ADJ-4a | Given adjustments and putaways committing at the same time on the same bin (and, once orders exist, order decrements), when they all commit, then no update is lost (row version or atomic conditional update). | TC-STK-07, NFR-08 |
| ADJ-4b | Given a change larger than the large-adjustment limit (N units, Q5), when it is submitted, then it is refused until the request carries an explicit second confirmation. | TC-STK-09 (SCRUM-79 detailed behaviour 5) |
| ADJ-4c | Given the adjustment endpoints, when the architecture tests run, then each sits in a secured group with the inventory-capable policy; real role enforcement (403 for a role without it) is verified when authentication arrives in S8. | TC-STK-06 (SCRUM-79 AC-6), SEC-08 |
| ADJ-5 | Given a valid adjustment, when it commits, then the adjustment record, its ledger movement and `StockLevelChangedEvent` are written in one transaction, with actor and time. | NFR-SC-05, NFR-FT-08 |
| ADJ-6 | Given an adjustment, when anyone tries to edit or delete it, then no endpoint allows it; a mistake is fixed by a new, opposite adjustment. | NFR-SC-05 (new) |
| ADJ-7 | Given a note longer than the limit, a zero change, a non-integer, an unknown/inactive bin, or an unknown GTIN, when it is submitted, then it is refused. | (new, security) |
| ADJ-8 | Given the same submission twice (idempotency key), when both arrive, then it applies once. | (new) |
| ADJ-9 | Given a voided (retired) GTIN, when a negative adjustment is submitted for tagged stock still on the shelf, then it is allowed; a positive adjustment is refused. | Q3 (new) |
| ADJ-10 | Given the adjustments list, when it loads, then it is paged (1..100), ordered by a unique key, N+1-free, with a semantic, keyboard-operable screen. | NFR-17, NFR-19, CONVENTIONS.md |

## Non-functional constraints

NFR-SC-05 (attribution and time), NFR-FT-08, NFR-17, NFR-19. Reason codes are a lookup, never a free-text string or an enum ordinal.

## Out of scope

Second-approver workflows (Q5), per-variant adjustment without naming a bin (stock lives in bins, so the screen asks for one), valuation write-off accounting, adjustments of reserved stock (no reservations yet), bulk import of adjustments.

## Open questions

- [x] **Q5 (module): large adjustments.** SCRUM-79 says adjustments above N units need a "second confirmation" but gives neither N nor what "second" means. Default: N = 20 units (configuration), and the second confirmation is a deliberate confirm step by the same person (one-person warehouse); a second approver is the alternative. Who decides: owner. Blocks: ADJ-4b.
- [x] **Q-ADJ-1: Extra reason codes.** The BRD lists four. Default: only those four; more are a lookup row away.
- [x] **Q-ADJ-2: Who may adjust.** No authentication until Sprint 8. Default: the endpoint sits in `Policies.Admin`-ready group (declared now, enforced with S8), per ADR 0011.
