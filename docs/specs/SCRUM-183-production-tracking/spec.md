# SCRUM-183: Production Tracking (milestones, PP-sample gate, delivery notes)

- **Status:** Approved (2026-09-29, decisions D1–D13 below given by the product owner)
- **Jira:** SCRUM-183 (spec/plan gate); build tickets SCRUM-184..196 under epic SCRUM-74 "[Module] Production Tracking (PROD)". SCRUM-94 (FR-SC-04) is split: delivery notes here, GRN in Sprint 4.
- **BRD sections:** §5.9 (PO status lifecycle), §5.10 (Production Tracking), §5.11 (what follows: GRN/QC), §7.10 (FR-SC), §9.10 (PROD module, event catalogue), §12.11 (Production tables)
- **Requirement IDs:** FR-SC-04 (delivery-note half), FR-SC-01 (scorecard data only), NFR-SC-05 (attributable, timestamped actions). No `TC-*` cases exist in the BRD for this area, so every acceptance criterion below is derived from the BRD text and the decisions, and is marked `(new)` where the BRD is silent.
- **Folder:** `docs/specs/SCRUM-183-production-tracking/`

**Builds on Sprint 1/2 as delivered:**
- PO states Draft → Sent to Vendor → Acknowledged, plus Cancelled with a mandatory reason. The PO row mirrors the **In-force revision** (ADR 0007). Revisions are numbered; an Acknowledged PO changes only through an amendment.
- The PO carries **latest acceptable date**, **over/under tolerance %** and **fabric responsibility** (`VendorSupplied` / `RompSupplied`), added in Sprint 2 so Sprint 3/4 can judge "short", "late" and "on time".
- `VNDR.OUTB_MSG` events (`PoCreated`, `PoSentToVendor`, `PoAcknowledged`, `PoCancelled`, and the five revision events) carry message ID, schema version, aggregate id and `PO_NO`, plus the terms snapshot. The shared dispatcher (SCRUM-181) delivers them to per-module handlers; each consuming module has its own inbox for idempotency.
- `Romp.Modules.Vendor.Contracts` exposes read-only queries (In-force terms, revisions, effective files).
- Sprint 2's attachment infrastructure (`IFileStorage`, content-signature type check, size/count limits, generated storage keys, safe download headers) exists inside the Vendor module.
- Vendor communications (Sprint 2) record how, who responded (free-text name), when, and evidence; the response time is bounded between the PO's Send date and now.
- There is no authentication until Sprint 8, so actions are attributed to the system actor (`ICurrentActor`) today.
- Vendors do not use this system: they confirm by WhatsApp and phone. Staff record what the vendor said.

## Problem & intent

Once a vendor acknowledges a PO, Romp has no visibility until goods arrive. Osama needs to know whether the vendor has booked fabric, whether the pre-production sample has been checked, when bulk production is expected to finish, and, when the vendor ships in several lots, what has actually been sent against the PO and whether it is late, short or over. The pre-production (PP) sample is the cheapest point to catch a fit or fabric mistake, so bulk cutting must not be recorded as started until a sample is approved.

This is milestone-level visibility, not a factory-floor system. The sprint also records the facts (late, short, over) that the Sprint 4 vendor scorecard is computed from, and moves the PO through the rest of its BRD lifecycle.

## Decisions (product owner, 2026-09-29)

