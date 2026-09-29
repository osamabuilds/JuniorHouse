# SCRUM-197: Receive & Inspect (goods receipt, QC, vendor scorecard)

- **Status:** Draft (awaiting approval; open questions Q1–Q10 are unresolved, recommended defaults are proposals only)
- **Jira:** SCRUM-197 (provisional key for the spec/plan gate; real keys are assigned when the Sprint 4 tickets are created; next free is SCRUM-197). Build tickets sit under epic SCRUM-15 "[Module] Quality Control (QC)". SCRUM-94 (FR-SC-04) is completed here (GRN half; delivery-note half was Sprint 3). SCRUM-95 (FR-SC-05) and SCRUM-91's scorecard remainder are linked.
- **BRD sections:** §5.11 (Quality Control & Goods Receipt), §7.10 (FR-SC), §9.10 (QC module, event catalogue), §12.11 (Production & QC tables)
- **Requirement IDs:** FR-SC-04 (GRN half), FR-SC-05, FR-SC-01 (scorecard clause), NFR-SC-05, NFR-FT-08, NFR-17, NFR-19. No `TC-*` cases exist in the BRD for this area, so every acceptance criterion below is derived from the BRD text and is marked `(new)` where the BRD is silent.
- **Folder:** `docs/specs/SCRUM-197-receive-inspect/` (provisional name; rename when the real key is assigned)

**Builds on Sprint 1–3 as delivered:**
- PROD records each vendor delivery note (DN) against a PO: `DLVR_NOTE` / `DLVR_NOTE_LINE` with vendor DN number, dispatch date, quantities per size × colour, final-shipment flag, and the judged facts (timing `OnTime` / `Late` / `BeyondAcceptable` with days late; quantity `OnQuantity` / `Short` / `Over` with over/short totals; revision judged against; `RompFabricDelay`).
- PROD publishes `DeliveryNoteRecorded` v1 through its outbox (`Production.Contracts`). It carries run id, PO id/no, vendor DN no, dispatch date, final flag, PO outcome, timing/quantity codes, days late, over/short qty, revision number, `RompFabricDelay` and lines (size id, colour id, qty). It does **not** carry a DN row id: the natural key is `(RunId, VendorDnNo)`.
- S3 decision D4: over-delivery beyond tolerance is recorded and flagged, "accepting or rejecting the physical excess belongs to Sprint 4's GRN/QC".
- S3 decision D2: closing a PO is manual. PO states run Draft → Sent → Acknowledged → In Production → Partially Delivered → Delivered → Closed (or Cancelled). The DN alone drives Partially Delivered / Delivered.
- S3 decision D14 / AC-37: the fact "Romp fabric delay" is stored on each DN so that Romp-caused delay does not count against the vendor's on-time score. The scorecard (this sprint) applies it.
- The shared dispatcher fans each outbox message out to every registered handler; each consuming module has its own inbox (`INBX`) for idempotency (SCRUM-181/186). `Vendor.Contracts` exposes `IPurchaseOrderQueries` (in-force terms, terms at a date, revisions, files, production context); `Production.Contracts` exposes `IProductionQueries`.
- Business numbers use ADR 0006 (`{PREFIX}-{YYYY}-{NNNNN}`, atomic upsert on a per-module counter table). ADR 0006 already names GRNs (S4) as a user.
- There is no authentication until Sprint 8; actions are attributed to the system actor (`ICurrentActor`) plus a required, free-text acting-person name (S3 D13).

## Problem & intent

When a vendor's delivery reaches Romp's warehouse, Osama needs to record what actually arrived, compare it with what the vendor claimed, and decide, batch by batch and with a checklist that includes child safety, which quantities are good enough to sell. Today a DN says what was *sent*, nothing says what was *received*, and nothing stops a rejected garment from reaching a customer. In a children's apparel brand a safety recall is a far worse failure than a returned order, so two properties are non-negotiable: rejected (or not-yet-decided) quantity can never become sellable, and no quantity is accepted without the child-safety checks.

