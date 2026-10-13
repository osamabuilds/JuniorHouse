# Print jobs: the tag print queue and reprints

- **Status:** Approved (2026-10-14; split from the approved Sprint 5 spec, acceptance criteria renumbered `PJ-n`)
- **Jira:** SCRUM-198 (epic SCRUM-16, queue also touches SCRUM-18); tasks SCRUM-97 (FR-SC-07)
- **BRD sections:** §5.12 (What Gets Printed, and Where; re-print edge case)
- **Requirement IDs:** FR-SC-07, NFR-SC-02, NFR-SC-03, NFR-SC-05, NFR-FT-08
- **Module context:** [README](../README.md), [module design](../plan.md), [ADR 0010](../../../adr/0010-label-printing-queue-and-printer-port.md)

## Problem & intent

Tags are printed in bulk once QC accepts a batch. A jammed or offline printer must never stop other work, and a lost or damaged tag must be reprintable without a new barcode so sales history stays continuous.

## Requirements (quoted from the BRD)

> **FR-SC-07:** System shall support re-printing an existing SKU's tag without generating a new GTIN.

> **NFR-SC-02:** Label/tag printers must support a queued, retry-on-jam printing model — a single printer fault must not halt the SKU-GEN or PACK workflow for other items in queue.

> Tag printing happens in-house on a barcode/label printer (thermal transfer, e.g. Zebra/TSC-class) once QC accepts a batch … Edge case: a re-print is needed (tag lost/damaged in warehouse) → the system must allow re-printing an existing GTIN's tag without generating a new barcode, so historical sales data for that SKU stays continuous.

## User flow

1. From the worklist, staff **queue a print job** for an item with a tag count (the unlabelled quantity plus the configured spare allowance, default 0%, D10). The job is saved instantly; the printer is not contacted.
2. The **print worker** sends due jobs to the printer one at a time per printer. A failed attempt is retried with growing delays; after the attempt limit the job shows **Failed** with the last error, and staff can **Retry** or **Cancel**. Other jobs keep printing.
3. When a job is printed, the item's "labelled n of N" updates.
4. For a lost or damaged tag, staff open the variant and **reprint**: a new job for the same GTIN with a reason (Lost, Damaged, PriceChange, Other), a note and the acting person. A reprint prints the current price (D4).
5. Staff watch the **Print queue** screen (paged, refreshes by itself).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| PJ-1 | Given a work item, when staff queue a job, then the tag count is between 1 and the unlabelled quantity plus the spare allowance (D10), the job is created in Queued state, and the request returns without contacting the printer. | §5.12, D10 (new) |
| PJ-2 | Given the printer fails on a job, when the retry policy runs, then the job is retried with backoff and, after the attempt limit, ends Failed with the last error shown; the next due job still prints. | NFR-SC-02 |
| PJ-3 | Given a Failed job, when staff press Retry, then the same job is queued again (attempts reset) without a new job record or GTIN; Cancel ends it. | NFR-SC-02 (new) |
| PJ-4 | Given several printers, when one is offline, then jobs for the others are unaffected and the screens keep working. | NFR-SC-02 |
| PJ-5 | Given an existing variant with an Active GTIN, when staff reprint with a reason, then a new job is created for the same GTIN and the GTIN and SKU are unchanged. | FR-SC-07 |
| PJ-6 | Given a reprint, when it is saved, then it records reason, note, acting person and time, and appears in the variant's print history. | FR-SC-07, NFR-SC-05 |
| PJ-7 | Given a reprint without a reason or acting person, or a count outside 1..max, when submitted, then it is rejected with field-level errors. | NFR-SC-05 (new) |
| PJ-8 | Given a Retired GTIN, when anyone tries to print or reprint it, then the request is refused. | NFR-SC-03 (new) |
| PJ-9 | Given a worker crash while a job is Printing, when the stuck-job timeout passes, then the job returns to Queued; a status change is a single conditional update so two workers never print one job twice. | ADR 0010 (new) |
| PJ-10 | Given any job creation, state change, reprint or cancel, when it commits, then it is attributable and timestamped, `TagJobPrinted` v1 is in the outbox in the same transaction on success, and a printer or notification failure never rolls back QC acceptance or GTIN assignment. | NFR-SC-05, NFR-FT-08 |
| PJ-11 | Given the limits (max tags per job 5,000; max 200 queued jobs per printer), when a request would exceed them, then it is rejected with a clear message. | (new, security) |
| PJ-12 | Given the printer address, then it comes only from configured printer records; no API field, query or event can supply or change it. | (new, security) |
| PJ-13 | Given the queue screen and job lists, when they load, then they are paged (1..100), ordered by a unique key, with no N+1, and are semantic and keyboard-operable. | NFR-17, NFR-19, CONVENTIONS.md |

## Non-functional constraints

NFR-SC-02, NFR-SC-05, NFR-FT-08, plus the security baseline in the [plan](plan.md).

## Out of scope

Tag content and rendering (tags); GTIN assignment (variants); choosing printer hardware (D7); shipping labels (Sprints 14-15).

## Open questions

None open beyond the module-level ones in the [README](../README.md).
