# SCRUM-183: Production Tracking (milestones, PP-sample gate, delivery notes)

- **Status:** Draft (awaiting approval)
- **Jira:** SCRUM-183 (spec/plan gate); build tickets SCRUM-184..196 under epic SCRUM-74 "[Module] Production Tracking (PROD)". SCRUM-94 (FR-SC-04) is split: delivery notes here, GRN in Sprint 4.
- **BRD sections:** §5.9 (PO status lifecycle), §5.10 (Production Tracking), §5.11 (what follows: GRN/QC), §7.10 (FR-SC), §9.10 (PROD module, event catalogue), §12.11 (Production tables)
- **Requirement IDs:** FR-SC-04 (delivery-note half), FR-SC-01 (scorecard data only), NFR-SC-05 (attributable, timestamped actions). No `TC-*` cases exist in the BRD for this area, so every acceptance criterion below is derived from the BRD text and marked `(new)`.
- **Suggested folder:** `docs/specs/SCRUM-183-production-tracking/`

**Builds on Sprint 1/2 as delivered:**
- PO states Draft → Sent to Vendor → Acknowledged, plus Cancelled with a mandatory reason. The PO row mirrors the **In-force revision** (ADR 0007). Revisions are numbered; an Acknowledged PO changes only through an amendment.
- The PO carries **latest acceptable date**, **over/under tolerance %** and **fabric responsibility** (`VendorSupplied` / `RompSupplied`). Sprint 2 added them so Sprint 3/4 could judge "short", "late" and "on time".
- `VNDR.OUTB_MSG` events (`PoCreated`, `PoSentToVendor`, `PoAcknowledged`, `PoCancelled`, and the five revision events) carry message ID, schema version, aggregate id and `PO_NO`, plus the terms snapshot. The shared dispatcher (SCRUM-181) delivers them to per-module handlers, with a per-module inbox for idempotency.
- `Romp.Modules.Vendor.Contracts` exposes read-only queries: a PO's In-force terms, its revisions, its effective file set.
- There is no authentication until Sprint 8, so actions are attributed to the system actor today (`ICurrentActor`) and become real users then.
- Vendors do not use this system: they confirm by WhatsApp and phone. Staff record what the vendor said, as in Sprint 2's vendor-response capture.

## Problem & intent

Once a vendor acknowledges a PO, Romp has no visibility until goods arrive. Osama needs to know whether the vendor has approved fabric, whether the pre-production sample has been checked, when bulk production is expected to finish, and, when the vendor ships in several lots, what has actually been sent against the PO and whether it is late or short. The pre-production (PP) sample is the cheapest point to catch a fit or fabric mistake, so bulk production must not be recorded as started until it is approved.

This is milestone-level visibility, not a factory-floor system. The sprint also produces the facts (late, short) that the Sprint 4 vendor scorecard is computed from, and moves the PO through the rest of its BRD lifecycle.

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

## User flow

