# Putaway: shelving accepted stock

- **Status:** Approved (2026-10-14; plan next)
- **Jira:** SCRUM-98 (FR-SC-08, epic SCRUM-17); depends on SCRUM-96 (GTINs, done in S5)
- **BRD sections:** §5.13, §7.10, §9.10.2, §9.10.3, §12.11 (PutawayRecords)
- **Requirement IDs:** FR-SC-08, NFR-SC-01 (scan latency), NFR-SC-05, NFR-SC-06, NFR-SC-07, NFR-FT-08, NFR-17, NFR-19; test cases TC-PUT-01..05 (from the Jira comment on SCRUM-98)
- **Module context:** [README](../README.md)

## Problem & intent

After QC accepts a batch and it is tagged, the units sit in a receiving area. Warehouse staff need to record where each unit physically went, by scanning, and the storefront must not show any of that stock as available until it is shelved and findable.

## Requirements (quoted from the BRD)

> **§5.13:** Putaway is recorded by scanning the GTIN + scanning/selecting the bin location — this is the system's source of truth for 'where physically is this unit', separate from 'how many do we have' (both matter: pick staff need location, not just count). Stock becomes Available-to-Promise (ATP) on the storefront only after putaway is confirmed — not the moment QC accepts it.

> **FR-SC-08:** System shall not mark a variant 'available' on the storefront until putaway is confirmed in the warehouse, not merely on QC acceptance.

> **NFR-SC-06:** Stock-visible-on-storefront must reflect a putaway-confirmed WMS state within the same eventual-consistency bound already set in v8.0 Section 12.7 (a few seconds), not longer.

> **§5.11 (for context):** Only Accepted (and Accept-with-concession, tagged accordingly) quantities proceed to SKU-GEN/putaway; rejected units never enter sellable inventory.

> **§9.10.3:** `Stock.PutawayConfirmed` — published by WMS, consumed by the Catalog read model (ATP becomes visible on storefront).

## User flow

