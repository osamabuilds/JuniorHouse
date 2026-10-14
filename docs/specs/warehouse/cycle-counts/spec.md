# Cycle counts: catching drift continuously

- **Status:** Approved (2026-10-14; plan next)
- **Jira:** SCRUM-200 (epic SCRUM-17)
- **BRD sections:** §5.14, §12.11 (CycleCounts)
- **Requirement IDs:** NFR-SC-05, NFR-17, NFR-19
- **Module context:** [README](../README.md)

## Problem & intent

Systems and shelves drift apart. Staff recount a few bins at a time, the system compares the count with what it believes, and every difference is explained and corrected on the record rather than overwritten.

## Requirements (quoted from the BRD)

> **§5.14:** Cycle counts (partial physical recounts on a rolling schedule) reconcile system stock against physical stock; a full annual stocktake is the fallback, cycle counts catch drift continuously instead.

> **§5.14:** Edge case: cycle count finds a discrepancy → logged as a StockAdjustment with a mandatory reason code (damage, miscount, theft, found-stock) — never a silent overwrite of the quantity.

> **NFR-SC-05:** All inventory-adjusting actions (QC accept/reject, cycle-count adjustment, reverse-QC restock) must be attributable to a user/session and timestamped.

## User flow

1. Staff open **Warehouse → Cycle counts** and create a count by choosing bins (a handful, a whole zone, or all bins for the annual stocktake). The bins list shows "days since last counted" to help choose. (new)
2. The count sheet lists the chosen bins. Staff scan each bin and each GTIN and enter the counted quantity. The system quantity is hidden while counting (blind count, Q9). Lines can be added for variants found in a bin that the system does not expect there. (new)
3. On submit the system shows each line's system quantity, counted quantity and variance. Staff pick a reason code for every line with a variance.
4. Confirming creates one adjustment per variance line (adjustments feature), linked to the count. Lines with no variance close without an adjustment.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| CC-1 | Given chosen active bins, when a count is created, then it holds one sheet per bin, status Open, with the creator and time. | §5.14 |
| CC-2 | Given an Open count, when staff view it, then system quantities are not sent to the client until the counts are submitted (blind count). | Q9 (new) |
| CC-3 | Given a bin that already belongs to an Open count, when another count includes it, then it is refused. | (new) |
| CC-4 | Given submitted counts, when the system quantity differs, then each variance line requires a reason code; confirmation is refused while any variance line lacks one. | §5.14 |
| CC-5 | Given confirmation, when it commits, then one adjustment per variance exists, linked to the count, with ledger movements and `StockLevelChangedEvent`, all in one transaction, and the count is Closed. | §5.14, NFR-SC-05 |
| CC-6 | Given a variant counted in a bin where the system expects none, when confirmed, then it is a positive adjustment with a reason code. | (new) |
| CC-7 | Given stock in the bin changed (putaway or adjustment) after the count was opened, when the counts are submitted, then those lines are flagged "changed since count opened" and must be recounted before confirming. | (new) |
| CC-8 | Given a zero-variance line, when confirmed, then no adjustment is created and the bin's "last counted" time is updated. | (new) |
| CC-9 | Given a count not yet confirmed, when staff cancel it, then no stock changes and the cancellation is recorded. | (new) |
| CC-10 | Given quantities that are negative or non-integer, a bin or GTIN that is unknown, or a retired GTIN counted as present, when submitted, then it is refused with a message. | (new, security) |
| CC-11 | Given the counts list and sheets, when they load, then they are paged (1..100), ordered by a unique key, N+1-free, semantic and keyboard-operable. | NFR-17, NFR-19, CONVENTIONS.md |
| CC-12 | Given any count creation, submission, confirmation or cancellation, when it commits, then it is attributable and timestamped. | NFR-SC-05 |

## Non-functional constraints

NFR-SC-05, NFR-FT-08, NFR-17, NFR-19, NFR-SC-07 (works with keyboard-style scanners on a basic network).

## Out of scope

Automatic scheduling of counts (Q9), supervisor recount workflows, tolerance thresholds, mobile app, variance reports.

## Open questions

- [x] **Q9: Rolling schedule and blind counting.** The BRD says "rolling schedule" without a rule. Default: counts are created by staff; the screen shows days since each bin was last counted; counts are blind. The alternative is a system-suggested schedule (for example, bins not counted in 30 days). Who decides: owner.
- [ ] **Q-CC-1: Count of a bin in use.** Default: counting does not freeze the bin; CC-7 handles movement during the count.
