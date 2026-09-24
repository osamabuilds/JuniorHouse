# SCRUM-92: Procurement — Part 1 (reference data, style master, vendor, PO to Acknowledged)

- **Status:** Approved
- **Jira:** SCRUM-91, SCRUM-92, SCRUM-173, SCRUM-172 (spec/plan gate: SCRUM-169, priority Highest, blocks all other Sprint 1 tickets). Epic SCRUM-14 "[Module] Vendor & Procurement (VEND)" for SCRUM-91/92; SCRUM-173 sits under epic SCRUM-5 "[Module] Catalog & Discovery"; SCRUM-172 (reference data) has no epic.
- **BRD sections:** §5.9 (Demand Planning & Purchase Order), §7.10 (FR-SC), §12.4 (Lookup Table Catalogue)
- **Requirement IDs:** FR-SC-01, FR-SC-02. (FR-SC-03 exists in the BRD but is explicitly out of scope here — see below.)

> **Note on folder name:** SCRUM-169's own description names this folder `docs/specs/SCRUM-92-procurement-part-1/`, not `SCRUM-169-...`, even though SCRUM-169 is the ticket that gates the spec. Followed as written; flag if that was a typo in Jira.

## Problem & intent

Before Romp can sell anything, staff (Osama) need to record what is being produced and who is producing it. This is the first step of the supply chain: define a style to produce, record (or reuse) the vendor making it, and raise a Purchase Order against that vendor with a size × colour breakdown. Everything downstream — production tracking, QC, barcoding, warehouse putaway, and eventually the storefront listing — hangs off the style, vendor and PO records created here. This spec covers the *first three* PO states only (Draft → Sent to Vendor → Acknowledged, or Cancelled); production milestones, deliveries, and QC start in later sprints.

This spec also covers the reference/lookup data (sizes, colours, fabrics, categories, cities, payment terms, vendor specialisations, PO statuses) that styles, vendors and POs are all built on, since none of the three can exist without it.

## Requirements (quoted from the BRD)

> **FR-SC-01:** System shall support creating a Vendor record with contact, specialisation, payment terms, and a running on-time/on-quantity/defect-rate scorecard.

> **FR-SC-02:** System shall support creating a Purchase Order against a vendor with a size/colour quantity breakdown, unit cost, expected delivery date, and tech-pack attachment.

> **§5.9 narrative (Demand Planning & Purchase Order):**
> "Osama decides a style/collection to produce: fabric, colourways, size run (e.g. 2-3Y, 4-5Y, 6-7Y...), target unit cost, target retail price, target quantity per size/colour.
> A Vendor record exists (or is created) — name, contact, city, specialisation (knits/wovens/uniforms), payment terms, historical on-time-delivery %, historical defect rate.
> A Purchase Order (PO) is created against that vendor: PO number, style reference, tech pack attachment (measurements, fabric/trim specs, construction notes), quantity breakdown by size × colour, agreed unit cost, expected delivery date, payment terms (advance %, balance on delivery is typical locally).
> PO status lifecycle: Draft → Sent to Vendor → Acknowledged → In Production → Partially Delivered → Delivered → Closed. Each transition is a domain event (Section 5).
> Edge case: vendor renegotiates cost or delivery date after acknowledgement → PO must support a versioned amendment (PO-v2) rather than silently overwriting agreed terms, so there is an audit trail if a dispute arises."

> **§12.4 (Lookup Table Catalogue), general principle:** "Every status/type/reason/channel value in the schema above is backed by one of these tables — never a hardcoded string or in-code enum."
> The BRD's own catalogue table (§12.4) is storefront-oriented and does not enumerate vendor/PO lookups by name; the specific lookups needed here (sizes, colours, fabrics, genders/segments, age brackets, categories, cities, payment terms, vendor specialisations, PO statuses) are scoped in Jira SCRUM-172, applying the same "no hardcoded enum" principle from §12.4/§12.6 of `CLAUDE.md`.

**No formal FR ID exists in the BRD for "define a style/collection to produce."** §5.9 describes it narratively as the first step of the flow, but the BRD's only style/product-shaped FR is FR-53 ("Admin users shall create/edit products and variants..."), which is the *storefront-facing* product record built in Sprint 7 (Publish), not the pre-production style master needed here. See **Open questions**.

## User flow

From BRD §5.9, split into the three record types this spec covers (style, vendor, PO), in the order a staff user works through them:

1. Staff opens **Reference Data** in the admin app and confirms the lookups needed below exist (sizes, colours, fabrics, categories, cities, payment terms, vendor specialisations) — seeded by migration, editable if a new value is needed. *(new — BRD doesn't describe a reference-data screen explicitly, but implies lookups must be maintainable from the admin panel per §12.4.)*
2. Staff creates a **Style**: code, name, collection, category, gender/segment, age bracket, fabric, one or more colourways, a size run, target unit cost, target retail price, and target quantity per size × colour. *(new — BRD §5.9 lists the fields but not the record shape; formalised here as "Style" per Jira SCRUM-173 since the BRD gives it no name.)*
3. Staff finds an existing **Vendor** or creates one: name, contact, city, one or more specialisations, a default payment term, active flag. (FR-SC-01) *(specialisation is multi-select — see Decisions.)*
4. Staff raises a **Purchase Order** against that vendor and style: PO number (system-generated, `PO-{YYYY}-{NNNNN}`), size × colour quantity lines validated against the style's size run and colourways, agreed unit cost, expected delivery date, a payment term (defaulted from the vendor's default term, changeable per PO) and its advance % (defaulted from the term, overridable per PO). PO is created in **Draft**. (FR-SC-02) *(see Decisions.)*
5. Staff edits the PO while it is in Draft (quantities, cost, date, payment term, advance % — anything), then transitions it to **Sent to Vendor**. Each transition writes a domain event to the outbox in the same transaction as the status change (ADR 0004). (§5.9)
6. When the vendor confirms, staff transitions the PO to **Acknowledged**. (§5.9)
7. Staff can **Cancel** a PO from **Draft**, **Sent to Vendor**, or **Acknowledged**, giving a mandatory cancellation reason. Cancelling never changes agreed terms (that stays FR-SC-03's job, out of scope) — it only ends the PO. *(see Decisions.)*

## Decisions (resolved 2026-09-25)

Four of the five open questions from the first draft were resolved by the user with "follow the conventional approach." Researched against how mainstream procurement/ERP systems (SAP Ariba, Oracle Procurement Cloud, NetSuite) model vendor master and PO data, and applied here:

- **PO number: `PO-{YYYY}-{NNNNN}`** — e.g. `PO-2026-00001`. Sequential per calendar year, zero-padded to 5 digits, generated server-side when a PO first enters Draft (never user-entered). This is the standard shape used by ERP PO numbering (year + zero-padded sequence): unique, sortable, and immediately tells a human when a PO was raised without needing to open it. The year-scoped sequence must be generated atomically (e.g. a `REF.PO_NO_SEQ` table keyed by year, row-locked on allocation, or a Postgres `SEQUENCE` per year) so two concurrent Draft creations can never collide — this becomes an explicit requirement in plan.md. Per `docs/db/naming.md`, `PO_NO` is its own unique-constrained column, separate from the surrogate `ID` PK (CLAUDE.md's rule for real-world identifiers).
- **Vendor specialisation: many-to-many.** Conventional vendor-master modelling (NetSuite's multi-select vendor category, SAP's vendor material groups) treats a vendor's capability/category as many-to-many, because real vendors commonly supply more than one (a vendor doing knits often also does uniforms). `VNDR_SPEC_MAP` becomes a join table (`VNDR_ID`, `VSPC_ID`) rather than a single FK column on the vendor.
- **Payment terms: lookup-defaulted, PO-overridable.** Standard ERP PO behaviour (SAP/Oracle/NetSuite all work this way): the vendor master carries a *default* payment term; a PO defaults to that term (and the term's standard advance %) at creation, but a buyer can select a different term or type a different advance % on that specific PO, since real negotiations vary order to order even with the same vendor. So: `REF.PYMT_TERM` gets a `DFLT_ADV_PCT` column (the term's standard advance %, e.g. "50/50" → 50), `VNDR.PYMT_TERM_ID` is the vendor's default, and `VNDR_PO.PYMT_TERM_ID` + `VNDR_PO.ADV_PCT` are copied from the vendor's default at creation but independently editable while the PO is in Draft.
- **Cancel is allowed through Acknowledged, with a mandatory reason.** Conventional PO lifecycles (again SAP/Oracle/NetSuite, and general purchasing practice) allow cancellation up until an irreversible physical or financial event occurs — for a garment PO, that's the vendor starting production or the first delivery (GRN), neither of which exist until Sprint 3+. Since this spec never reaches In Production, Cancel is available from any of the three states it covers (Draft, Sent to Vendor, Acknowledged), but requires a reason from a new lookup, `REF.PO_CANCEL_REASON` (seed rows: `VendorDeclined`, `CostDispute`, `QualityConcern`, `StyleDiscontinued`, `DuplicateEntry`, `Other`). This is deliberately distinct from FR-SC-03 amendment: cancelling ends the PO, it never changes its agreed terms.

## Acceptance criteria

The BRD has no `TC-*` test cases for supply chain (BRD §6 only defines `TC-CAT/PDP/CHK/ACC/RET/LOY/NOTIF` for the storefront), so every row below is sourced from FR-SC-01/02, the §5.9 narrative, or the Sprint 1 Jira scoping comments, and marked `(new)` accordingly.

| ID | Given / When / Then | Source |
|---|---|---|
| AC-1 | Given the REF schema is migrated, when the API starts, then sizes, colours, fabrics, genders/segments, age brackets, categories, cities, payment terms, vendor specialisations and PO statuses are seeded and readable via a lookup API. | SCRUM-172 (new) |
| AC-2 | Given a lookup value is retired (`ACT_IND = false`), when it is queried for new selection, then it no longer appears as selectable, but existing records that reference it are unaffected. | `docs/db/naming.md` lookup rule (new) |
| AC-3 | Given valid style fields (code, name, category, gender, age bracket, fabric, ≥1 colourway, ≥1 size in the size run, target unit cost, target retail price, target quantity per size×colour), when staff submits the style, then it is created and listed/searchable. | §5.9, SCRUM-173 (new) |
| AC-4 | Given a style code that already exists, when staff submits a duplicate, then creation is rejected with a clear error. | (new — uniqueness not stated explicitly in BRD but implied by "code") |
| AC-5 | Given valid vendor fields (name, contact, city, one or more specialisations, a default payment term), when staff submits the vendor, then it is created with an active flag defaulted to true and scorecard fields present but zero/blank. | FR-SC-01, SCRUM-91 comment |
| AC-5a | Given a vendor form, when staff selects more than one specialisation (e.g. knits and uniforms), then all selected specialisations are saved against the vendor. | Decisions (new) |
| AC-6 | Given an existing vendor, when staff edits its contact, city, specialisations, default payment term, or active flag, then the change is saved and audit columns (`INSR_DTE/BY`, `UPDT_DTE/BY`) are updated. | FR-SC-01 (new) |
| AC-7 | Given an active vendor and an existing style, when staff creates a PO, then the PO is created in **Draft** with a system-generated PO number in the form `PO-{YYYY}-{NNNNN}`, unique and sequential within that calendar year. | FR-SC-02, §5.9, Decisions |
| AC-7a | Given a PO being created for a vendor with a default payment term, when the PO form loads, then the payment term and advance % are pre-filled from the vendor's default, and staff can change either before saving. | Decisions (new) |
| AC-7b | Given two POs created concurrently in the same calendar year, when both are saved, then both get distinct, gapless-or-not-but-never-colliding PO numbers (no duplicate `PO_NO` under concurrent creation). | Decisions (new) |
| AC-8 | Given a PO quantity line for a size or colour not in the style's size run / colourways, when staff submits it, then the PO is rejected with a validation error. | (new — not explicit in BRD, but the style's size run/colourways are the only stated source of truth for what's orderable) |
| AC-9 | Given a PO in **Draft**, when staff edits its lines, cost, date, payment term, or advance %, then the changes are saved in place (no versioning yet — see Out of scope). | §5.9, SCRUM-92 comment |
| AC-10 | Given a PO in **Draft**, when staff transitions it to **Sent to Vendor**, then the status changes, the transition is recorded, and a domain event is written to the outbox table in the same transaction. | §5.9 ("each transition is a domain event"), ADR 0004 |
| AC-11 | Given a PO in **Sent to Vendor**, when staff transitions it to **Acknowledged**, then the status changes and a domain event is written to the outbox. | §5.9 |
| AC-12 | Given a PO in **Draft**, **Sent to Vendor**, or **Acknowledged**, when staff cancels it with a reason selected from `PO_CANCEL_REASON`, then the status becomes **Cancelled**, the reason is stored, and a domain event is written to the outbox. | Decisions (new) |
| AC-12a | Given the cancel action, when staff attempts it without selecting a reason, then the cancellation is rejected with a validation error. | Decisions (new) |
| AC-13 | Given a PO in **Acknowledged**, when staff attempts to edit its lines/cost/date/payment term (anything other than cancelling), then the action is rejected — only versioned amendment (FR-SC-03, out of scope here) can change agreed terms past this point. | §5.9 edge case, FR-SC-03 boundary |
| AC-14 | Given any PO status transition, when it is recorded, then it is visible in the PO's detail view as a timeline (status, timestamp, who, and reason where applicable). | §5.9 ("audit trail if a dispute arises") |
| AC-15 | Given a submitted style, vendor, or PO with an invalid/missing required field, when staff submits the form, then the specific field-level error is shown (via the MediatR validation pipeline, SCRUM-171) and nothing is persisted. | SCRUM-171 (new) |

## Non-functional constraints

- **Money:** unit cost, target retail price and any PKR amount are `numeric(12,2)`, never `double` (CLAUDE.md, mandatory).
- **DB naming:** every table/column/constraint in `REF`, `CTLG`, `VNDR` schemas follows `docs/db/naming.md` (UPPERCASE, 2–4 char abbreviated words), enforced by the architecture test in SCRUM-170.
- **Audit + concurrency:** every transactional table (styles, vendors, POs, PO lines) carries `INSR_DTE/BY`, `UPDT_DTE/BY` and an `xmin`-based concurrency token (SCRUM-162).
- **Module boundaries:** `REF` lookups are read by `CTLG` (styles) and `VNDR` (vendors, POs) only through each module's contracts project, never by direct cross-schema query (ADR 0002).
- **Outbox, writer only:** PO status transitions write to `VNDR.OUTB_MSG` in the same transaction as the change (ADR 0004). The background dispatcher (retry/backoff/dead-letter) is explicitly **out of scope** for Sprint 1 per SCRUM-165's comment — there is no consumer yet, so nothing currently reads the outbox rows this sprint.
- **No auth yet:** per CLAUDE.md's scope decision, there is no login and no SEC-08 role check in Sprint 1. Admin endpoints are open locally. This is a known, deliberate gap to close before S8, not an oversight — noted here so it isn't lost.
- **Lookup shape:** every REF table follows the standard shape (`ID` smallint PK, `CODE` unique, `NAME`, `DSCR`, `SORT_SEQ`, `ACT_IND`), retired via `ACT_IND`, never hard-deleted.
- **PO number generation is concurrency-safe:** `PO_NO` allocation (see Decisions) must not race under concurrent Draft creation — no two POs can ever receive the same number, enforced by both a unique constraint on `PO_NO` and an atomic allocation mechanism (row-locked per-year counter or a DB sequence), not by an application-level read-then-increment.

## Out of scope

- **FR-SC-03** (versioned PO amendments after Acknowledged) — moved to Sprint 2 per SCRUM-92's comment.
- **Tech-pack attachment** on the PO — moved to Sprint 2 per SCRUM-92's comment (FR-SC-02 mentions it, but Osama's Sprint 1 scoping comment explicitly defers it).
- **Vendor scorecard calculation** (on-time %, on-quantity %, defect rate) — fields exist on the vendor record from Sprint 1, but the calculations themselves depend on deliveries and QC, which don't exist until Sprint 4 (SCRUM-91 comment).
- **PO states beyond Acknowledged** (In Production, Partially Delivered, Delivered, Closed) — Sprint 3 onward (§5.10–5.11).
- **Cost sheets** — Sprint 2 (per the sprint plan, FR-SC-03 grouping).
- Authentication / role checks (SEC-08) — deferred to Sprint 8 per CLAUDE.md.

## Open questions

- [ ] **No FR ID for "style master."** The BRD names FR-53 for the storefront product record (Sprint 7) but never gives the pre-production style/collection concept from §5.9 step 1 its own FR-SC number. Should we ask for a BRD erratum (alongside the known SCRUM-168 errata), or is it fine to build "Style" as an unlabelled but Jira-tracked (SCRUM-173) concept? Blocks: whether to cite an FR ID for this part of the spec at all.

The other four open questions from the first draft (PO number format, vendor specialisation cardinality, payment terms override, cancel-from-Acknowledged) are resolved — see **Decisions** above.