1. Staff acknowledge a PO (Sprint 1/2). The `PoAcknowledged` event reaches PROD, which **opens a production run** for the PO (no staff action). The run's milestone list depends on the PO's fabric responsibility: `VendorSupplied` gets *Fabric booked by vendor*; `RompSupplied` gets *Fabric dispatched by Romp*. Both lists then continue with *Fabric sourced/cut (optional)*, *PP sample*, *Bulk production*, *Ready for dispatch*. `(new)` for the exact wording of the two fabric milestones; the BRD only names "fabric sourced/cut".
2. As the vendor reports progress by WhatsApp/phone, staff **record each milestone** (date it happened, who reported it, optional note). Skipping the optional milestone is allowed.
3. **PP sample:** the vendor submits a sample; staff record the submission, inspect it, then **approve** or **reject** with a reason. A rejection means a new submission round. Bulk production **cannot be recorded as started** until a round is approved.
4. Staff set the **expected completion date** for bulk production and update it if the vendor says it moves. The system keeps the history and compares it with the PO's In-force expected and latest acceptable dates.
5. When the vendor dispatches goods, staff **record a delivery note (DN)**: vendor's DN number, dispatch date, and the quantities shipped per size × colour. A PO can have several DNs (partial deliveries). Each DN is checked on entry against the ordered quantities and the tolerance.
6. The PO status follows: **In Production** when bulk production starts; **Partially Delivered** after the first DN that leaves quantity outstanding; **Delivered** when the shipped total meets the ordered quantity within the under-tolerance; **Closed** when staff close it (see open questions).
7. Each DN is stamped **on time / late** and **on quantity / short / over** against the terms In force at that moment. These facts are stored for the Sprint 4 scorecard.
8. The admin sees a paged list of production runs and a run detail with the milestone timeline, PP rounds and DN history. The PO detail shows the production status.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| AC-1 | Given a PO is acknowledged, when the event is delivered, then one production run exists for that PO with the milestone list for its fabric responsibility. | §9.10.3 `PurchaseOrder.Confirmed`, §5.10 (new) |
| AC-2 | Given the same `PoAcknowledged` message is delivered twice, when both are handled, then still exactly one run exists (the inbox makes the second a no-op). | ADR 0004, SCRUM-181 (new) |
| AC-3 | Given a PO that is not acknowledged (Draft, Sent, Cancelled), then no production run is created and milestones cannot be recorded against it. | §5.9 (new) |
| AC-4 | Given fabric responsibility `VendorSupplied`, then the run has the vendor-booking fabric milestone and not Romp's dispatch milestone, and the reverse for `RompSupplied`. | S2 forward rule, §5.10 (new) |
| AC-5 | Given a run, when staff record a milestone, then it stores the actual date, who reported it (vendor contact), who recorded it, and a timestamp; a milestone date cannot be in the future. | §5.10, NFR-SC-05 (new) |
| AC-6 | Given the optional "fabric sourced/cut" milestone, when it is never recorded, then later milestones can still be recorded. | §5.10 |
| AC-7 | Given no approved PP-sample round, when anyone tries to start bulk production, then the API refuses with a message naming the missing approval. | §5.10 hard gate |
| AC-8 | Given a submitted PP sample, when staff approve it, then the approver and time are recorded and bulk production can be started. | §5.10, NFR-SC-05 |
| AC-9 | Given a submitted PP sample, when staff reject it, then a reason is mandatory, the round is closed as rejected, and a new submission round can be opened; bulk production stays blocked. | §5.10 (new) |
| AC-10 | Given an approved PP round, when a later sample is submitted anyway, then it is refused (an approval is final unless an amendment reopens it). | (new) |
| AC-11 | Given bulk production started, when staff set an expected completion date, then it is stored with a history of changes (old value, new value, who, when, reason optional). | §5.10, §9.10.1 |
| AC-12 | Given an expected completion date later than the PO's In-force latest acceptable date, then the run is flagged "at risk of late" and the flag is visible in the list. | S2 terms, §5.10 (new) |
| AC-13 | Given bulk production starts, then the PO status becomes In Production through a PROD event handled by VNDR, and the change is written to the PO's status history. | §5.9 |
| AC-14 | Given an In Production PO, when a delivery note is recorded with shipped quantities, then each line's size × colour must exist on the PO and quantities must be positive whole numbers. | FR-SC-04 (new) |
| AC-15 | Given several delivery notes on one PO, then each is stored separately and the run shows shipped-to-date per size × colour against ordered. | FR-SC-04, §5.10 |
| AC-16 | Given a DN, when the same vendor DN number is entered again for the same PO, then it is refused as a duplicate. | (new) |
| AC-17 | Given the first DN leaves quantity outstanding, then the PO becomes Partially Delivered; given the shipped total reaches ordered minus the under-tolerance, then it becomes Delivered. | §5.9 |
| AC-18 | Given a DN whose cumulative quantity for a line would exceed ordered plus the over-tolerance, then it is refused with the limit stated. | S2 tolerance (new) |
| AC-19 | Given a DN dispatched after the In-force expected date, then it is stored as late by N days; after the latest acceptable date, as beyond-acceptable. On or before, on time. | §5.10 edge case |
| AC-20 | Given a DN that leaves the PO short beyond the under-tolerance when the vendor says nothing more is coming (marked as final shipment), then it is stored as short by N pieces. | §5.10 edge case (new) |
| AC-21 | Given the terms were amended, when a DN is recorded, then late/short is judged against the revision in force **on the DN's recording date**, and the revision number is stored with the fact. | S2 forward rule |
| AC-22 | Given quantities already delivered, when an amendment is proposed, then it cannot reduce any line below the delivered quantity; given Delivered or Closed, amendments are refused. | S2 forward rule |
| AC-23 | Given a PP sample was approved, when an amendment is created, then it is flagged as costly, with a message that fabric and sampling are already committed. | S2 forward rule |
| AC-24 | Given a PO that is In Production or later, when staff try to cancel it, then the outcome follows the rule chosen in Open question 5. | §5.9 (new) |
| AC-25 | Given a milestone is recorded, then a `Production.MilestoneReached` event is written to the PROD outbox in the same transaction, carrying run id, `PO_NO`, milestone code, actual date, and schema version. Payloads contain no internal notes. | §9.10.3, ADR 0004 |
| AC-26 | Given a DN is recorded, then a delivery event is written to the outbox (same transaction) with the DN number, lines, and the late/short facts. A notification failure never rolls back the recording. | NFR-FT-08, FR-65 |
| AC-27 | Given more than one page of runs, then the list endpoint is paged with a stable order and the total, and its query count does not grow with the number of runs. | CONVENTIONS.md §4 |
| AC-28 | Given the admin app, then the production screens show milestone timeline, PP rounds and DN history with names instead of ids, are usable at 320 px and up, and use one `<h1>` and semantic landmarks. | NFR-17, NFR-19 (new) |
| AC-29 | Given the run detail, when a staff member opens it for a PO in any state, then it never shows the PO's internal notes, cost sheets or target costs. | S2 information rule (new) |

## Non-functional constraints

