# SCRUM-93: Procurement — Part 2 (PO amendments, spec files, outbox dispatcher)

- **Status:** Approved (2026-09-28). The 6 open questions below (tolerance defaults, dead-letter blocking trade-off, capacity cut line, BRD errata logging, Jira housekeeping, local compliance verification) are carried forward unresolved by the user's explicit instruction — none block starting plan.md.
- **Jira:** SCRUM-93 (FR-SC-03), SCRUM-181 (outbox dispatcher + inbox), SCRUM-179 (Sprint 1 carry-over bug). Spec/plan gate: SCRUM-180. Epic SCRUM-14 "[Module] Vendor & Procurement (VEND)" for SCRUM-93; SCRUM-181 sits under epic SCRUM-44 "[Module] Foundation & Platform Setup".
- **BRD sections:** §5.9 (Demand Planning & Purchase Order, edge case), §5.10–5.11 (what comes next: production, delivery, QC), §7.10 (FR-SC), §9.5/§13.8 (outbox), §12.11 (Vendor & Procurement core tables)
- **Requirement IDs:** FR-SC-03. The tech-pack clause of FR-SC-02 (deferred from Sprint 1). Three PO commercial terms have **no FR ID** in the BRD (see Decisions R10), so they are logged for the BRD errata (SCRUM-168).
- **Suggested folder:** `docs/specs/SCRUM-93-procurement-part-2/` (same convention as Sprint 1, which used the feature ticket, not the gate ticket).

**Builds on Sprint 1 (SCRUM-92) as delivered:**
- `PO_MAIN` holds one unit cost per PO (single-style PO). `PO_LINE` holds size × colour quantity. `PO_STS_HIST` is an append-only status ledger. PO numbers come from `PO_NO_SEQ` (ADR 0006).
- States are Draft → Sent to Vendor → Acknowledged, plus Cancelled from any of them with a mandatory reason. Events written to the outbox: `PoCreated`, `PoSentToVendor`, `PoAcknowledged`, `PoCancelled`.
- Each module has its own `OUTB_MSG` (`VNDR.OUTB_MSG` for VNDR); its shape is owned by SCRUM-165. Writer only, no dispatcher, no consumer.
- Cross-module access goes through `.Contracts` projects (`Romp.Modules.Catalog.Contracts` exposes `IStyleQueries`). There are no cross-schema foreign keys (naming.md rule 7).
- Architecture tests enforce the `^[A-Z0-9]{2,4}(_[A-Z0-9]{2,4})*$` naming pattern and covering indexes on every FK column.
- There is no authentication (SEC-08 lands in Sprint 8). `POST /api/purchase-orders/{id}/acknowledge` takes no body today.

## Problem & intent

Sprint 1 built the PO through Acknowledged. This sprint closes the gaps that stop the flow **PO → production → delivery** from continuing, in this order of priority:

1. **Sprint 1 carry-overs.** A known bug (SCRUM-179) and one hole: a style can be edited after POs already use it, which would silently invalidate PO lines and revisions.
2. **The outbox has writers but no reader.** Production Tracking (Sprint 3) is the first real consumer of `PoAcknowledged` and of revision events, so the dispatcher, the consumer-side inbox, and a stable **event contract** must exist before Sprint 3 starts.
3. **Acknowledged terms are locked, but vendors change them.** A vendor asks for a higher cost, a later date, more advance, or a fabric/trim substitution. Today the only options are editing history in place or cancelling and re-raising the PO. FR-SC-03 exists to close this.
4. **The PO has no tech pack**, and no place for the commercial terms that Sprint 3/4 need to judge "short", "late" and "on time" (tolerance, latest acceptable date, who supplies fabric).

Design principles for everything below:
- **An agreed commitment is never edited, only superseded by a newer, numbered, auditable revision** that the counter-party agrees to. Whoever proposes a change says why and what it costs.
- **What the vendor commits to (terms and specification files) is versioned together.** A vendor must always be able to say exactly which revision, with exactly which files, they agreed to.
- **Romp's internal information never reaches the vendor** (target cost, retail price, internal notes, cost sheets).
- **Vendors confirm on WhatsApp and phone**, not in this system. The system is the audit record of those conversations, so it captures how, who, when, and evidence.

Business context that shaped these rules: Romp holds pre-made stock and targets a fixed summer 2027 launch window. A late delivery therefore costs a season, and money paid to a vendor in advance is hard to recover. That is why date flags, advance impact and a latest acceptable date are in scope.

## Delivery order and cut line

| Priority | Scope | Why this order |
|---|---|---|
| P0 | Sprint 1 carry-overs: SCRUM-179 fix, style guard | Sprint 2 adds more lookups through the same endpoint and revisions depend on stable styles |
| P1 | Dispatcher, inbox, event contract, Vendor contracts | Blocks Sprint 3 (Production Tracking) |
| P2 | Commercial-terms additions, amendments, vendor response capture | The core of FR-SC-03 |
| P3 | Spec files with revision rule, send-time checklist, vendor-facing view | Needed before production starts, but the least coupled |

If capacity is short, move the vendor-facing view and the non-essential attachment categories to Sprint 3. **Never** move P0–P2.

## Requirements (quoted from the BRD)

> **FR-SC-03:** System shall support versioned PO amendments (cost/date changes) without overwriting the original agreed terms.

> **FR-SC-02 (tech-pack clause, deferred from Sprint 1):** System shall support creating a Purchase Order against a vendor with a size/colour quantity breakdown, unit cost, expected delivery date, and **tech-pack attachment**.

> **§5.9 edge case:** "vendor renegotiates cost or delivery date after acknowledgement → PO must support a versioned amendment (PO-v2) rather than silently overwriting agreed terms, so there is an audit trail if a dispute arises."

> **§5.9 narrative (tech-pack origin):** "A Purchase Order (PO) is created against that vendor: PO number, style reference, **tech pack attachment (measurements, fabric/trim specs, construction notes)**, quantity breakdown by size × colour, agreed unit cost, expected delivery date, payment terms (advance %, balance on delivery is typical locally)."

> **§5.11 narrative (downstream use of the tech pack, context only):** "Each inspected unit checked against the tech pack: stitching, measurements (spot-check against size chart), fabric/colour match to the approved sample, trims/buttons/zippers secure, no visible defects, correct care-label content."