| # | Decision | Replaces open question |
|---|---|---|
| D1 | **In Production** fires when **bulk cutting starts**, not at Acknowledged. | 1 |
| D2 | **Closing a PO is manual.** | 2 |
| D3 | A **"final shipment" marker** on a delivery note is required before a short delivery lets the PO count as Delivered. | 3 |
| D4 | **Over-delivery beyond the over-tolerance is accepted and flagged**, never refused. The delivery note is still recorded; the flag is loud and visible (same non-blocking pattern as Sprint 2's "beyond latest acceptable date"). Accepting or rejecting the physical excess belongs to Sprint 4's GRN/QC. | 4 |
| D5 | **Cancelling after production started is allowed** in any state before Delivered/Closed, never hard-blocked. Mandatory reason (extend `PO_CNCL_RSN_LKP` with a production-stage reason). The UI shows a prominent sunk-cost / vendor-impact warning before confirming. It is not restricted to "before PP approval". | 5 |
| D6 | **No limit on PP-sample rounds.** The round number and history are tracked and shown; informational, not a gate. | 6 |
| D7 | A PP-sample round **can carry photos/files**, reusing Sprint 2's attachment infrastructure (`IFileStorage`, content-signature validation), not a parallel system. | 7 |
| D8 | **Finished / ready-to-ship quantity** can be recorded as an **optional** milestone before the delivery note. Not mandatory; gates nothing. | 8 |
| D9 | **One production run per PO.** No separate Batch entity. Partial progress is tracked by delivery notes with line-level quantities. Revisit only if a real need for independently approved production lots inside one PO appears. | 9 |
| D10 | **Milestone set:** `FabricBooked` (optional, only when fabric responsibility is `VendorSupplied`) → `PPSampleSubmitted` → `PPSampleApproved` / `PPSampleRejected` (repeatable rounds) → `BulkCuttingStarted` (blocked until a sample is approved) → `FinishedReadyToShip` (optional) → delivery note(s). **Repeat styles do not skip PP sampling** (dye-lot variation is real). This may be revisited later, gated on the vendor's Sprint 4 scorecard record, not on style repetition. | 10 |
| D11 | **Back-dating** a milestone is allowed only between the PO's **Send date** and **today** (never before the PO was sent, never in the future), the same bound as Sprint 2's vendor-communication response time. If a milestone is recorded more than a **configurable number of days** after its claimed date, show a non-blocking **"recorded late"** indicator. | 11 |
| D12 | **Late / short / over is judged against the revision in force on the delivery note's actual dispatch date**, never the date it was entered. | 12 |
| D13 | Until Sprint 8, approvals are recorded as the system actor **and** the UI asks for the acting person's name as free text (same pattern as Sprint 2's vendor-communication "responder name"), so the audit trail stays meaningful once real auth exists. | 13 |

## Requirements (quoted from the BRD)

> **FR-SC-04:** System shall support partial deliveries against a single PO, each recorded as its own Goods Receipt Note.

*(Scope note: the "own GRN" half is built in Sprint 4 with QC, §5.11. This sprint records each partial delivery as its own **vendor delivery note** against the PO, which the GRN will later reference.)*

> **FR-SC-01 (scorecard clause):** …a running on-time/on-quantity/defect-rate scorecard.

*(This sprint records the on-time and on-quantity facts; the scorecard is computed in Sprint 4.)*

> **NFR-SC-05:** All inventory-adjusting actions … must be attributable to a user/session and timestamped …

*(Applied to approvals and delivery records here, as they will become the basis of stock.)*

> **§5.9, PO status lifecycle:** Draft → Sent to Vendor → Acknowledged → In Production → Partially Delivered → Delivered → Closed. Each transition is a domain event (Section 5).

> **§5.10 Production Tracking:** Romp is not running factory-floor MES (Manufacturing Execution System) — that level of granularity belongs to the vendor. What Romp needs is milestone-level visibility:
> - Fabric sourced/cut (optional milestone, vendor-reported)
> - Sampling — a pre-production sample (PP sample) is approved by Osama before bulk production starts; this is a hard gate, not optional, since it is the cheapest point to catch a fit/fabric mistake.
> - Bulk production in progress, with an expected completion date
> - Ready for dispatch from vendor — quantity actually finished may be less than ordered (short-shipment is common in local manufacturing); the system must support partial deliveries against one PO, each with its own delivery note.
>
> Edge case: vendor delivers late or short → PO record should flag this against the vendor's on-time/on-quantity history, feeding future vendor-selection decisions (a simple scorecard, not a full vendor-rating engine at MVP).

> **§9.10.1 Production Tracking (PROD):** PO-to-batch mapping, expected vs actual delivery dates, partial-shipment tracking. Primary users: Osama / future ops staff.

> **§9.10.3 Event catalogue:** `PurchaseOrder.Confirmed` — published by VEND, consumed by PROD (starts tracking), Notifications. `Production.MilestoneReached` — published by PROD, consumed by Admin dashboard, Notifications. `GoodsReceipt.Recorded` — published by QC, consumed by VEND (closes/partially-closes PO).

> **§12.11:** Production & QC core tables: ProductionMilestones, GoodsReceiptNotes, QCBatches, QCDefectRecords. *(Sprint 3 owns ProductionMilestones and the delivery-note tables; GRN and QC tables are Sprint 4.)*