The sprint also closes the vendor-selection loop the BRD has promised since Sprint 1: a simple running scorecard (on-time, on-quantity, defect rate) fed by the facts PROD already records and the QC results recorded here.

## Requirements (quoted from the BRD)

> **§5.11 Quality Control & Goods Receipt.** When a delivery note (DN) arrives at Romp's warehouse against a PO:
> - Goods Receipt Note (GRN) opened, referencing the PO and DN; counted quantity recorded per size/colour and reconciled against what the vendor claims to have sent.
> - QC sampling per AQL (Acceptable Quality Level) principles — for a small/medium batch, inspect a statistically representative sample (e.g. AQL 2.5 tables are the garment-industry norm) rather than every unit, unless volume is small enough to inspect 100%.
> - Each inspected unit checked against the tech pack: stitching, measurements (spot-check against size chart), fabric/colour match to the approved sample, trims/buttons/zippers secure, no visible defects, correct care-label content.
> - Outcome per batch: Accept, Accept with concession (minor defects, sold as-is or discounted), Reject & return to vendor, or Rework (sent back to vendor for correction, re-inspected on return).
> - Only Accepted (and Accept-with-concession, tagged accordingly) quantities proceed to SKU-GEN/putaway; rejected units never enter sellable inventory.
> - Edge case: partial batch fails QC → only the failing sub-batch is rejected/reworked; the passing portion should not be held hostage waiting for the vendor to fix the rest.
> - Non-negotiable per Section 8.7 of v8.0 (Child Data Privacy) — QC on garments for children additionally checks for choking-hazard risk on small trims (buttons, bows) and drawstring safety on necklines, since this is a children's apparel brand and a safety recall is a far worse failure mode than a returned order.

> **FR-SC-01 (scorecard clause):** System shall support creating a Vendor record with contact, specialisation, payment terms, and a running on-time/on-quantity/defect-rate scorecard.

> **FR-SC-04:** System shall support partial deliveries against a single PO, each recorded as its own Goods Receipt Note.

> **FR-SC-05:** System shall record QC results per batch (Accept / Accept-with-concession / Reject / Rework) with defect notes, and shall block rejected quantity from ever reaching sellable inventory.

> **§5.10 (edge case, carried from Sprint 3):** vendor delivers late or short → PO record should flag this against the vendor's on-time/on-quantity history, feeding future vendor-selection decisions (a simple scorecard, not a full vendor-rating engine at MVP).

> **§9.10.1 Quality Control (QC):** Inbound QC records, AQL sampling results, defect logging, accept/reject/rework decisions.

> **§9.10.3 Event catalogue (supply-chain side):** `GoodsReceipt.Recorded` — published by QC, consumed by VEND (closes/partially-closes PO). `QC.BatchAccepted` / `QC.BatchRejected` — published by QC, consumed by SKU-GEN (on accept), VEND (on reject, for vendor scorecard).

> **§12.11:** Production & QC core tables: ProductionMilestones, GoodsReceiptNotes, QCBatches, QCDefectRecords.

> **NFR-SC-05:** All inventory-adjusting actions … must be attributable to a user/session and timestamped …

*(Scope notes. (1) SKU-GEN and putaway are Sprint 5+: this sprint exposes the *released quantity* through `Quality.Contracts` and publishes the events; it builds no barcode or stock ledger. (2) The BRD cites "Section 8.7 of v8.0" for the child-safety checklist; that section is not in the merged document, so the exact checklist is an open question (Q6). The two checks named in §5.11 are the minimum built here. (3) §9.10.3 says `GoodsReceipt.Recorded` "closes/partially-closes" the PO; S3 decision D2 says closing is manual. Q7 records the conflict without picking a side.)*

## User flow