> **§9.5/§13.8 (outbox):** PostgreSQL transactional outbox at MVP. Events and notifications are written in the same transaction as the business change, and a notification failure must never roll back the business transaction (NFR-FT-08). Retry with backoff and dead-letter on exhaustion.

> **SCRUM-181 (Jira description):** background worker polls `OUTB_MSG` with `FOR UPDATE SKIP LOCKED`, publishes to in-process handlers, retries with exponential backoff, dead-letters after N attempts, and provides an **inbox/idempotency key table for consumers** (NFR-FT-05).

> **§12.11 (context):** the BRD's core-table list for this module names `Vendors, PurchaseOrders, PurchaseOrderLines, POAmendments, TechPacks`. Revision records and attachments here correspond to `POAmendments` and `TechPacks`. Table and column names are decided in plan.md per `docs/db/naming.md`; this spec names none.

## Terminology

| Term | Meaning |
|---|---|
| **Revision** | A numbered, immutable snapshot of what the vendor commits to: the commercial terms, the line quantities (size × colour), and the set of specification files. |
| **Rev 0** | The revision exactly as first sent to the vendor, created at the first Send. Draft edits never create revisions. |
| **In force** | The revision whose contents are the PO's current agreed position. Exactly one revision is In force at any time after the first Send. |
| **Pending** | Proposed but not yet agreed. It has no effect until accepted. |
| **Superseded / Rejected / Withdrawn** | Superseded: was In force, replaced by a newer In-force revision. Rejected: turned down by the decider. Withdrawn: cancelled by the proposer, or automatically when the PO is cancelled. |
| **Initiator** | Who proposed the revision: **Buyer** (Romp) or **Vendor**. |
| **Decider** | For a Buyer-initiated Pending revision, the vendor decides. For a Vendor-initiated one, the buyer decides. There is no vendor portal, so staff record the vendor's answer. |
| **Spec file (vendor-visible)** | A file the vendor works from (tech pack, artwork/labels, trim card, colour standard, packing instructions). It is part of the revision. |
| **Internal file** | A file only Romp sees (cost sheet, compliance/test report, vendor evidence). It is never part of a revision and never shown to the vendor. |
| **Effective file set** | The spec files in force for a given In-force revision. |

## User flow

### A. Sprint 1 carry-overs (P0)

1. Fix `GET /api/ref/{lookup}` returning 500 when `includeInactive` is omitted (SCRUM-179), with a regression test, and fix the Sprint 1 E2E locator bugs recorded on that ticket.
2. **Style guard.** Updating a style must not remove a size or colour that is used by any line of a non-cancelled PO (an In-force revision's lines or a Pending revision's lines). The error names the PO number(s). Adding sizes/colours and editing other style fields stay allowed. CTLG learns this through a Vendor contracts query, never by reading VNDR tables (ADR 0002).
3. **Legacy acknowledge stays compatible.** `POST .../acknowledge` with an empty body still works and means "vendor confirmed the In-force revision", recorded with channel `Unspecified`. The new Record vendor response action (section D) is what the UI uses. Sprint 1's tests and E2E keep passing.

### B. PO commercial terms (P2)

Sprint 1's PO has one expected delivery date and one unit cost. Sprint 3/4 need more to judge a delivery ("short", "late", "on time"), and adding these fields later would mean re-snapshotting every revision. New PO terms, all part of every revision:

| Term | Rule |
|---|---|
| **Latest acceptable delivery date** | Must be on or after the expected delivery date. Required before a **new** PO can be sent. It is Romp's cancel-by date: the expected date is what the vendor plans for, and this is the last date Romp will accept. |
| **Over-ship tolerance %** and **Under-ship tolerance %** | Prefilled from configuration (suggested starting default 5% each way, to be confirmed with the vendor), editable per PO, within a configured maximum (suggested 20%). Used by GRN acceptance in Sprint 3/4. Shipped quantity rarely matches the PO exactly, so the allowed range belongs in the PO terms. |
| **Fabric responsibility** | Lookup: `VendorSupplied` or `RompSupplied`. Required before a new PO can be sent. It decides which production milestones exist in Sprint 3 (fabric booking by the vendor versus fabric dispatched by Romp). |
| **Payment term and advance %** | Already on the PO from Sprint 1. They become amendable. |

Existing Sprint 1 POs get **null (not specified)** for the new terms. Nothing is invented for POs that were already agreed. They can be filled through a normal amendment.

### C. PO amendments (FR-SC-03)

**What is amendable:** unit cost, expected delivery date, latest acceptable delivery date, over/under tolerance, payment term, advance %, fabric responsibility, line quantities (change, add, or remove a size × colour line, provided at least one line remains), and spec files (add or retire). **Not amendable:** vendor and style. Changing either means cancelling and raising a new PO.

**Behaviour by PO state:**

| PO state | Direct edit | Buyer amendment | Vendor-initiated amendment |
|---|---|---|---|
| **Draft** | Yes, in place, no revision consumed (Sprint 1 behaviour, now including files) | Not applicable | Not applicable |
| **Sent to Vendor** | No (locked) | New revision, **immediately In force**; the previous revision becomes Superseded. The vendor must acknowledge the *latest* revision number. PO stays Sent to Vendor. | Recorded as a **Pending** vendor-initiated revision. PO stays Sent to Vendor until the buyer accepts. |
| **Acknowledged** | No (locked) | New revision, **Pending**; the In-force revision remains the PO's position until the vendor accepts. PO stays Acknowledged. | Recorded as a **Pending** vendor-initiated revision. The buyer accepts or rejects. PO stays Acknowledged. |
| **Cancelled** | No | Rejected | Rejected |
| **In Production and later** | Not built yet (Sprint 3+). Forward-compatible rules are under Out of scope. | | |

**Rules that apply to every amendment:**