- **ADR 0002:** PROD never reads VNDR tables. It uses `Vendor.Contracts` queries and events; VNDR learns about production state only through PROD's events, consumed via VNDR's inbox.
- **ADR 0004 / NFR-FT-08:** every state change and its event share one transaction; delivery to consumers is at-least-once, consumers are idempotent (inbox).
- **NFR-SC-05:** approvals, milestone records and DNs are attributable and timestamped (system actor until Sprint 8).
- **CONVENTIONS.md:** feature folders and namespaces, one `IEntityTypeConfiguration` per entity, paged list endpoints, no N+1, NgRx feature store, containers vs components.
- **DB rules (`docs/db/naming.md`):** schema `PROD`, abbreviated uppercase names, lookup tables for milestone types, milestone statuses, PP-sample outcomes and delivery timing/quantity outcomes; audit columns and concurrency token; declared, indexed FKs; `numeric` for quantities.
- **Testing:** requirement-tagged xUnit tests, Testcontainers for database tests, E2E for the full flow.

## Out of scope

- **GRN, counting, reconciliation and QC** (Sprint 4). No stock or inventory changes happen in this sprint.
- **Vendor scorecard computation and display** (Sprint 4). Only the facts are recorded.
- **Vendor login or vendor self-service** (no authentication before Sprint 8; vendors work by WhatsApp/phone).
- **Notification delivery to the vendor or staff** for milestones (SCRUM-13). Events are written; a real consumer comes later.
- **Factory-floor detail** (MES), per-day production output, per-line quantities produced before dispatch (see Open question 8).
- **Photos or files attached to PP samples**, apart from an optional note (see Open question 7).
- **Per-drop delivery schedules** agreed in advance (mentioned as possible in S2 out-of-scope; not needed to record actual partial deliveries).
- **Sprint 2 backlog items** not needed by PROD: AC-31 vendor-decision capture, vendor-amendment request admin UI.
- **Payments**, including balance-on-delivery.

## Open questions

Nothing below is resolved silently; each blocks the item shown.

- [ ] **1. When does the PO become "In Production"?** BRD lists the status but not the trigger. Proposal: when bulk production is started (after PP approval), not on acknowledgement. *Blocks AC-13, ticket SCRUM-190.* Who decides: Osama.
- [ ] **2. What closes a PO ("Closed")?** Proposal: staff close it manually after Delivered (a short PO needs a "close as short" decision). Alternative: automatic once Sprint 4's GRN and QC are complete. *Blocks AC-17 and the Closed transition.*
- [ ] **3. Does "Delivered" need a vendor declaration when the delivery is short?** Proposal: yes, a DN can be marked "final shipment"; a short final shipment moves the PO to Delivered with a recorded shortfall. Without a declaration the PO stays Partially Delivered. *Blocks AC-17, AC-20.*
- [ ] **4. Over-delivery.** Proposal: beyond the over-tolerance a DN is refused, not just flagged, because Romp cannot accept it without a decision. Alternative: accept and flag. *Blocks AC-18.*
- [ ] **5. Cancelling after production has started.** S1 allows cancellation "through Acknowledged". Options: refuse once In Production; allow with a mandatory reason and a warning that fabric/cutting cost may be owed; allow only before PP approval. *Blocks AC-24.* Business decision.
- [ ] **6. Are PP-sample rounds limited?** Proposal: unlimited rounds, each recorded, no cap. Alternative: a configurable maximum after which staff must decide to cancel. *Affects AC-9.*
- [ ] **7. Should PP samples carry a photo or file?** Files raise the storage rules from Sprint 2 (type checks, size, retention). Proposal: note only this sprint. *Affects AC-8/9 scope.*
- [ ] **8. Quantity produced before dispatch.** §5.10 says "quantity actually finished may be less than ordered" at *Ready for dispatch*. Proposal: record a single "finished quantity per size × colour" on the Ready-for-dispatch milestone (optional), used only for display; delivery is judged on DNs. Alternative: skip it. *Affects milestone model.*
- [ ] **9. One run per PO, or PO-to-batch mapping?** §9.10.1 says PROD owns "PO-to-batch mapping". Proposal: one run per PO; each DN is the batch that QC will later inspect and that the GRN will reference. If Osama expects separate production batches inside one PO, the model changes. *Blocks the data model; must be answered before the plan.*
- [ ] **10. Milestone wording and set.** The two fabric milestones and the list above are proposed from the BRD. Confirm names and whether vendors ever skip PP sampling for a repeat style (BRD says it is a hard gate, so the proposal is no exceptions). *Affects lookup seed data.*
- [ ] **11. Timezone and "future date" rule.** Dates are Pakistan time for staff entry and stored UTC. Confirm that recording a milestone for a past date (back-fill) is allowed with no limit. *Affects AC-5.*
- [ ] **12. Late/short judged at recording date (AC-21).** Confirm this is right for disputes: the revision in force when the goods were dispatched vs when the DN was typed in. Proposal: use the DN's dispatch date to pick the revision (the revision that was in force on that date). *Affects AC-21.*
- [ ] **13. Authentication.** Until Sprint 8 every actor is the system actor. Confirm that "approved by Osama" is therefore recorded as the system actor for now.