1. PROD's `DeliveryNoteRecorded` reaches QC (inbox). QC keeps its own copy of each inbound DN (the **receiving queue**): PO, vendor DN number, dispatch date, claimed lines. (new)
2. When the parcels arrive at the warehouse, staff open the DN in the receiving queue and **open a GRN** for it: receipt date, acting person, and the **counted quantity per size × colour**. QC shows claimed vs counted and the difference per line and overall (Matched / Short / Over). Discrepancies are shown loudly and never block the receipt. There is one GRN per DN; a PO with several DNs has several GRNs (FR-SC-04).
3. Recording the GRN writes `GoodsReceipt.Recorded` to the outbox and opens one **QC batch** for the GRN, with the counted quantities as its lines, status Pending. (new: the BRD says "batch" without defining it; here one batch per GRN.)
4. Staff start an **inspection**. QC works out the sample size from the lot size using the AQL table (or 100% for a small lot), staff inspect the sample against the tech-pack checklist (stitching, measurements, fabric/colour match, trims secure, visible defects, care label), log **defects** (type, severity, quantity, note) and record the two mandatory **child-safety checks** (choking hazard on small trims, drawstring safety on necklines). The AQL result (pass/fail against the accept/reject numbers) is computed.
5. Staff record the **outcome**. If the whole batch shares one outcome, it is decided at once. If only part fails, they **split** the batch into sub-batches by size × colour quantity; each sub-batch gets its own outcome (Accept, Accept with concession, Reject, Rework), so the passing part proceeds without waiting for the vendor.
6. **Accept / Concession** quantities become *released* (concession tagged as such) and `QC.BatchAccepted` is written. **Reject** writes `QC.BatchRejected` (the vendor scorecard consumes it). **Rework** sends the sub-batch back to the vendor; when it returns, staff record the returned quantity and it is **re-inspected** (a new inspection round on that sub-batch), ending in a new outcome.
7. The **vendor scorecard** (on-time %, on-quantity %, defect rate) is updated from DN facts and QC results and shown on the vendor screen.
8. The admin sees a **Receiving & QC** area: receiving queue, GRN form, inspection screen with the outcome panel, and the scorecard on the vendor screen.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| AC-1 | Given a `DeliveryNoteRecorded` message, when it is delivered, then QC stores one inbound-delivery record for that `(RunId, VendorDnNo)` with its claimed lines and it appears in the receiving queue. | §5.11 "delivery note arrives" (new mechanism) |
| AC-2 | Given the same message is delivered twice, when both are handled, then still exactly one record exists (inbox makes the second a no-op). | ADR 0004, SCRUM-181 (new) |
| AC-3 | Given an inbound DN with no GRN, when staff open a GRN, then the GRN references the PO and the DN, stores the receipt date, the acting person and the counted quantity per size × colour, and receives a generated GRN number (`GRN-YYYY-NNNNN`, ADR 0006). | §5.11, FR-SC-04, ADR 0006 |
| AC-4 | Given a DN that already has a GRN, when a second GRN is opened for it, then it is refused as a duplicate (one GRN per DN). | FR-SC-04 (new) |
| AC-5 | Given a DN for a PO that is Cancelled, or an unknown DN, when a GRN is opened, then it is refused with the reason. Given a cancelled PO, the receiving queue does not list its DNs as open. | §5.9 (new) |
| AC-6 | Given counted quantities, then every line's size × colour must be on the DN or on the PO's in-force terms, and quantities must be whole numbers ≥ 0; otherwise the GRN is refused naming the line. | §5.11 (new) |
| AC-7 | Given a GRN, when it is recorded, then per line and in total the claimed quantity, counted quantity and difference are stored and shown, and the GRN carries a reconciliation status `Matched` / `Short` / `Over`. A discrepancy never blocks the GRN. | §5.11 "reconciled against what the vendor claims" |
| AC-8 | Given the receipt date, then it must be on or after the DN dispatch date and not after today (Pakistan time), and the acting person's name is required (2–100 characters). | NFR-SC-05, S3 D13 (new) |
| AC-9 | Given several DNs on one PO, then each gets its own GRN and the PO's receiving history lists all of them with their totals. | FR-SC-04 |
| AC-10 | Given a GRN is recorded, then `GoodsReceipt.Recorded` is written to the QC outbox in the same transaction, with GRN id/no, PO id/no, vendor DN no, receipt date, per-line claimed and counted quantities and the reconciliation status. A notification or consumer failure never rolls back the receipt. | §9.10.3, ADR 0004, NFR-FT-08 |
| AC-11 | Given a GRN whose counted quantity on a line exceeds the DN's claimed quantity, then the excess is shown as an over-delivery on the GRN and the QC batch, and is handled as decided in Q5 (recommended: received and inspected with the batch, and rejectable through a sub-batch split like any other quantity). | S3 D4 |
| AC-12 | Given a recorded GRN, then exactly one QC batch exists for it in status Pending, whose lines equal the counted quantities per size × colour. | §5.11 "QC per batch" (new definition) |
| AC-13 | Given a batch lot size, an inspection level and an AQL level, then the sample size and the accept/reject numbers come from a seeded, table-driven AQL plan (single sampling, normal inspection) through a pure calculator, never from a formula in a handler. The plan values are covered by table tests. | §5.11 AQL |
| AC-14 | Given a lot no larger than the configured full-inspection threshold, then the inspection is 100% (sample = lot) and marked as such. | §5.11 "unless volume is small enough to inspect 100%" |
| AC-15 | Given any lot, then the sample size never exceeds the lot size and is at least 1. | AC-13 (new) |
| AC-16 | Given an inspection, when it is recorded, then it stores the sampled quantity actually inspected, the acting person, the inspection date and one checklist result (`Pass` / `Fail`) for each tech-pack item: stitching, measurements, fabric/colour match, trims secure, visible defects, care label. | §5.11 |
| AC-17 | Given a defect is logged, then it stores defect type, severity (`Critical` / `Major` / `Minor`), quantity of units affected, optional size × colour, and a mandatory note. A failed checklist item requires at least one defect record of its type. | FR-SC-05 "with defect notes" |
| AC-18 | Given the defect counts of an inspection, then the AQL result is `Pass` when the countable defects are at or below the accept number, `Fail` when at or above the reject number, computed by the domain and stored, never typed in. Which severities count is Q1. | §5.11 AQL |
| AC-19 | Given a batch with no recorded inspection, when anyone tries to record an outcome, then it is refused naming the missing inspection. | FR-SC-05 (new) |
| AC-20 | Given a batch, when a decision is recorded, then each sub-batch's outcome is exactly one of Accept, Accept with concession, Reject (and return to vendor), Rework, and a reason is mandatory for Concession, Reject and Rework. | §5.11, FR-SC-05 |
| AC-21 | Given a Concession outcome, then the released quantity carries the tag `Concession` everywhere it is exposed. The sold-as-is-or-discounted decision is not made here (Q3). | §5.11 "tagged accordingly" |
| AC-22 | Given a batch where only part fails, when staff split it, then each sub-batch is a set of size × colour quantities, the sub-batch quantities for every size × colour sum exactly to the batch's counted quantity (no unit lost or double-counted), and each sub-batch is decided independently. | §5.11 edge case |
| AC-23 | Given a batch split into a passing part and a failing part, when the passing sub-batch is accepted, then its quantity is released immediately, with no dependency on the failing sub-batch's rework or return. | §5.11 edge case ("not held hostage") |
| AC-24 | Given any quantity that is Rejected, Rework, Pending, in inspection, or otherwise undecided, then it is never part of the released quantity returned by `Quality.Contracts`, and no code path adds it. The only released quantities are Accepted and Concession. | FR-SC-05, §5.11 |
| AC-25 | Given released quantities per size × colour for a batch, then their sum can never exceed the batch's counted quantity, and a decided sub-batch cannot be decided again. | FR-SC-05 (new) |
| AC-26 | Given a Rework sub-batch, then its status is Awaiting rework until the vendor returns it. When staff record the return (returned quantity per size × colour, not more than was sent, acting person, date), a new inspection round opens on that sub-batch and it is re-inspected and decided again. The number of rework rounds is tracked and shown, with no limit. | §5.11 "re-inspected on return", (new) |
| AC-27 | Given returned rework quantity smaller than what was sent, then the difference stays visible as outstanding on the sub-batch; it is not silently rejected or accepted. | (new) |
| AC-28 | Given a Reject outcome, then the rejected quantity and reason are recorded and `QC.BatchRejected` is written. Whether a return or debit note is created is Q4; this sprint only records the reject. | §5.11, §9.10.3 |
| AC-29 | Given a decision, then `QC.BatchAccepted` (Accept and Concession quantities, with tag), `QC.BatchRejected`, or a rework-requested event is written to the QC outbox in the same transaction, with sub-batch id, batch id, PO id/no, lines and the tag, and never internal notes, defect photos or personal data. | §9.10.3, ADR 0004 |
| AC-30 | Given every inspection, defect, safety result, decision and rework return, then it stores the acting person's name (required, 2–100 characters) alongside the system actor and a timestamp. | NFR-SC-05, S3 D13 |
| AC-31 | Given an inspection, then both child-safety checks (choking hazard on small trims; drawstring safety on necklines) must have a result recorded (`Pass`, `Fail`, or `NotApplicable`) before any Accept or Concession outcome is allowed on that batch. A missing check refuses the decision naming the check. | §5.11 (non-negotiable) |
| AC-32 | Given a safety check recorded as `Fail`, then the affected quantity (the listed size × colour lines, or the whole batch when none are listed) can only be decided Reject or Rework; Accept and Concession are refused for it, with no override. | §5.11 (non-negotiable) |
| AC-33 | Given a safety check recorded as `NotApplicable` (for example a garment with no small trims), then a reason is mandatory. | (new) Q9 |
| AC-34 | Given a rework return, then the safety checks are recorded again on the new inspection round before the sub-batch can be accepted. | AC-31 (new) |
| AC-35 | Given a vendor, then the scorecard shows on-time %, on-quantity % and defect rate with the counts they come from (deliveries counted, inspected units, defective units) and the window used, computed from the formulas in Q2 once approved. | FR-SC-01 |
| AC-36 | Given a delivery whose DN carries `RompFabricDelay`, then it is excluded from the vendor's on-time score and the exclusion is visible as a count. | S3 AC-37, D14 |
| AC-37 | Given a vendor with no deliveries or no inspections yet, then the scorecard shows "no data" for that measure, never 0% or 100%. | (new) |
| AC-38 | Given the same DN or QC event delivered twice, then the scorecard counts it once (inbox); given a re-inspection after rework, then the defect rate treats it as decided in Q2. | ADR 0004 (new) |
| AC-39 | Given the vendor screen, then it shows the scorecard, and `Vendor.Contracts` exposes a read-only scorecard query for other modules. | FR-SC-01, ADR 0002 |
| AC-40 | Given `GoodsReceipt.Recorded`, when VNDR consumes it, then it is applied idempotently and forward-only in the way decided in Q7 (recommended: VNDR records the received quantity progress for the PO and changes no PO status). | §9.10.3 |
| AC-41 | Given a GRN, a batch decision or a scorecard-relevant QC event, then no PO field other than the ones Q7 allows is changed by VNDR, and the PO's cancel/close rules from Sprint 3 are unchanged. | S3 D2 |
| AC-42 | Given more than one page of receiving-queue items, GRNs or batches, then each list endpoint is paged with a stable order and total, and its query count does not grow with the number of rows. | CONVENTIONS.md §4 |
| AC-43 | Given two staff deciding the same batch or GRN at once, then the concurrency token makes the second fail with a conflict instead of both applying. | NFR-08 spirit (new) |
| AC-44 | Given the admin app, then the receiving and QC screens show names instead of ids, are usable from 320 px, use one `<h1>` and semantic landmarks, every form control has a label, and the flags (over, short, safety fail, concession, awaiting rework) are text as well as colour. | NFR-17, NFR-19 (new) |
| AC-45 | Given any QC screen or event, then it never exposes the PO's internal notes, cost sheets or target costs. | S2 information rule (new) |
| AC-46 | Given the QC seed and configuration, then AQL level, inspection level, full-inspection threshold and scorecard window are configuration or seeded lookup values, not constants in handlers. | CLAUDE.md DB design (new) |