1. Warehouse receives `QcBatchAcceptedEvent` and `VariantGtinAssignedEvent` (inbox, idempotent). For each accepted line it keeps a **putaway item**: variant, grade (`Accepted` | `Concession`), accepted quantity, quantity put away, status. The variant is identified by style (from the PO), colour and size; the GTIN comes from the labelling event. An item whose GTIN has not arrived yet waits as **Awaiting GTIN**. (new)
2. Staff open **Warehouse → Putaway** and see items awaiting putaway ("put away n of N"), oldest first.
3. Staff scan the GTIN on a tag, then scan (or pick) the bin, enter the quantity and confirm. A scan input stays focused so the next scan can follow without the mouse. (Q7)
4. The system records the putaway (who, when, bin, quantity), adds the stock to that bin, and only now does the variant's available quantity rise. A `StockPutawayConfirmedEvent` goes to the outbox in the same transaction.
5. Partial putaway is normal: the rest of the item stays in the queue, and the units can go to several bins. (new)

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| PUT-1 | Given a `QcBatchAcceptedEvent`, when it is delivered, then exactly one putaway item exists per `(SubBatchId, SizeId, ColourId)` with the accepted quantity, and no stock is available for it. | TC-PUT-01 (SCRUM-98 AC-1), §5.13 |
| PUT-2 | Given the same message delivered twice, when both are handled, then the pending quantity is unchanged. | ADR 0004 (new) |
| PUT-3 | Given a variant with no putaway yet, when its available quantity is read, then it is zero even though QC accepted units. | TC-PUT-01, FR-SC-08 |
| PUT-4 | Given a scanned GTIN that belongs to a pending item and a valid active bin, when putaway is confirmed for a quantity ≤ pending, then the bin holds that quantity, the item's put-away count rises, and the variant's available quantity (putaway-confirmed quantity minus reservations, none yet) rises by the same amount. | TC-PUT-02 (SCRUM-98 AC-2), §5.13 |
| PUT-5 | Given a quantity greater than the pending quantity, a quantity ≤ 0, or a non-integer, when putaway is confirmed, then it is refused and nothing changes. | (new) |
| PUT-6 | Given an unknown GTIN, a GTIN with no pending item, or a bin code that does not exist or is inactive, when putaway is confirmed, then it is refused with a message that says which. | TC-PUT-03 (SCRUM-98 AC-3) |
| PUT-7 | Given a GTIN whose number has been voided (retired), when it is scanned, then putaway is refused and the message says the barcode is retired. | Q14 / Q3 (new) |
| PUT-8 | Given the same confirm request submitted twice (double scan, network retry), when both arrive, then the quantity is added once (idempotency key). | (new) |
| PUT-9 | Given two staff confirming against the same item at once, when both commit, then the total put away never exceeds the accepted quantity. | NFR-08 spirit (new) |
| PUT-10 | Given a confirmed putaway, when it commits, then `StockPutawayConfirmedEvent` and the ledger movement are written in one transaction, and a failure to deliver the event never rolls back the putaway. | NFR-SC-06, NFR-FT-08 |
| PUT-11 | Given a confirmed putaway, when the event is dispatched, then it reaches the outbox consumer within the bound in the ledger spec (LED-4). | NFR-SC-06 |
| PUT-12 | Given rejected, rework, pending or undecided quantity, when putaway is considered, then no item exists for it (only released quantity arrives). | §5.11, FR-SC-05 |
| PUT-13 | Given a mistaken putaway, when staff reverse it, then the correction is an adjustment (adjustments spec), never an edit of the putaway record. | NFR-SC-05 (new) |
| PUT-14 | Given the putaway queue and history, when they load, then they are paged (1..100), ordered by a unique key, free of N+1 queries, and the screen is semantic and keyboard-operable with the scan input focused. | NFR-17, NFR-19, NFR-SC-07, CONVENTIONS.md |
| PUT-15 | Given any putaway, when it commits, then it is attributable (acting person plus system actor) and timestamped. | TC-PUT-04 (SCRUM-98 AC-4), NFR-SC-05 |
| PUT-16 | Given the Catalog module (and any module other than Warehouse), when the architecture tests run, then none reads Warehouse tables or references its internals; availability reaches other modules only by event or contract. | TC-PUT-05 |
| PUT-17 | Given the GTIN/bin scan lookup endpoint, when 1,000 scans are measured on the test stack, then p95 is under 300 ms server-side. | NFR-SC-01 (SCRUM-120 AC-1; applied to putaway scans, Q10) |
| PUT-19 | Given an item whose variant has no printed tag yet (nothing to scan), when staff open the putaway screen, then they can choose the item from a list and confirm putaway without a GTIN scan; the bin is still scanned or selected, and every other rule (quantity, active bin, audit) applies unchanged. | D1 (owner decision) |
| PUT-20 | Given putaway items of grade `Accepted` and `Concession`, when stock is put away, then the bin balance and ledger movement carry the same grade; the two are never merged. | D2 (owner decision) |
| PUT-21 | Given a quantity above what is pending, when staff try to put it away, then it is refused and they are pointed to a `FOUND_STOCK` adjustment, which obeys the large-adjustment re-confirm (no exemption). | D11 (owner decision) |
| PUT-18 | Given a throttled connection (slow 3G profile), when staff confirm a putaway, then the screen stays usable: one small request per confirm, no large payloads on scan. | NFR-SC-07 (SCRUM-120 AC-7) |

## Non-functional constraints

FR-SC-08, NFR-SC-05, NFR-SC-06, NFR-FT-08, NFR-SC-07, NFR-17, NFR-19. Warehouse never reads Quality, Labelling or Vendor tables; it uses their events (and contracts if a read is unavoidable).

## Out of scope

Pick, pack and reservations; relocating stock between bins (Q4); printing bin labels; camera scanning (Q7); the storefront variant and read model (Sprint 7); putaway of returned parcels (reverse-QC, Sprint 16).

## Open questions

- [x] **Q1 (module), decided D1 (plus list selection, PUT-19): what is putaway-able?** Default above (accepted quantity from QC, active GTIN required, tag printing informational). Alternative: block putaway until the tags for that quantity are printed. Blocks: PUT-1/PUT-4.
- [x] **Q2 (module): concession stock.** Default: the grade is kept on the item, in the ledger and in the available quantity breakdown. Blocks: the stock schema.
- [x] **Q3 (module): voided GTIN.** Default: refuse at the scan (PUT-7).
- [x] **Q10: NFR-SC-01 scope.** The BRD ties the 300 ms scan-to-confirm to pick and pack; SCRUM-120 says "scan endpoint". Default: apply it to the putaway scan lookup too (PUT-17).
- [x] **Q-PUT-1: Over-receipt.** If staff find more units than were accepted, the default is to refuse in putaway and record the extra through a `FOUND_STOCK` adjustment. Who decides: owner.