*(D9 reads §9.10.1's "PO-to-batch mapping" as: one run per PO; a delivery note is the batch a later GRN references.)*

## User flow

1. Staff acknowledge a PO (Sprint 1/2). The `PoAcknowledged` event reaches PROD, which **opens a production run** for the PO (no staff action). The run's milestone list follows D10; `FabricBooked` exists only when the PO's fabric responsibility is `VendorSupplied`.
2. As the vendor reports progress by WhatsApp/phone, staff **record each milestone**: date it happened (within D11's bounds), who reported it (vendor contact), the acting person's name (D13), optional note. Optional milestones may be skipped.
3. **PP sample:** the vendor submits a sample (staff record the submission with an optional note and photos/files, D7). Staff inspect it, then **approve** or **reject** with a reason. A rejection opens the way for another round (D6). **`BulkCuttingStarted` cannot be recorded until a round is approved.**
4. When bulk cutting is recorded, the PO becomes **In Production** (D1). Staff set the **expected completion date** and update it when the vendor says it moves; the history is kept and compared with the PO's In-force expected and latest acceptable dates.
5. Optionally, staff record **finished / ready-to-ship** quantities per size × colour (D8) when the vendor reports them. Nothing depends on it.
6. When the vendor dispatches goods, staff **record a delivery note (DN)**: vendor's DN number, dispatch date, quantities shipped per size × colour, and whether it is the **final shipment** (D3). A PO can have several DNs. Each DN is judged on entry (D12) and always recorded (D4).
7. The PO becomes **Partially Delivered** after the first DN that leaves quantity outstanding; **Delivered** when the shipped total reaches the ordered quantity within the under-tolerance, **or** when a DN marked final shipment is recorded (a short final shipment stays visible as short). Staff **close** a Delivered PO manually (D2).
8. **Cancel** is available on any In Production or later state before Delivered/Closed, with a mandatory reason and the sunk-cost warning (D5).
9. The admin sees a paged list of production runs and a run detail with the milestone timeline, PP rounds and DN history. The PO detail shows the production status.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| AC-1 | Given a PO is acknowledged, when the event is delivered, then one production run exists for that PO with the milestone list for its fabric responsibility (D10). | §9.10.3 `PurchaseOrder.Confirmed`, §5.10 (new) |
| AC-2 | Given the same `PoAcknowledged` message is delivered twice, when both are handled, then still exactly one run exists (the inbox makes the second a no-op). | ADR 0004, SCRUM-181 (new) |
| AC-3 | Given a PO that is not acknowledged (Draft, Sent, Cancelled), then no run exists and no milestone can be recorded against it. | §5.9 (new) |
| AC-4 | Given fabric responsibility `VendorSupplied`, then the run offers `FabricBooked`; given `RompSupplied`, then it does not. | D10, S2 forward rule (new) |
| AC-5 | Given a run, when staff record a milestone, then it stores the claimed date, the vendor contact who reported it, the acting person's name, the system actor, and the recording timestamp. | §5.10, NFR-SC-05, D13 (new) |
| AC-6 | Given a milestone date before the PO's Send date or after today, then it is refused with the allowed range stated. | D11 |
| AC-7 | Given a milestone recorded more than the configured number of days after its claimed date, then it is saved and shown with a non-blocking "recorded late" indicator. | D11 |
| AC-8 | Given the optional milestones (`FabricBooked`, `FinishedReadyToShip`) are never recorded, then later milestones can still be recorded. | §5.10, D8, D10 |
| AC-9 | Given no approved PP-sample round, when anyone tries to record `BulkCuttingStarted`, then it is refused, naming the missing approval. | §5.10 hard gate, D10 |
| AC-10 | Given a submitted PP sample, when staff approve it, then the approver name, actor and time are recorded and `BulkCuttingStarted` becomes recordable. | §5.10, NFR-SC-05 |
| AC-11 | Given a submitted PP sample, when staff reject it, then a reason is mandatory, the round closes as rejected, and a new round can be submitted with no limit. Each round shows its number and history. | D6 |
| AC-12 | Given a PP round, when photos/files are attached, then they pass Sprint 2's content-signature check and size/count limits, are stored with generated keys, and download with safe headers. A disallowed file is refused with the reason. | D7, Sprint 2 AC-45/46 |
| AC-13 | Given an approved round, when a new round is submitted anyway, then it is allowed only if the approval was superseded by a reopening (an amendment after PP approval, AC-24); otherwise refused. | (new) |
| AC-14 | Given `BulkCuttingStarted` is recorded, then the PO becomes In Production through a PROD event handled by VNDR, and the change is written to the PO's status history. It does not change at Acknowledged. | D1, §5.9 |
| AC-15 | Given bulk cutting started, when staff set an expected completion date, then it is stored with a history (old value, new value, acting person, time, optional reason). | §5.10, §9.10.1 |
| AC-16 | Given an expected completion date later than the PO's In-force latest acceptable date, then the run shows an "at risk of late" flag, also visible in the list. | S2 terms (new) |
| AC-17 | Given a finished / ready-to-ship record, then it stores quantities per size × colour (positive whole numbers, size/colour must exist on the PO), and nothing else in the flow depends on it. | D8 |
| AC-18 | Given an In Production PO, when a DN is recorded, then every line's size × colour must exist on the PO and quantities must be positive whole numbers; the dispatch date must be between the PO's Send date and today. | FR-SC-04, D11 (new) |
| AC-19 | Given several DNs on one PO, then each is stored separately and the run shows shipped-to-date per size × colour against ordered. | FR-SC-04, §5.10 |
| AC-20 | Given a vendor DN number already recorded for the same PO, then the new DN is refused as a duplicate. | (new) |
| AC-21 | Given a DN whose cumulative quantity on any line exceeds ordered plus the over-tolerance, then the DN is **recorded** and flagged **over by N pieces** in a loud, visible flag on the DN and the run, with the note that Sprint 4's GRN decides what happens to the excess. It is never refused. | D4, S2 flag pattern |
| AC-22 | Given a DN dispatched after the In-force expected date, then it is stored as late by N days; after the latest acceptable date, as beyond acceptable; on or before, on time. The revision number used is stored with the fact. | §5.10 edge case, D12 |
| AC-23 | Given the terms were amended, when a DN is recorded, then late/short/over is judged against the revision in force **on the DN's dispatch date**, not on the entry date. | D12 |
| AC-24 | Given a DN not marked final shipment that leaves any line short of ordered, then the PO becomes Partially Delivered. Given the shipped total meets ordered minus under-tolerance on every line, then the PO becomes Delivered. Given a DN marked final shipment that leaves a line short beyond the under-tolerance, then the PO becomes Delivered and the DN and run are marked short by N pieces. Without the marker a short PO never becomes Delivered. | D3, §5.9 |
| AC-25 | Given a Delivered PO, when staff close it, then it becomes Closed (recorded with actor and time); it is never closed automatically. Given a PO that is not Delivered, then close is refused. | D2 |
| AC-26 | Given quantities already delivered, when an amendment is proposed, then no line may go below its delivered quantity; given Delivered or Closed, then amendments are refused. | S2 forward rule |
| AC-27 | Given a PP sample was approved, when an amendment is created, then it is flagged as costly (fabric and sampling are committed), and a later PP round becomes allowed again (reopens AC-13). | S2 forward rule |
| AC-28 | Given a PO in Acknowledged, In Production or Partially Delivered, when staff cancel it, then a reason is mandatory, including the new production-stage reason, and the confirmation shows a prominent warning about sunk cost and vendor impact. It is never blocked because production has started. Given Delivered or Closed, cancellation is refused. | D5, §5.9 |
| AC-29 | Given a cancelled PO, then its run is closed as cancelled and no further milestones or DNs can be recorded. | D5 (new) |
| AC-30 | Given a milestone is recorded, then a `Production.MilestoneReached` event is written to the PROD outbox in the same transaction, with run id, `PO_NO`, milestone code, claimed date, and schema version. Payloads never contain internal notes, files or reporter contact details. | §9.10.3, ADR 0004 |
| AC-31 | Given a DN is recorded, then a delivery event is written to the outbox in the same transaction with the DN number, lines, final-shipment flag and the late/short/over facts. A notification failure never rolls back the recording. | NFR-FT-08, FR-65 |
| AC-32 | Given more than one page of runs, then the list endpoint is paged with a stable order and the total, and its query count does not grow with the number of runs. | CONVENTIONS.md §4 |
| AC-33 | Given the admin app, then the production screens show the milestone timeline, PP rounds and DN history with names instead of ids, are usable from 320 px, and use one `<h1>` and semantic landmarks; the over, short, late and recorded-late flags are text as well as colour. | NFR-17, NFR-19 (new) |
| AC-34 | Given the run detail for any PO, then it never shows the PO's internal notes, cost sheets or target costs. | S2 information rule (new) |
| AC-35 | Given every approval, milestone and DN, then the acting person's name is required (free text, trimmed, 2–100 characters) alongside the system actor. | D13 |

## Non-functional constraints

- **ADR 0002:** PROD never reads VNDR tables. PROD uses `Vendor.Contracts` queries and events; VNDR learns about production only through PROD's events (VNDR inbox) and through a read-only `Production.Contracts` query for the amendment guard (plan.md).
- **ADR 0004 / NFR-FT-08:** every state change and its event share one transaction; delivery to consumers is at-least-once and consumers are idempotent (inbox).
- **NFR-SC-05:** approvals, milestone records and DNs are attributable and timestamped (system actor plus named person until Sprint 8).
- **Attachments:** reuse of Sprint 2's storage and validation without duplicating it (plan.md moves the shared parts to a building block).
- **CONVENTIONS.md:** feature folders and namespaces, one `IEntityTypeConfiguration` per entity, paged list endpoints, no N+1, NgRx feature store, containers vs components.
- **DB rules (`docs/db/naming.md`):** schema `PROD`, abbreviated uppercase names, lookup tables for milestone types, PP outcomes and delivery timing/quantity outcomes; audit columns and concurrency token; declared, indexed FKs; `numeric`/`int` for quantities; new abbreviations added to the glossary in the same change.
- **Configuration, not constants:** the "recorded late" threshold (days), attachment limits, and the at-risk rule are configuration values.
- **Testing:** requirement-tagged xUnit tests, Testcontainers for database tests, E2E for the full flow.

## Out of scope

- **GRN, counting, reconciliation and QC** (Sprint 4), including any accept/reject decision on excess or short stock. No stock or inventory changes happen in this sprint.
- **Vendor scorecard computation and display** (Sprint 4). Only the facts are recorded.
- **Vendor login or vendor self-service** (no authentication before Sprint 8).
- **Notification delivery** for milestones (SCRUM-13). Events are written; real consumers come later.
- **Factory-floor detail** (MES) and a separate **Batch** entity (D9).
- **Skipping PP sampling for repeat styles** (D10); any future exemption is gated on the Sprint 4 scorecard.
- **Per-drop delivery schedules** agreed in advance.
- **Sprint 2 backlog items** not needed by PROD: vendor-decision capture on accept/reject (AC-31 of Sprint 2) and the vendor-amendment request admin UI.
- **Payments**, including balance-on-delivery, and any refund or credit for a cancelled PO's sunk cost.
- **A "Romp fabric dispatched" milestone** for `RompSupplied` POs (see Open questions).

## Open questions

Questions 1–13 of the draft were answered by D1–D13 above. Remaining, none blocking the plan:

- [ ] **A. `RompSupplied` fabric milestone.** D10 lists `FabricBooked` only for `VendorSupplied` and no equivalent for `RompSupplied`; Sprint 2 had suggested "Romp fabric dispatched". Assumption in this spec and plan: `RompSupplied` runs simply have no fabric milestone this sprint. Confirm, or name a milestone to add (a lookup row and one flag, no schema change).
- [ ] **B. "Recorded late" threshold default.** D11 makes it configurable; suggested default **3 days**. Confirm or set.
- [ ] **C. Production-stage cancel reason wording.** Suggested lookup row: code `CancelledInProduction`, name "Cancelled after production started". Confirm wording.
- [ ] **D. Amendment reopens PP sampling (AC-13/AC-27).** Assumed: an amendment made after PP approval is flagged costly and allows a new PP round. Confirm that reopening is wanted rather than only the flag.
- [ ] **E. At-risk rule (AC-16).** Assumed: expected completion later than the In-force latest acceptable date. Confirm whether a buffer (for transit days) should be configurable from the start.
