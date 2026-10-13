# Label work: accepted batches awaiting tags

- **Status:** Approved (2026-10-14; split from the approved Sprint 5 spec, acceptance criteria renumbered `LW-n`)
- **Jira:** SCRUM-198 (epic SCRUM-16)
- **BRD sections:** §5.12, §9.10.3
- **Requirement IDs:** FR-SC-06 (input side), NFR-SC-05, NFR-FT-08, NFR-17, NFR-19
- **Module context:** [README](../README.md) (decisions D1-D12, shared BRD text), [module design](../plan.md)

## Problem & intent

QC accepts quantities batch by batch. Warehouse staff need one clear list of "what has been accepted and still needs tags", without any cross-module reads, and they need the list to be trustworthy: no rejected quantity, no duplicates when a message is redelivered, and a visible reason when something cannot be processed.

## Requirements (quoted from the BRD)

> **§9.10.3 Event catalogue:** `QC.BatchAccepted` — published by QC, consumed by SKU-GEN (on accept).

> **§5.11:** Only Accepted (and Accept-with-concession, tagged accordingly) quantities proceed to SKU-GEN/putaway; rejected units never enter sellable inventory.

> **FR-SC-05 (for context):** … shall block rejected quantity from ever reaching sellable inventory.

## User flow

1. QC publishes `QcBatchAccepted`. This feature receives it (inbox), validates it, and works out the style from the PO. (new)
2. It keeps one **work item** per accepted sub-batch line: size, colour, quantity, outcome (`Accepted` | `Concession`), status. For each line it asks `variants` to assign a GTIN if the variant has none (D9).
3. Staff open **Labelling → Worklist** and see items awaiting tags, with "labelled n of N" and a "price changed since printing" flag. They queue printing from here (that is `print-jobs`).
4. If a PO's style cannot be found, the item is parked as **Needs attention** with a reason; staff can fix the cause and press **Resolve**.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| LW-1 | Given a `QcBatchAccepted` message, when it is delivered, then exactly one work item exists per `(SubBatchId, SizeId, ColourId)` and it appears in the worklist. | §9.10.3 |
| LW-2 | Given the same message delivered twice, when both are handled, then still one set of items exists and no extra GTIN is consumed. | ADR 0004 (new) |
| LW-3 | Given rejected, rework, pending or undecided quantity, when labelling is considered, then no work item exists for it (only `Accepted` and `Concession` lines are accepted). | §5.11, FR-SC-05 |
| LW-4 | Given a PO whose style is missing or retired, when the message arrives, then no GTIN is assigned, the item is parked as Needs attention with the reason, and **Resolve** re-runs it once the cause is fixed. A retired style proceeds after staff confirm (Q15). | (new) |
| LW-5 | Given a batch printed before the style's latest target-price revision, when the worklist shows it, then it carries a non-blocking "price changed since printing" flag; a reprint (print-jobs) that prints the current price clears it. Nothing is blocked or auto-printed. | D4 (new) |
| LW-6 | Given a malformed message (non-positive quantity, unknown size or colour id, empty lines, more than 500 lines, unsupported outcome code), when it is handled, then nothing is stored, no GTIN is assigned, and the message goes to the dead-letter path with a reason; one bad line never partially applies. | (new, security) |
| LW-7 | Given the worklist, when it loads, then it is paged (page size 1..100), ordered by a unique key, loads without N+1 queries, and renders as a semantic, keyboard-operable screen. | NFR-17, NFR-19, CONVENTIONS.md |
| LW-8 | Given any work-item creation, resolution or parking, when it commits, then it is attributable and timestamped, and any resulting event is in the outbox in the same transaction. | NFR-SC-05, NFR-FT-08 |

## Non-functional constraints

NFR-SC-05 (audit), NFR-FT-08 (a notification or printer failure never rolls back QC acceptance), NFR-17, NFR-19, plus the security baseline in the [plan](plan.md).

## Out of scope

Printing (print-jobs), GTIN logic (variants), anything about the shelf (Sprint 6).

## Open questions

- [ ] Q15 (retired style) and Q13 (Catalog contract) as in the [module README](../README.md); defaults adopted.