## Non-functional constraints

- **ADR 0002:** QC never reads PROD or VNDR tables. It uses `Production.Contracts` / `Vendor.Contracts` queries and events. VNDR learns about QC only through QC's events and a read-only `Quality.Contracts` query. `tests/Romp.ArchitectureTests` gets the new module in `ModuleNames`.
- **ADR 0004 / NFR-FT-08:** every state change and its event share one transaction; delivery to consumers is at-least-once and consumers are idempotent (inbox).
- **NFR-SC-05:** every receipt, inspection, decision and rework return is attributable and timestamped (system actor plus named person until Sprint 8).
- **SEC-25 / child safety:** logs carry no free-text defect notes beyond what the logging behaviour already redacts. (No Child Profile data is touched in this sprint.)
- **SEC-05 / SEC-08:** no roles or ownership until Sprint 8; endpoints are marked for the S8 sweep.
- **CONVENTIONS.md:** feature folders and namespaces, one `IEntityTypeConfiguration` per entity, paged lists, no N+1, NgRx feature store, containers vs components.
- **DB rules (`docs/db/naming.md`):** schema `QC`, abbreviated uppercase names, lookup tables for every enumerated value (outcomes, defect types/severities, safety checks and results, statuses, AQL levels), audit columns and concurrency token, declared and indexed FKs, new abbreviations added to the glossary in the same change (group B only).
- **Testing:** requirement-tagged xUnit tests, Testcontainers for database tests (CI is the authority for integration tests, per the environment note), Playwright for the journey.