1. Every amendment creates a new numbered revision (Rev 1, Rev 2, …). An existing revision is never edited.
2. An amendment must differ from the In-force revision in at least one way: a term, a line quantity, or a spec file added/retired. A **spec-only amendment** (for example the vendor proposing a substitute trim or print, with no cost or date change) is valid. This matters for children's wear, where a supplier changing a trim, ink or print base is a real event that needs buyer review.
3. Every amendment needs a **reason** (lookup) and a mandatory **internal impact note** (staff only). It may also carry an optional **message to vendor** (shareable). Reason and message appear on the vendor-facing view, and the internal note never does.
4. The system **computes the impact itself** and stores it with the revision: PO value before/after/difference (PKR), advance amount before/after, expected-date shift in days, latest-acceptable-date shift, per-line and total quantity differences, and spec files added/retired. The note explains the impact, and the computed figures are the facts.
5. If a revision puts the expected delivery date **after the latest acceptable date**, the impact carries a prominent "beyond latest acceptable date" flag. It does not block anything, because accepting or refusing that is Romp's business decision, but it must be impossible to miss when deciding.
6. Only **one open (Pending) revision per PO** at a time. A Pending revision is accepted (becomes In force, the previous one Superseded, and the PO's current terms update in the same transaction), rejected (kept in history, no effect), or withdrawn by the proposer.
7. If the PO is cancelled while a revision is Pending, that revision is automatically Withdrawn in the same transaction.
8. Reason and impact note are mandatory on **every** amendment. The original rule ties this to production milestones, which do not exist until Sprint 3, so the safe superset applies now.

### D. Recording the vendor's response

One staff action, **Record vendor response**, replaces the bare Acknowledge button. It is available while the PO is Sent to Vendor and asks which revision number the vendor is responding to, then one outcome:

- **Confirmed as sent:** the PO becomes Acknowledged and the acknowledged revision number is stored. If the number is not the current In-force revision (a stale acknowledgement, for example the vendor confirming Rev 0 after Rev 1 was sent), it is rejected with a message naming the latest revision.
- **Countered:** staff enter what the vendor asks for (any amendable term, quantity, or a proposed spec file), with reason and impact note. A **vendor-initiated Pending revision** is created and the PO stays Sent to Vendor. The buyer then accepts (revision In force, PO becomes Acknowledged at that revision) or rejects (revision Rejected, PO stays Sent to Vendor; the vendor can confirm the In-force revision, counter again, or the PO can be cancelled).
- **Declined:** opens the existing Cancel flow with `VendorDeclined` preselected. The PO is cancelled only once staff confirm.

After acknowledgement, a vendor asking for a change (the §5.9 edge case) is recorded with **Record vendor amendment request** and becomes a vendor-initiated Pending revision. Staff use **Record decision** (Accept / Reject) for a Pending revision, or **Withdraw** it.

**Every recording of something the vendor said captures:** the **channel** (lookup: `WhatsApp`, `PhoneCall`, `Email`, `InPerson`), the **name of the person who responded** (defaulting to the vendor's contact), **when the vendor responded** (defaulting to now, editable, never in the future), and optional **evidence file(s)** such as a WhatsApp screenshot (internal category). This is the audit trail that §5.9 asks for.

### E. Labelling, history and the vendor-facing view

1. `PO_NO` never changes (ADR 0006). A separate revision number is stored per revision.
2. The label is composed **at render time**: a Draft shows `PO-2026-00001`. After the first Send it shows `PO-2026-00001 Rev 2` for the In-force revision (so `Rev 0` shows right after the first Send), plus `Rev 3 (proposed)` when a Pending revision exists.
3. The staff **revision history** lists every revision: number, initiator, status, terms, computed impact, reason, internal note, message to vendor, who/when, vendor-communication details and evidence, and a before/after comparison against the previous revision.
4. A **vendor-facing view** is a normal web page, readable on a phone (vendors read on phones) and printable on A4 (browser print-to-PDF). It shows `PO_NO Rev n`, vendor, style code and name, lines, the commercial terms, the effective vendor-visible files, and a "changes from previous revision" summary with the shareable reason and message. A Pending revision is clearly marked *proposed, not agreed*.
5. **It never shows:** the style's target unit cost or target retail price, the internal impact note, internal files, or vendor evidence. It is built from a **separate vendor-facing data shape**, so leakage is structurally impossible rather than dependent on someone remembering to hide fields.
6. Server-side PDF generation and sending the view to the vendor (WhatsApp/email) are out of scope.

### F. Spec files (tech pack) — FR-SC-02 clause

A tech pack here is a set of files, as §5.9 describes it. It is not structured data entry. Categories come from a lookup with a **vendor-visible** flag:

| Vendor-visible (spec files, part of a revision) | Internal (never shown to the vendor) |
|---|---|
| `TechPackSpec` (spec sheet, measurements, construction) | `CostSheet` |
| `ArtworkLabels` (artwork, labels, print strike-offs) | `ComplianceTestReport` |
| `TrimCardBom` | `VendorEvidence` |
| `ColourStandard` (approved colour / lab dip) | `Other` |
| `PackingInstructions` | |

**Rules:**

1. **Draft:** add and remove any file freely (removal is a soft delete, metadata kept). Files present at Send form part of Rev 0.
2. **After the first Send, spec files change only through a revision.** The files are uploaded with the amendment and become effective when that revision is In force. Retiring a spec file is also done in a revision. A file is never edited or removed in place. Otherwise the spec could change under the same revision number the vendor already acknowledged, and production could follow outdated instructions.
3. **Internal files** can be added at any time (unless the PO is Cancelled). After Send they cannot be removed.
4. **Cancelled:** read-only. Viewing and downloading still work.
5. Files of a Rejected or Withdrawn revision stay stored and visible in history but are never effective.
6. The list shows filename, category, uploaded-by, uploaded-at, the revision it was added in, and the revision it was retired in (if any). The **effective file set for any In-force revision** is retrievable. Sprint 4 QC uses this to know which spec applied.

**Send-time checklist (non-blocking).** When staff Send a PO, the confirmation shows a short reminder: tech pack attached; safety spec for the age range (neck/hood drawstrings, small parts and trims, snap/button security); care labels and batch/tracking-label content defined; approved print/colour standards attached where relevant; packing instructions attached. If **no `TechPackSpec` file** is attached, the dialog warns and requires an explicit "send anyway", and the history records that the PO was sent without a tech pack. The checklist is guidance text only, not tracked per item. It draws on US/EU children's-wear practice; Romp's own local requirements are not verified here (see Open questions).

**File constraints (decided in this spec, not stated in the BRD):**
- Allowed types: PDF, JPG, PNG, XLSX, DOCX, checked by **content signature**, not by extension alone. No archives, executables, or macro-enabled formats.
- Limits: 20 MB per file, 10 files per PO, both **configuration values**.
- Storage: S3-compatible object storage behind an abstraction per the BRD stack. Local development uses a local implementation of the same interface (plan.md decides). Storage keys are **generated**, never derived from the filename. The original filename is kept as sanitised metadata only.
- Download: `Content-Disposition: attachment` and `X-Content-Type-Options: nosniff`.

### G. Events and the contract Sprint 3 depends on (P1)

Production Tracking must react to PO events without reading VNDR tables (ADR 0002), so the events are a designed, versioned contract.

1. **Every outbox payload** carries: a stable **message ID**, event type, **schema version**, occurred-at (UTC), aggregate type and ID (the PO id) and `PO_NO`.
2. **Revision events** (revision proposed, put in force, rejected, withdrawn, superseded) carry: revision number, status, initiator, reason code, the full commercial-terms snapshot, line quantities (size, colour, quantity), the effective vendor-visible file identifiers, and the computed impact figures.
3. **Sprint 1 events** (`PoCreated`, `PoSentToVendor`, `PoAcknowledged`, `PoCancelled`) are extended **additively** (new fields only, schema version bumped) with the revision number and terms snapshot, so no Sprint 1 field changes meaning.
4. **Payloads never contain** the internal impact note, vendor evidence, or internal files. A future notification handler could forward event data to a vendor, so internal information must not be in the payload.
5. A new `Romp.Modules.Vendor.Contracts` project (like `IStyleQueries` for Catalog) exposes read-only queries for consumers: a PO's In-force terms, its revision list, its effective file set, and which sizes/colours of a style are in use by active POs. Section A2's style guard and Sprint 3's Production Tracking both use it.

### H. Outbox dispatcher and inbox (SCRUM-181, P1)

No staff-facing flow. It is a background worker, generic and module-agnostic, owned by the platform (SCRUM-44). Each module registers its own outbox with it (VNDR now, others later) and registers handlers per event type. Sprint 3's Production Tracking will be the first real handler, and Notifications (SCRUM-13) will plug its channel senders in as handlers on this same dispatcher, so the platform never has a second one.

1. **Outbox shape.** Confirm what `VNDR.OUTB_MSG` already has. If it lacks any of aggregate identity, attempt count, next-attempt time, claim owner, lease expiry, processed-at, or dead-letter state and reason, add them with an **additive migration** whose defaults let Sprint 1's existing unprocessed rows dispatch normally. The first run must **drain the Sprint 1 backlog** without error.
2. **Claim with a lease.** The dispatcher claims a batch in a *short* transaction using `FOR UPDATE SKIP LOCKED`, stamping who claimed it and when the claim expires. The handler runs **outside** that transaction, and on success the row is marked processed in a separate short transaction. If a worker dies, the lease expires and the row becomes claimable again.
3. **Inbox (consumer-side idempotency).** At-least-once delivery means duplicates. A consuming module records `(message ID, handler)` in its own **inbox** table in the **same transaction as the handler's effect**. A redelivered message finds its inbox row and does nothing. Inbox rows are purged after a configurable retention, which must exceed the maximum retry window (validated at startup).
4. **Retry with exponential backoff (plus jitter).** A failure records the attempt count and last error and schedules the next attempt. After a configurable number of attempts the message is **dead-lettered**: kept with payload, event type, aggregate identity, attempt history and last error, never deleted, never retried automatically.
5. **Ordering per aggregate.** Messages for the same PO are delivered in creation order: a later message is not dispatched while an earlier one for that PO is pending, in flight, or waiting to retry. Different POs proceed independently. A dead-lettered message stops blocking later messages for its PO (otherwise one poison message would freeze the PO's event stream), so dead-lettering is made loudly visible instead.
6. **Visibility.** Each dead-letter writes an error-level log with message ID, event type and aggregate, and the dead-letter count is exposed on the health/diagnostics surface if one exists. An admin screen for inspecting or replaying dead letters is out of scope.
7. **Placeholder handling.** In dev and prod a logging handler is registered for VNDR events so messages actually reach Processed. In tests a **recording test double** proves exactly-once invocation on the happy path, and a failing double drives retry, backoff and dead-letter.
8. Tunables are configuration, not constants: poll interval, batch size, lease duration, max attempts, base and max delay, jitter, inbox retention.

## Decisions

**Resolved by the user (2026-09-28):**

- **D1.** Amendment applies in both Sent to Vendor and Acknowledged, with different mechanics (supersede vs Pending).
- **D2.** `PO_NO` is stable. A separate revision number is stored, and `{PO_NO} Rev {n}` is composed at render time.
- **D3.** BA review defaults accepted: tolerance and latest acceptable date are added to the PO now; fabric responsibility is added now; spec-file changes after Send go through a revision; payment term and advance % are amendable.

**Made during the BA review, to be confirmed at the plan gate (SCRUM-180):**

- **R1. Immutable terms, append-only history.** Revision contents are never updated. Status changes append history rows (from, to, who, when, note), matching the `PO_STS_HIST` pattern. Whether a revision's current status is derived or denormalised is a plan.md choice.
- **R2. One open revision at a time**, with a **Withdrawn** outcome.
- **R3. A vendor counter keeps the PO in Sent to Vendor** until the buyer accepts; only then does the PO become Acknowledged.
- **R4. Acknowledgement is per revision number**, and a stale acknowledgement is rejected.
- **R5. Label rule:** no Rev on Drafts; after Send always show the In-force `Rev n`; a Pending revision shows as `(proposed)`.
- **R6. Amendable set** as listed in section C, for both initiators. Payment term and advance % are included because vendors commonly ask for more advance to book fabric, and factories often take around 30% on acceptance, so an advance change has real cash impact. Vendor and style are not amendable.
- **R7. Spec files are versioned with the terms.** After Send they change only via a revision; spec-only amendments are valid; internal files are free. This corrects an earlier draft that allowed add-only files stamped with the in-force revision, which let the specification change under an already-acknowledged revision number.
- **R8. Vendor visibility.** Separate vendor-facing data shape; category-level vendor-visible flag; shareable reason/message separate from the internal note.
- **R9. Vendor communication is captured** (channel, responder, time, evidence) because vendors confirm outside the system.
- **R10. Commercial-terms additions** (latest acceptable date, over/under tolerance, fabric responsibility) have no FR ID in the BRD. They come from standard PO practice: an ex-factory date plus a cancel date as a delivery window, stated over/under-ship tolerances, and whether the brand or the factory owns the fabric. They go to the BRD errata (SCRUM-168).
- **R11. Event contract and Vendor contracts** as in section G, so Sprint 3 consumes events, not tables.
- **R12. Dispatcher design:** lease-based claiming, inbox table for idempotency, per-aggregate ordering, non-blocking dead letters with loud visibility, generic so SCRUM-13 reuses it.
- **R13. Cost sheets are deferred** (no FR, fields or table in the BRD). The internal `CostSheet` attachment category is the cheap cover.
- **R14. File constraints** as listed in section F.
- **R15. Send-time checklist is non-blocking**, except that sending without a `TechPackSpec` file needs an explicit confirm and is recorded.
- **R16. Scorecard data intent (Sprint 4):** Rev 0's terms are preserved and every change is tagged with its initiator, so on-time can be measured against the last buyer-agreed date and a vendor-initiated slip still counts against the vendor. The formula itself is Sprint 4's decision.

## Acceptance criteria

The BRD has no `TC-SC-*` test cases, so every row is sourced from FR-SC-03, the §5.9 edge case, the decisions above, or this spec's own scoping, marked `(new)` where the BRD does not state it.

### Sprint 1 carry-overs (P0)

| ID | Given / When / Then | Source |
|---|---|---|
| AC-1 | Given `GET /api/ref/{lookup}` is called without `includeInactive`, then it returns the active rows with 200, not 500, and a regression test covers it. | SCRUM-179 |
| AC-2 | Given a style whose size or colour is used by a line of a non-cancelled PO (an In-force revision or a Pending revision), when staff update the style to remove it, then the update is rejected with an error naming the PO number(s). Adding sizes/colours and editing other fields remains allowed. | Carry-over (new) |
| AC-3 | Given the style guard, then CTLG obtains PO usage only through the Vendor contracts interface, never by reading VNDR tables. | ADR 0002 |
| AC-4 | Given `POST /api/purchase-orders/{id}/acknowledge` with an empty body on a Sent PO, then it confirms the In-force revision with channel `Unspecified`, and Sprint 1's existing tests and E2E pass unchanged. | Regression |

### PO commercial terms (P2)

| ID | Given / When / Then | Source |
|---|---|---|
| AC-5 | Given a Draft PO, when staff save latest acceptable delivery date, over/under-ship tolerance %, and fabric responsibility, then they are stored. The latest acceptable date must be on or after the expected date. Tolerances must be within 0 and the configured maximum. Tolerance defaults are prefilled from configuration and editable. | R10 (new) |
| AC-6 | Given a **new** PO, when staff try to Send it without a latest acceptable date or a fabric responsibility, then Send is rejected with field-level errors. | R10 (new) |
| AC-7 | Given existing Sprint 1 POs, when the migration runs, then the new terms are null ("not specified"), no value is invented, Rev 0 copies them as null, and they can be set later by amendment. | Migration (new) |
| AC-8 | Given a Draft PO, when staff edit any of these terms, then they save in place and consume no revision. | Sprint 1 AC-9 |

### Amendments

| ID | Given / When / Then | Source |
|---|---|---|
| AC-9 | Given a PO in **Sent to Vendor** with no open revision, when staff submit a buyer amendment (a change in terms, line quantities or spec files, with reason and internal impact note), then a new revision is created and **immediately In force**, the previous revision becomes Superseded, and the PO stays Sent to Vendor awaiting acknowledgement of the new revision number. | D1 |
| AC-10 | Given a PO in **Acknowledged** with no open revision, when staff submit a buyer amendment, then a new revision is created as **Pending**, the In-force revision remains the PO's current position, and the PO stays Acknowledged. | D1 |
| AC-11 | Given a **Pending** revision, when the decider's answer **Accept** is recorded, then the revision becomes In force, the previous In-force revision becomes Superseded, and the PO's current terms update in the same transaction. | FR-SC-03 |
| AC-12 | Given a **Pending** revision, when **Reject** is recorded, then it becomes Rejected, the PO's position is unchanged, and it stays visible in history. | FR-SC-03 |
| AC-13 | Given a **Pending** revision, when its proposer **withdraws** it (optional note), then it becomes Withdrawn and never takes effect. | R2 (new) |
| AC-14 | Given a PO with an open Pending revision, when staff try to create another amendment, then it is rejected with an error naming the open revision. | R2 (new) |
| AC-15 | Given an amendment, then any of these may change: unit cost, expected date, latest acceptable date, over/under tolerance, payment term, advance %, fabric responsibility, line quantities. Line changes are validated against the style's size run and colourways (Sprint 1 AC-8), quantities are positive whole numbers, and at least one line remains. Changing vendor or style is rejected. | R6 |
| AC-16 | Given an amendment that changes no term and no line but adds or retires at least one spec file, then it is a valid revision (spec-only). Given an amendment that differs from the In-force revision in **no** way, then it is rejected as a no-op. | R7 |
| AC-17 | Given an amendment without a reason or without an internal impact note, then it is rejected with a field-level error. A message to vendor is optional. | Amendment rule 3 |
| AC-18 | Given any revision is created, then the system computes and stores with it: PO value before/after/difference (PKR, `numeric(12,2)`), advance amount before/after, expected-date shift in days, latest-acceptable-date shift, per-line and total quantity differences, and spec files added/retired. These figures are immutable and shown in history. | Amendment rule 4 |
| AC-19 | Given a revision whose expected date is after its latest acceptable date, then the impact carries a "beyond latest acceptable date" flag, shown wherever the revision is displayed or decided. It does not block the revision. | Amendment rule 5 (new) |
| AC-20 | Given a PO in **Draft** or **Cancelled**, when staff try to amend it, then it is rejected. | Sprint 1 AC-9/AC-13 |
| AC-21 | Given a PO with a Pending revision, when the PO is cancelled with its mandatory reason, then that revision is automatically Withdrawn in the same transaction. | Amendment rule 7 (new) |
| AC-22 | Given a persisted revision, then no endpoint or handler can modify its contents or computed impact. Only status-history rows are appended, and a test asserts this. | FR-SC-03, R1 |
| AC-23 | Given any revision status change, then a history row records from-status, to-status, who, when and note. | §5.9 audit trail |
| AC-24 | Given two staff amend the same PO at the same moment, then exactly one succeeds and the other gets a conflict error. Revision numbers are unique and consecutive per PO. | Sprint 1 AC-7b pattern (new) |

### Vendor response and communication capture

| ID | Given / When / Then | Source |
|---|---|---|
| AC-25 | Given a PO in **Sent to Vendor**, when staff record **Confirmed as sent** for the current In-force revision number, then the PO becomes Acknowledged and the acknowledged revision number is stored. | Sprint 1 Acknowledge, extended |
| AC-26 | Given a response recorded for a revision number that is not the current In-force revision, then it is rejected with a message stating the latest revision number. | R4 |
| AC-27 | Given a PO in Sent to Vendor, when staff record **Countered** (terms, quantities and/or proposed spec files, with reason and internal note), then a **vendor-initiated Pending** revision is created and the PO stays Sent to Vendor. Nothing is overwritten. | D1, §5.9 |
| AC-28 | Given a vendor-initiated Pending revision on a Sent PO, when the buyer **accepts**, then it becomes In force (previous Superseded) and the PO becomes Acknowledged at that revision. When the buyer **rejects**, it becomes Rejected and the PO stays Sent to Vendor. | R3 |
| AC-29 | Given a PO in Sent to Vendor, when staff record **Declined**, then the Cancel flow opens with `VendorDeclined` preselected, and the PO is cancelled only after staff confirm. | Sprint 1 cancel |
| AC-30 | Given a PO in **Acknowledged**, when staff record a vendor amendment request, then a vendor-initiated Pending revision is created, and the buyer accepts (In force, PO stays Acknowledged) or rejects. | §5.9 edge case |
| AC-31 | Given anything recorded as said by the vendor (a response, a counter, an amendment request, or a vendor decision on a Pending revision), then **channel** (required), **responder name**, **response time** (defaults to now, never in the future) and optional **evidence file(s)** are captured and shown in history. Missing channel is a validation error. | R9 (new) |
| AC-32 | Given a Pending revision, then the UI states who must decide, and Accept / Reject / Withdraw appear only on Pending revisions. | (new) |

### Labelling, history and the vendor-facing view

| ID | Given / When / Then | Source |
|---|---|---|
| AC-33 | Given a Draft PO, then it shows `PO_NO` only. Given a sent PO, then it shows `{PO_NO} Rev {n}` for the In-force revision, plus `Rev {m} (proposed)` when a Pending revision exists. `PO_NO` is never modified, and the label is never stored. | D2, R5, ADR 0006 |
| AC-34 | Given a PO with revisions, when staff open its history, then every revision shows number, initiator, status, contents, computed impact, reason, internal note, message to vendor, communication details, evidence, who and when, with a before/after comparison against the previous revision. | FR-SC-03 |
| AC-35 | Given a sent PO, when staff open the vendor-facing view, then it shows `PO_NO Rev n`, vendor, style code and name, lines, commercial terms, effective vendor-visible files, and the "changes from previous revision" summary with shareable reason and message. A Pending revision is marked *proposed, not agreed*. It is readable on a phone and prints cleanly on A4. | R8 |
| AC-36 | Given the vendor-facing view, then it never includes the style's target unit cost or target retail price, the internal impact note, internal files, or vendor evidence. It is built from a separate vendor-facing data shape, and a test asserts those fields do not exist on it. | R8 (new) |

### Spec files

| ID | Given / When / Then | Source |
|---|---|---|
| AC-37 | Given a PO in **Draft**, when staff attach a file with a category, then it is stored and listed (filename, category, uploaded-by, uploaded-at) and downloadable. When they remove one, it disappears from the list (soft delete, metadata kept). | FR-SC-02 |
| AC-38 | Given a Draft PO is sent, then Rev 0's effective file set is the vendor-visible files present at Send. | R7 |
| AC-39 | Given Send is confirmed with no `TechPackSpec` file, then the dialog warned and required an explicit "send anyway", and the history records the PO was sent without a tech pack. The reminder checklist is shown on every Send and never blocks. | R15 (new) |
| AC-40 | Given a PO in **Sent to Vendor** or **Acknowledged**, when staff try to add or remove a vendor-visible file directly, then it is rejected with a message pointing to Amend. Added or retired through an amendment, the change becomes effective only when that revision is In force. | R7 |
| AC-41 | Given any non-Cancelled PO, when staff add an **internal** file, then it is stored and never appears on the vendor-facing view. After Send, internal files cannot be removed. | R7, R8 |
| AC-42 | Given a **Cancelled** PO, when staff try to add or remove a file, then it is rejected, and existing files stay viewable and downloadable. | R7 |
| AC-43 | Given a revision is Rejected or Withdrawn, then its files remain stored and visible in history but are not in any effective file set. | R7 |
| AC-44 | Given any In-force revision, then its effective vendor-visible file set is retrievable, and the list shows for each file the revision it was added in and (if any) retired in. | R7 (new) |
| AC-45 | Given an upload whose type is outside the allowlist (by content signature), or which exceeds the configured size or per-PO file limit, then it is rejected with a specific error and nothing is stored. | R14 (new) |
| AC-46 | Given a download, then it is served as an attachment with `nosniff`, the filename is sanitised, and storage keys are generated rather than filename-derived, so path traversal is impossible. | R14 (new) |

### Events and contracts (P1)

| ID | Given / When / Then | Source |
|---|---|---|
| AC-47 | Given a revision is created, put In force, rejected, withdrawn or superseded, then an event is written to the outbox in the same transaction as the change (ADR 0004). | §5.9, ADR 0004 |
| AC-48 | Given any outbox payload, then it carries a stable message ID, event type, schema version, occurred-at (UTC), aggregate type and ID, and `PO_NO`. Revision events carry the terms snapshot, line quantities, effective file identifiers, initiator, reason code and computed impact. | R11 |
| AC-49 | Given the Sprint 1 events, then their payloads are extended additively (revision number and terms snapshot), the schema version is bumped, and no existing field changes meaning. | R11 |
| AC-50 | Given any payload, then it never contains the internal impact note, vendor evidence, or internal files. | R11 (new) |
| AC-51 | Given a consuming module, then it can read a PO's In-force terms, revision list, effective file set, and style usage through `Romp.Modules.Vendor.Contracts` only. | ADR 0002, R11 |

### Outbox dispatcher and inbox (P1)

| ID | Given / When / Then | Source |
|---|---|---|
| AC-52 | Given the dispatcher starts against a database that already holds unprocessed Sprint 1 outbox rows, then it drains them without error. Any missing outbox columns were added by an additive migration with safe defaults. | R12 (new) |
| AC-53 | Given committed, unprocessed rows in a registered module outbox, when the dispatcher runs, then each is delivered to its handler(s) and marked Processed only after the handler succeeds. | §9.5, ADR 0004 |
| AC-54 | Given multiple dispatcher instances, then rows are claimed with `FOR UPDATE SKIP LOCKED` in a short transaction with a lease, so no row is handled by two workers at once, and the handler runs outside the claim transaction. | R12 |
| AC-55 | Given a worker dies mid-handling, when the lease expires, then the row becomes claimable again and is redelivered. | NFR-FT-05 |
| AC-56 | Given a handler with a database effect, then the effect and its inbox row (message ID, handler) are committed in one transaction, and a redelivery of the same message is a no-op. | SCRUM-181, NFR-FT-05 |
| AC-57 | Given a handler fails, then attempt count and last error are recorded and the next attempt is scheduled with exponential backoff and jitter. After the configured maximum attempts the message is dead-lettered. | §9.5 |
| AC-58 | Given a dead-lettered message, then payload, event type, aggregate identity, attempt history and last error are retained, it is never retried automatically, an error-level log is written, and the dead-letter count is visible on the health/diagnostics surface if present. | R12 |
| AC-59 | Given several messages for the same PO, then they are delivered in creation order and a later one is not dispatched while an earlier one is pending, in flight or waiting to retry. Different POs are independent. A dead-lettered message does not block later ones. | R12 |
| AC-60 | Given the dispatcher or a handler fails, then the business transaction that wrote the outbox row is unaffected, because the dispatcher only reads committed rows. | NFR-FT-08 |
| AC-61 | Given the placeholder setup, then dev/prod register a logging handler so VNDR events reach Processed, and tests use a recording double (exactly-once invocation per message) and a failing double (retry, backoff, dead-letter). | R12 |
| AC-62 | Given inbox rows older than the configured retention, then they are purged. The service refuses to start if retention is not longer than the maximum retry window. | R12 (new) |

### Sprint 1 regression and migration

| ID | Given / When / Then | Source |
|---|---|---|
| AC-63 | Given a Draft PO, when it is sent, then a **Rev 0** snapshot (terms, lines, effective files) is created in the same transaction as the status change and its outbox event. | R1, D2 |
| AC-64 | Given existing Sprint 1 POs, when the migration runs, then every PO that has ever been Sent gets a Rev 0 (In force) built from its current terms, and POs with an Acknowledged transition record acknowledged revision 0 with channel `Unspecified`. POs that never left Draft get none. | Migration (new) |
| AC-65 | Given any PO, then its current-terms fields always equal the In-force revision's terms, so Sprint 1 endpoints and queries keep their shape and Sprint 1's existing tests pass unchanged. | Regression |
| AC-66 | Given a PO in Sent to Vendor or Acknowledged, when staff try to edit terms directly, then it is still rejected (Sprint 1 AC-13) with a message pointing to Amend. Cancel remains available from Draft, Sent and Acknowledged. | Sprint 1 AC-12/AC-13 |

## Non-functional constraints

- **Money and percentages:** unit cost, PO value, advance amount and differences are `numeric(12,2)`. Advance % and tolerance % are `numeric(5,2)`. Never `double`. PO value is the sum of line quantity × unit cost (one unit cost per PO, as Sprint 1 built it).
- **Immutability and audit:** revision contents and computed impact never change after creation. History is append-only. The PO keeps its concurrency token (SCRUM-162 pattern), and revision numbers are allocated inside the transaction.
- **Idempotency and ordering:** at-least-once delivery, an inbox per consuming module keyed on message ID, and per-aggregate ordering (section H).
- **Failure isolation (NFR-FT-08):** the dispatcher never blocks or rolls back a business transaction.
- **Configuration, not constants:** file limits, tolerance default and maximum, and dispatcher/inbox tunables.
- **New REF lookups** (standard shape: `ID` smallint, `CODE` unique, `NAME`, `DSCR`, `SORT_SEQ`, `ACT_IND`; seeded by migration; retired via `ACT_IND`; never hardcoded enums). Names and placement follow `docs/db/naming.md`, and plan.md adds glossary entries for the new words.
  - Revision status: `Pending`, `InForce`, `Superseded`, `Rejected`, `Withdrawn`.
  - Amendment initiator: `Buyer`, `Vendor`.
  - Amendment reason (suggested seeds): `VendorCostIncrease`, `MoqConstraint`, `FabricOrTrimUnavailable`, `CapacityDelay`, `AdvanceRequest`, `SizeMixChange`, `ColourChange`, `SpecChange`, `SafetyOrCompliance`, `BuyerDemandChange`, `Other`.
  - Vendor communication channel: `WhatsApp`, `PhoneCall`, `Email`, `InPerson`, plus `Unspecified` (system-only, for the legacy acknowledge path and backfill).
  - Fabric responsibility: `VendorSupplied`, `RompSupplied`.
  - Attachment category with a vendor-visible flag (seeds in section F).
- **Naming and schema tests:** all new tables and columns pass the naming architecture test, and every new FK column is indexed.
- **Module boundaries (ADR 0002):** VNDR reads REF and Catalog only through contracts. The dispatcher is a platform component. Consumers use `Vendor.Contracts` and events, never VNDR tables.
- **Descriptive style data is read live** (style code and name on the vendor-facing view come from the current style). What the vendor commits to is the revision plus its files, so those are the frozen parts.
- **Attachment security:** content-type verification, generated storage keys, forced-download headers. Malware scanning is not built this sprint. Files are commercially sensitive.
- **No auth yet:** unchanged (no login, no SEC-08 role check). Attachment, download and vendor-view endpoints must not be publicly reachable until Sprint 8 closes this.

## Definition of Done

- Backend unit and integration tests (Testcontainers) green on CI.
- Frontend build and tests green on CI. In Sprint 1 the frontend and E2E never ran on the development machine, so **CI on Linux is the proof**, not a local run.
- The Playwright E2E is extended with the Sprint 2 path: amend an Acknowledged PO, vendor accepts, revision goes In force, and events are dispatched.
- README gains a "Try Sprint 2" section with a demo script.
- `docs/db/naming.md` glossary updated, and this spec's status set to **Implemented** at the end, noting any place the implementation differed from plan.md.
- Jira housekeeping done (see Open questions).

## Out of scope

- **Cost sheets** as a module. The BRD has no FR ID, field definition or table for them (§12.11 lists none). Costing files can be attached now under the internal `CostSheet` category.
- **Style-level default tech pack.** Files live on the PO for now.
- **QC itself** (Sprint 4, §5.11). This sprint only makes the spec files exist, versioned and retrievable per revision.
- **A real outbox consumer.** Production Tracking (Sprint 3) is the first. Notifications (SCRUM-13) will reuse this dispatcher.
- **PO states beyond Acknowledged** (In Production, Partially Delivered, Delivered, Closed). Forward-compatible rules for the Sprint 3/4 specs to carry forward:
  - Partially Delivered: only undelivered lines are amendable; delivered quantities are immutable. Delivered/Closed: no amendments.
  - GRN acceptance and "short"/"late" judgements use the In-force tolerance and dates at delivery time.
  - Fabric responsibility decides the production milestone list (vendor fabric booking versus Romp-supplied fabric dispatch).
  - After PP-sample approval, fabric booking or cutting, amendments should be flagged as costly and may need a stricter approval step. Reason and impact note are already mandatory on every amendment.
  - Batch/lot traceability (which PO and production run a garment came from) is anchored on `PO_NO`. Tracking-label content lives in the spec files. GRN, QC, carton labels and payments reference `PO_NO` only, optionally noting the revision in force.
  - The vendor scorecard is computed in Sprint 4 from Rev 0 terms, per-revision date shifts and initiators (R16).
- **Multi-style POs and grouping several POs per vendor per season**, **per-drop delivery schedules** (Sprint 3 may need them, and the revision snapshot should stay extensible), **size-band or per-line pricing** (one unit cost per PO stays), **vendor MOQ data**, and **penalty/liquidated-damages clauses**.
- **Server-side PDF and sending the PO to the vendor** (WhatsApp/email). Needs a notification consumer.
- **Dead-letter admin screen and replay**, and **malware scanning of uploads**.
- **Authentication / role checks (SEC-08)**, deferred to Sprint 8.
- **Legal compliance validation.** The send-time checklist is design guidance only.

## Open questions

- [x] ~~Amendment in Sent to Vendor or only Acknowledged?~~ Resolved: both (D1).
- [x] ~~Is "PO-v2" visible or internal?~~ Resolved: a separate stored revision number, label composed at render time (D2).
- [x] ~~Tolerance, latest acceptable date, fabric responsibility, spec files via revision, payment terms amendable?~~ Resolved: all yes (D3).
- [ ] **Tolerance defaults.** Suggested 5% each way with a 20% maximum. Confirm with the vendor(s) before the plan gate.
- [ ] **Dead-letter blocking trade-off (R12).** Dead letters stop blocking later messages for the same PO. The alternative is to keep blocking until resolved manually, which is safer for ordering but freezes the PO's event stream. Confirm the non-blocking choice.
- [ ] **Capacity cut line.** If Sprint 2 is too full, move the vendor-facing view and non-essential attachment categories to Sprint 3 (see Delivery order). P0–P2 stay.
- [ ] **BRD errata (SCRUM-168).** Log together: the three PO commercial terms (R10), cost sheets (R13), and the missing FR for the style master (Sprint 1 open question).
- [ ] **Local children's-wear requirements.** The checklist draws on US/EU practice (drawstrings, small parts, snap security, tracking labels). Romp's own requirements for selling in Pakistan have not been verified. Owner: Osama, before launch, not a Sprint 2 blocker.
- [ ] **Jira housekeeping.** SCRUM-13's description also lists a polling publisher, backoff and DLQ, so update it to say it consumes the SCRUM-181 dispatcher. SCRUM-92's scope comment says Cancel from Draft/Sent, while the Sprint 1 spec allows Draft/Sent/Acknowledged, so bring Jira in line.

## References (BA review, 2026-09-28)

US/EU sources for children's-wear practice, so read them as good practice, not as Pakistani law.

- What is a purchase order (apparel operator's guide): https://www.uphance.com/blog/what-is-a-purchase-order/
- Apparel PO template (revisions, ex-factory and cancel dates): https://www.skema3d.com/resources/templates/purchase-order
- Garment PO sheet (tolerances, confirmation before cutting): https://www.goldnfiber.com/2025/01/how-to-make-purchase-order-po-sheet-for-garments-industry.html and https://www.textileindustry.net/po-purchase-order-sheet-of-garments/
- Structuring an apparel PO (version references): https://www.fabrikn.com/blog/how-to-structure-a-purchase-order-for-apparel-production/
- PO revisions keep the PO number: https://purchaseorders.io/blog/how-to-amend-a-purchase-order
- Kidswear tech pack safety layer: https://www.adstronaut.net/use-cases/tech-pack-for-kidswear
- Baby clothing compliance checklist (strike-offs, print/trim changes): https://hapagarments.com/baby-clothing-manufacturer-compliance-checklist-us-eu/

## Changes from the previous Sprint 2 draft (v2)

| Area | Change |
|---|---|
| Sprint 1 fit | Added carry-overs (SCRUM-179, style guard, legacy acknowledge), outbox-shape check and backlog drain, inbox table, Definition of Done on CI. |
| Spec files | Now versioned with the terms: after Send they change only via a revision, spec-only amendments are valid, internal files are free. Fixes the v2 rule that let the spec change under an acknowledged revision number. |
| Vendor visibility | Separate vendor-facing data shape, vendor-visible category flag, shareable reason/message versus internal note. Added a leak test. |
| Vendor communication | Channel, responder, time and evidence captured on every recorded vendor statement. |
| Commercial terms | Added latest acceptable date, over/under tolerance and fabric responsibility to the PO; payment term and advance % became amendable; impact now includes advance amount and a "beyond latest acceptable date" flag. |
| Events | Versioned event contract, additive extension of Sprint 1 events, `Vendor.Contracts`, no internal data in payloads. |
| Categories and reasons | Attachment categories expanded and split by visibility; amendment reasons tuned to local causes (MOQ, fabric/trim unavailable, advance request, safety). |
| Send checklist | Non-blocking reminder, with an explicit confirm and a history record when no tech pack is attached. |
| Forward compatibility | Explicit hand-offs for Sprint 3/4: fabric responsibility, tolerance and dates for GRN, scorecard data, traceability. |