## Out of scope

- **SKU / GTIN generation, tag printing, putaway, ATP and any stock ledger** (Sprint 5+). This sprint exposes released quantities and events only.
- **Returns-to-vendor logistics, debit notes, credit notes and payment adjustments** for rejected goods, unless Q4 says otherwise.
- **Discount pricing of concession stock** (Q3): only the tag is recorded.
- **Defect photos or attachments.** The BRD asks for defect *notes*; attachments are not required. (new)
- **Correcting a decided outcome.** A decided sub-batch is final in this sprint; a correction path is raised with the product owner if a real need appears. (new)
- **Per-vendor or per-style AQL configuration** and **skip-lot / tightened / reduced inspection switching**, unless Q1 says otherwise.
- **Notification delivery** for QC events (SCRUM-13). Events are written; real consumers come later.
- **Vendor login or self-service** (no authentication before Sprint 8).
- **Phase 2/3 features** and the descoped Fit Finder / Virtual Try-On.

## Open questions

Every question below is unresolved. The "Recommended default" is a proposal so work can be planned; nothing is decided until the product owner approves. **Status of every default: awaiting approval.** Q1–Q6 come from the sprint plan; Q7–Q10 were found while writing this spec.

- [ ] **Q1. AQL table and inspection level.** The BRD says "e.g. AQL 2.5". Fixed default, configurable per vendor, or per style? Blocks AC-13, AC-14, AC-18, AC-46. *Recommended default (awaiting approval):* one global default, ISO 2859-1 single sampling, normal inspection, general level II, AQL 2.5 for Major and Minor defects counted together, **zero tolerance for Critical** (any Critical defect fails the lot), all read from configuration and seeded lookups so per-vendor or per-style values can be added later without a schema change. Full inspection (100%) at or below a lot of **50 units** (`Qc:FullInspectionMaxLot`).
- [ ] **Q2. Scorecard formulas and window.** BRD only says "simple scorecard". Blocks AC-35..38. *Recommended default (awaiting approval):* **on-time %** = deliveries with timing `OnTime` ÷ deliveries, excluding `RompFabricDelay` deliveries (shown as a separate count), with `Late` and `BeyondAcceptable` both counting as not on time (the split is shown); **on-quantity %** = deliveries with quantity `OnQuantity` ÷ deliveries; **defect rate** = defective units ÷ inspected units over the vendor's **first-pass** inspections (a rework re-inspection does not change it, so the rate measures what the vendor shipped, not what Romp fixed). Window: **all time**, with a **rolling 90 days** figure shown alongside (`Vndr:Scorecard:WindowDays`).
- [ ] **Q3. Concession handling.** "Sold as-is or discounted" needs a tag now; does the discount decision belong to S4 or S7 Publish? Blocks AC-21. *Recommended default (awaiting approval):* S4 records the outcome `Concession`, a mandatory reason, and the tag on the released quantity; the sold-as-is or discounted decision is made in **S7 Publish**. No price is set in S4.
- [ ] **Q4. Reject and return to vendor.** Is a return or debit note in scope, or only recording the reject? Blocks AC-28. *Recommended default (awaiting approval):* **only record the reject** (quantity, reason, `QC.BatchRejected`); physical return and any debit note are handled outside the system until a later sprint.
- [ ] **Q5. Over-delivery excess.** S3 D4 deferred it to the GRN: accept, reject, or hold? Blocks AC-11. *Recommended default (awaiting approval):* the excess is **counted, flagged as over-delivery, and inspected with the rest of the batch**; staff can reject it through a normal sub-batch split. Nothing is auto-accepted and no separate "hold" state is created.
- [ ] **Q6. Child-safety checklist.** The BRD points at "Section 8.7 of v8.0", which is not in the merged document. What is the exact checklist? Blocks AC-31..34. *Recommended default (awaiting approval):* build exactly the two checks named in §5.11 (`ChokingHazardTrims`, `DrawstringNeckline`), stored as a lookup so more can be added by a row, and ask the product owner for the v8.0 text before Sprint 4 is called complete.
- [ ] **Q7. What a GRN does to the PO.** §9.10.3 says `GoodsReceipt.Recorded` "closes/partially-closes" the PO in VEND; S3 D2 says closing is manual, and S3 already moves the PO to Partially Delivered / Delivered from the DN. Do the two conflict? Blocks AC-40, AC-41 and group H. *Recommended default (awaiting approval):* honour D2: VNDR consumes the event **idempotently and forward-only, records received quantities per PO (receipt progress), and changes no PO status**; the manual Close step is unchanged. Raised, not resolved, per CLAUDE.md.
- [ ] **Q8. Scope of a "batch".** The BRD never defines it (S3 D9: one production run per PO, the DN is the lot). Blocks AC-12. *Recommended default (awaiting approval):* **one QC batch per GRN** (so per DN); sub-batches exist only after a split.
- [ ] **Q9. Can a safety check be "not applicable"?** Some garments have no small trims or no neckline drawstring. Blocks AC-31, AC-33. *Recommended default (awaiting approval):* allowed, but only with a mandatory reason, and never for a check that failed elsewhere on the same batch.
- [ ] **Q10. Small-lot threshold and safety scope on partial fails.** How is a safety failure scoped when only some lines are affected (AC-32)? *Recommended default (awaiting approval):* the inspector lists the affected size × colour lines; none listed means the whole batch. Those lines can only be Reject or Rework.

## Implementation notes

*(Filled in when the sprint closes, per CLAUDE.md step 5.)*
