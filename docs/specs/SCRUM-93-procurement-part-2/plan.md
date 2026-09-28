# SCRUM-93: Technical plan

- **Spec:** [spec.md](spec.md)
- **Module(s):** `VNDR` (Vendor & Procurement — the bulk of this sprint), `CTLG` (Catalog — style guard only), `REF` (new lookups), plus a new platform-owned dispatcher building block under `Romp.BuildingBlocks` (no new schema of its own — see Data).
- **New ADR:** [0007 — Versioned amendment pattern for business documents](../../adr/0007-versioned-amendment-pattern.md)
- **Refines, does not supersede:** [ADR 0004 — PostgreSQL transactional outbox](../../adr/0004-postgres-transactional-outbox.md). 0004 already decided `FOR UPDATE SKIP LOCKED`, backoff, dead-letter, and consumer idempotency at a high level; this plan is the concrete shape (lease claiming, per-aggregate ordering, inbox table schema) of that same decision, not a new one.
- **Glossary additions needed in `docs/db/naming.md`** (proposed abbreviations, to confirm no collision with an existing word at implementation time): `revision→REV`, `initiator→INITR`, `pending→PEND` (state value, not a column name), `impact→IMPCT`, `tolerance→TOL`, `acceptable→ACPT`, `fabric→FBRC` (already exists), `responsibility→RESP`, `channel→CHNL`, `responder→RESPR`, `evidence→EVDN`, `inbox→INBX`, `lease→LEAS`, `expiry→EXPY`, `claim→CLM`, `dead(-letter)→DEDL`, `attempt→ATMP`, `aggregate→AGGR`, `schema(-version)→SCHM`.

## Domain model

**`VNDR.PurchaseOrder` aggregate, extended.** Sprint 1's `PO_MAIN`/`PO_LINE`/`PO_STS_HIST` stay exactly as built (no breaking change — spec AC-65). New pieces:

- **`PurchaseOrderRevision` (new child entity, root of its own mini state machine per ADR 0007):** an immutable snapshot of every amendable field (unit cost, expected delivery date, latest acceptable date, over/under tolerance, payment term, advance %, fabric responsibility) plus its own line quantities and effective file set. Never updated after creation — only its *status* changes, via an append-only history ledger (`PO_REV_STS_HIST`), same pattern as `PO_STS_HIST`.
  - States: `Pending → InForce | Rejected | Withdrawn`; `InForce → Superseded`.
  - Invariant (ADR 0007): at most one `Pending` revision per PO at any time — enforced in the command handler (a partial unique index on `(PO_ID) WHERE STS = Pending` backs it at the DB level too, belt-and-braces).
  - Invariant: revision numbers are scoped to the PO, allocated as `MAX(existing)+1` inside the transaction that also bumps `PO_MAIN`'s concurrency token — a concurrent amendment attempt fails on the `xmin` check (AC-24), not on a duplicate revision number.
  - Invariant: a revision must differ from the current in-force revision in at least one versioned field or its file set (AC-16) — a true no-op amendment is rejected before a revision row is even created.
  - Carries computed, immutable-at-creation impact figures (PO value before/after/diff, advance amount before/after, date shifts, quantity diffs, files added/retired, the "beyond latest acceptable date" flag) — computed once by the handler from the previous in-force revision and the new snapshot, never recalculated later even if lookups change.
- **`PO_MAIN`'s existing "current terms" columns become a denormalised mirror of the in-force revision** (ADR 0007) — updated only by the same transaction that moves a revision to `InForce`. Four new nullable columns added directly to `PO_MAIN` (mirrored on the revision too, since they're versioned): latest acceptable delivery date, over-ship tolerance %, under-ship tolerance %, fabric responsibility.
- **`PO_LINE` stays the "current effective lines" mirror** (same denormalisation reasoning) of the in-force revision's line snapshot, which itself lives on a new `PO_REV_LINE` table (one row per size × colour per revision — this *is* versioned, unlike Sprint 1 where it was mutable-in-place during Draft).
- **`PurchaseOrderFile` (new entity):** a file attached to a PO, categorised as vendor-visible (spec file — part of a revision) or internal (never versioned, free to add anytime pre-Cancel, only removable pre-Send). A vendor-visible file's `ADDED_IN_REV`/`RETIRED_IN_REV` pair of nullable revision references defines its effective window — "effective for revision N" means added at or before N and not yet retired at or before N. No separate join table needed; this pair *is* the versioning.
- **`VendorCommunication` (new entity):** one row per recorded vendor interaction (confirmed / countered / declined / amendment request / decision on a Pending revision) — channel, responder name, response time, optional evidence file references, linked to the PO and (where applicable) the revision it concerns. This is the audit trail §5.9 asks for; it is deliberately *not* folded into `PO_REV_STS_HIST`, because a communication can exist without a status change (e.g. "Declined" leads into the existing Cancel flow, which already has its own history).

**`CTLG.Style` aggregate — one new invariant, no new entity:** updating a style's colourways/size run now checks (via a new `VNDR.Contracts` query, not a direct read) whether any non-cancelled PO's in-force-or-pending revision lines use a size/colour being removed; if so, the update is rejected. This is the only cross-module *new* dependency this sprint (`CTLG` learning about `VNDR` usage), and it flows through a contract query exactly like the existing `VNDR`→`CTLG` (`IStyleQueries`) and `VNDR`→`REF` dependencies — never a direct table read (ADR 0002).

**Platform: outbox dispatcher (new, module-agnostic, lives in `Romp.BuildingBlocks`, not owned by `VNDR`):**
- `IOutboxDispatcher` / `IOutboxMessageHandler<TEvent>` — a module registers its own outbox table (already has one per SCRUM-165: `<SCHEMA>.OUTB_MSG`) and its handlers with the dispatcher at startup.
- The dispatcher is a single generic hosted service, not a per-module one — it iterates every registered module's outbox table each poll cycle (per ADR 0007's "no new ADR needed to change how a module owns its schema" reasoning extended: the dispatcher reads `OUTB_MSG` rows by schema-qualified table name per registration, still never joining across schemas at the SQL level — one query per module's outbox, not a cross-schema union).
- Consumer-side **inbox** is owned by whichever module registers a handler with a database effect (none yet this sprint — VNDR's own placeholder logging handler has no DB effect, so it needs no inbox row; Sprint 3's Production Tracking will add its own `PROD.INBX` when it registers a real handler). This plan defines the inbox table *shape* as a convention (see Data), not a shared table.

## Application layer

**Sprint 1 carry-overs (P0):**

| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `LookupEndpoints.MapLookupType` fix (SCRUM-179, already merged) | — | — | AC-1 |
| `UpdateStyleCommand`, extended | (unchanged) + validated against `IPurchaseOrderUsageQueries.GetActiveSizeColourUsage(styleId)` | rejects with PO numbers if a size/colour in use is removed | AC-2, AC-3 |

**PO commercial terms (P2) — folded into existing Sprint 1 commands, no new ones:**

| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `UpdatePurchaseOrderCommand`, extended | + latest acceptable date, over/under tolerance %, fabric responsibility id (Draft only, unchanged mechanism) | — | AC-5, AC-8 |
| `SendPurchaseOrderCommand`, extended validation | (unchanged input) | rejects if latest acceptable date or fabric responsibility is unset | AC-6, AC-63 |

**Amendments (P2 core):**

| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `CreateAmendmentCommand` | po id, changed terms (any subset), line changes (add/remove/change qty), spec-file changes (add/retire ids), reason id, internal impact note, optional message to vendor | `RevisionNo` | AC-9, AC-10, AC-15, AC-16, AC-17, AC-18, AC-19 |
| `DecideRevisionCommand` | po id, revision no, decision (Accept \| Reject), note | — | AC-11, AC-12 |
| `WithdrawRevisionCommand` | po id, revision no, note | — | AC-13 |
| `GetPurchaseOrderRevisionsQuery` | po id | `PoRevisionDto[]` (full history, before/after diffs) | AC-5 (history), AC-34 |

`CreateAmendmentCommand`'s handler is the one place that branches on the PO's current status: `SentToVendor` → new revision immediately `InForce`, old one `Superseded`, initiator `Buyer` (AC-9); `Acknowledged` → new revision `Pending`, initiator `Buyer` (AC-10); anything else → rejected (AC-20). It rejects a no-op amendment (AC-16) and enforces "at most one Pending" (AC-14) before creating the row.

**Vendor response & communication capture:**

| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `RecordVendorResponseCommand` | po id, revision no being responded to, outcome (ConfirmedAsSent \| Countered \| Declined), counter details if Countered, channel, responder name, response time, evidence file ids | — | AC-25, AC-26, AC-27, AC-29, AC-31 |
| `RecordVendorAmendmentRequestCommand` | po id, changed terms/lines/files, reason, internal note, channel, responder, response time, evidence | `RevisionNo` | AC-30, AC-31 |
| `AcknowledgePurchaseOrderCommand` (Sprint 1, kept) | po id, no body | — | AC-4 (now internally calls `RecordVendorResponseCommand` with outcome `ConfirmedAsSent`, revision = current in-force, channel `Unspecified`) |

`RecordVendorResponseCommand`'s `Countered` outcome creates a vendor-initiated Pending revision (reuses the same revision-creation logic as `CreateAmendmentCommand`, parameterised by initiator) rather than duplicating it (AC-27). `Declined` doesn't create a revision at all — it returns a signal the API layer uses to pre-fill the existing `CancelPurchaseOrderCommand` flow (AC-29); it is not itself a state change.

**Spec files:**

| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `UploadPoFileCommand` | po id, category, file stream, filename, content type | `FileId` | AC-37, AC-41, AC-45 |
| `RemovePoFileCommand` | po id, file id | — | AC-37, AC-40, AC-41, AC-42 |
| `DownloadPoFileQuery` | po id, file id | stream + metadata | AC-46 |
| `GetPoFileListQuery` | po id | `PoFileDto[]` (incl. added-in/retired-in revision) | AC-44 |

A vendor-visible category after the first Send is rejected by `UploadPoFileCommand`/`RemovePoFileCommand` directly (AC-40) — the only path to change one post-Send is through `CreateAmendmentCommand`'s file-changes parameter.

**Vendor-facing view:**

| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `GetVendorFacingPoViewQuery` | po id | `VendorPoViewDto` — a hand-built, separate shape (never `PoDto` with fields hidden) | AC-35, AC-36 |

All commands go through the existing MediatR validation/logging/transaction pipeline (SCRUM-171), unchanged.

## API endpoints

Same "no auth yet" caveat as Sprint 1 — flagged again, more sharply, under NFR/security design below, since this sprint adds a vendor-facing view and file downloads that are more sensitive than plain CRUD screens.

| Method | Route | Auth / role | Ownership check (SEC-05) |
|---|---|---|---|
| `PUT` | `/api/purchase-orders/{id}` (extended body) | None (S2) | N/A |
| `POST` | `/api/purchase-orders/{id}/amendments` | None (S2) | N/A |
| `POST` | `/api/purchase-orders/{id}/amendments/{revNo}/accept` | None (S2) | N/A |
| `POST` | `/api/purchase-orders/{id}/amendments/{revNo}/reject` | None (S2) | N/A |
| `POST` | `/api/purchase-orders/{id}/amendments/{revNo}/withdraw` | None (S2) | N/A |
| `GET` | `/api/purchase-orders/{id}/revisions` | None (S2) | N/A |
| `POST` | `/api/purchase-orders/{id}/vendor-response` | None (S2) | N/A |
| `POST` | `/api/purchase-orders/{id}/vendor-amendment-request` | None (S2) | N/A |
| `POST` | `/api/purchase-orders/{id}/acknowledge` (Sprint 1, kept as a legacy alias) | None (S2) | N/A |
| `GET` | `/api/purchase-orders/{id}/vendor-view` | None (S2) — **flagged as a real gap, see NFR** | N/A |
| `POST` | `/api/purchase-orders/{id}/files` (multipart) | None (S2) | N/A |
| `DELETE` | `/api/purchase-orders/{id}/files/{fileId}` | None (S2) | N/A |
| `GET` | `/api/purchase-orders/{id}/files/{fileId}` (download) | None (S2) — **flagged, see NFR** | N/A |
| `GET` | `/api/purchase-orders/{id}/files` | None (S2) | N/A |
| `PUT` | `/api/catalog/styles/{id}` (unchanged route, extended validation) | None (S2) | N/A |
| `GET`/`POST`/`PUT`/`.../retire` | `/api/ref/{lookup}` for `amendment-reasons`, `vendor-comm-channels` (staff-maintained) | None (S2) | N/A |
| `GET` | `/api/ref/{lookup}` for `revision-statuses`, `amendment-initiators`, `fabric-responsibilities`, `po-file-categories`, `vendor-comm-types` (system-owned, read-only — same treatment as Sprint 1's `po-statuses`) | None (S2) | N/A |

## Events

| Event | Published by | Consumed by | Schema version | Via outbox? |
|---|---|---|---|---|
| `PoRevisionProposed` | `CreateAmendmentCommand`, `RecordVendorAmendmentRequestCommand`, `RecordVendorResponseCommand` (Countered) | none yet (Sprint 3 Production Tracking will be first) | v1 | Yes |
| `PoRevisionPutInForce` | `CreateAmendmentCommand` (SentToVendor case), `DecideRevisionCommand` (Accept) | none yet | v1 | Yes |
| `PoRevisionRejected` | `DecideRevisionCommand` (Reject) | none yet | v1 | Yes |
| `PoRevisionWithdrawn` | `WithdrawRevisionCommand`, `CancelPurchaseOrderCommand` (auto-withdraw, AC-21) | none yet | v1 | Yes |
| `PoRevisionSuperseded` | whichever command puts a new revision `InForce` | none yet | v1 | Yes |
| `PoCreated`, `PoSentToVendor`, `PoAcknowledged`, `PoCancelled` (Sprint 1) | unchanged handlers | none yet | **v1 → v2**, additive (+ revision no, + terms snapshot) | Yes |

Every payload (Sprint 1's and this sprint's new ones) is extended/defined to carry the common envelope from spec section G: stable message id (`OUTB_MSG.ID`), event type, schema version, occurred-at (UTC), aggregate type (`PurchaseOrder`), aggregate id, `PO_NO`. Revision events additionally carry the full terms/lines/effective-vendor-visible-file-id snapshot, initiator, reason code, and computed impact figures — and never the internal impact note, vendor evidence, or internal files (AC-50), enforced by a dedicated event-payload builder that only ever reads from the vendor-visible projection, never the full entity.

## Data

All new/changed columns follow `docs/db/naming.md`; every new transactional table gets `INSR_DTE/BY`, `UPDT_DTE/BY`, `xmin`, except append-only ledgers (no `UPDT_*`/`xmin`, per the existing `PO_STS_HIST` exemption).

**`VNDR` schema — new/changed:**
- `PO_MAIN`: **+4 nullable columns** — `LATE_ACPT_DT date null`, `OVER_TOL_PCT numeric(5,2) null`, `UNDR_TOL_PCT numeric(5,2) null`, `FBRC_RESP_ID smallint null`. Existing `UNIT_COST_AMT`/`EXPC_DLVR_DT`/`PAYM_TERM_ID`/`ADV_PCT` are unchanged columns, now documented as the in-force-revision mirror (ADR 0007).
- `PO_REV` (new): `ID bigint` PK, `PO_ID bigint` FK→`PO_MAIN` (indexed), `REV_NO smallint`, unique on `(PO_ID, REV_NO)`, `INITR_ID smallint` (lookup), `STS_ID smallint` (lookup, denormalised current status), `REASN_ID smallint` (lookup), `IMPC_NOTE varchar(2000)`, `VNDR_MSG varchar(2000) null`, terms snapshot (`UNIT_COST_AMT numeric(12,2)`, `EXPC_DLVR_DT date`, `LATE_ACPT_DT date null`, `OVER_TOL_PCT numeric(5,2) null`, `UNDR_TOL_PCT numeric(5,2) null`, `PAYM_TERM_ID smallint`, `ADV_PCT numeric(5,2)`, `FBRC_RESP_ID smallint null`), computed impact (`PO_VAL_BEF_AMT numeric(12,2)`, `PO_VAL_AFT_AMT numeric(12,2)`, `PO_VAL_DIFF_AMT numeric(12,2)`, `ADV_AMT_BEF numeric(12,2)`, `ADV_AMT_AFT numeric(12,2)`, `EXPC_DT_SHFT_DAY integer`, `LATE_ACPT_DT_SHFT_DAY integer null`, `QTY_DIFF integer`, `BYND_LATE_IND boolean`), partial unique index `(PO_ID) WHERE STS_ID = Pending`, audit + `xmin`.
- `PO_REV_STS_HIST` (new, append-only): `ID bigint` PK, `PO_REV_ID bigint` FK→`PO_REV`, `FROM_STS_ID smallint null`, `TO_STS_ID smallint`, `NOTE varchar(2000) null`, `INSR_DTE`, `INSR_BY` only.
- `PO_REV_LINE` (new): `ID bigint` PK, `PO_REV_ID bigint` FK→`PO_REV`, `SIZE_ID smallint`, `CLR_ID smallint`, `QTY integer`, unique on `(PO_REV_ID, SIZE_ID, CLR_ID)`, audit + `xmin`. `PO_LINE` (Sprint 1) stays as the in-force mirror, unchanged shape.
- `PO_FILE` (new): `ID bigint` PK, `PO_ID bigint` FK→`PO_MAIN`, `CATG_ID smallint` (lookup, carries the vendor-visible flag), `FILE_NAME varchar(255)`, `STOR_KEY varchar(500)` (generated, never filename-derived), `CNTT_TYP varchar(100)`, `FILE_SIZE_BYT bigint`, `ADDD_REV_ID bigint null` FK→`PO_REV` (null = internal file, or a Draft-stage vendor-visible file not yet sent), `RETD_REV_ID bigint null` FK→`PO_REV`, `DELD_IND boolean default false` (Draft-stage soft delete), audit + `xmin`.
- `PO_VNDR_COMM` (new): `ID bigint` PK, `PO_ID bigint` FK→`PO_MAIN`, `PO_REV_ID bigint null` FK→`PO_REV` (which revision this concerns, where applicable), `COMM_TYP_ID smallint` (lookup: Confirmed/Countered/Declined/AmendmentRequest/Decision), `CHNL_ID smallint` (lookup), `RSPR_NAME varchar(200)`, `RSPN_DTE timestamptz`, audit + `xmin`. Evidence files reference this via `PO_FILE.VNDR_COMM_ID bigint null` (an additional nullable FK on `PO_FILE`, category always `VendorEvidence` when set).
- `OUTB_MSG`: **+9 columns**, additive migration with safe defaults so Sprint 1's existing unprocessed rows dispatch normally — `AGGR_TYP varchar(50) not null default 'PurchaseOrder'`, `AGGR_ID bigint null` (backfilled from `PYLD` where possible, nullable for anything that can't be), `MSG_VER smallint not null default 1`, `ATMP_CNT smallint not null default 0`, `NXT_ATMP_DTE timestamptz null`, `CLM_BY varchar(100) null`, `LEAS_EXPY_DTE timestamptz null`, `PROC_DTE timestamptz null`, `DEDL_IND boolean not null default false`, `DEDL_RSN varchar(500) null`.

**`REF` schema — new lookups (standard shape unless noted):**
- `PO_REV_STS_LKP` (system-owned, no admin CRUD — same treatment as Sprint 1's `PO_STS_LKP`): `Pending`, `InForce`, `Superseded`, `Rejected`, `Withdrawn`.
- `AMND_INIT_LKP` (system-owned): `Buyer`, `Vendor`.
- `AMND_RSN_LKP` (staff CRUD): seeds per spec section on Non-functional constraints (`VendorCostIncrease`, `MoqConstraint`, `FabricOrTrimUnavailable`, `CapacityDelay`, `AdvanceRequest`, `SizeMixChange`, `ColourChange`, `SpecChange`, `SafetyOrCompliance`, `BuyerDemandChange`, `Other`).
- `VNDR_COMM_CHNL_LKP` (staff CRUD): `WhatsApp`, `PhoneCall`, `Email`, `InPerson`, `Unspecified`.
- `FBRC_RESP_LKP` (system-owned — drives real branching logic in Sprint 3's milestone list, same treatment as `PO_STS_LKP`): `VendorSupplied`, `RompSupplied`.
- `PO_FILE_CATG_LKP` (system-owned — the vendor-visible flag is structural, not admin-editable): standard shape + `VNDR_VSBL_IND boolean` — seeds `TechPackSpec`(true), `ArtworkLabels`(true), `TrimCardBom`(true), `ColourStandard`(true), `PackingInstructions`(true), `CostSheet`(false), `ComplianceTestReport`(false), `VendorEvidence`(false), `Other`(false).
- `PO_VNDR_COMM_TYP_LKP` (system-owned): `Confirmed`, `Countered`, `Declined`, `AmendmentRequest`, `Decision`.

**Platform (no new schema — a convention, per module):** the **inbox** table shape any module adds when it registers its first real handler: `<SCHEMA>.INBX` (`MSG_ID bigint`, `HNDL_NAME varchar(200)`, `PROC_DTE timestamptz`, composite PK `(MSG_ID, HNDL_NAME)`). Not created this sprint (no real consumer yet) — documented here so Sprint 3 doesn't have to redesign it.

Migrations: additive only for `OUTB_MSG`/`PO_MAIN` (existing Sprint 1 data must load cleanly — AC-52, AC-64), plus a data migration that back-fills a synthetic `Rev 0` (and, where applicable, an acknowledged revision) for every existing Sprint 1 PO that has ever been Sent, per AC-64.

## NFR / security design

- **Money/percentages:** all new PKR amounts `numeric(12,2)`; all new percentages `numeric(5,2)`; never `double` — extends Sprint 1's rule to the new impact-figure columns.
- **Immutability (ADR 0007, AC-22):** no `UpdateXCommand` or endpoint exists for `PO_REV`'s snapshot/impact columns after creation — a test in `Romp.ArchitectureTests` (or a targeted unit test) asserts no such write path exists, the same spirit as the existing naming-convention architecture test.
- **Idempotency/ordering (SCRUM-181, ADR 0004 refinement):** lease-based claim (`CLM_BY`/`LEAS_EXPY_DTE`) with a short claiming transaction, handler execution outside that transaction, per-aggregate (`AGGR_ID`) ordering enforced by the dispatcher's query (next batch excludes any aggregate with an unprocessed, non-dead-lettered row already claimed or scheduled earlier) — this is what AC-59 tests.
- **Failure isolation (NFR-FT-08):** unchanged from Sprint 1 — the dispatcher only ever reads already-committed `OUTB_MSG` rows; nothing it does can roll back the business transaction that wrote them.
- **Vendor-visibility leak prevention (AC-36):** `GetVendorFacingPoViewQuery`'s DTO (`VendorPoViewDto`) is a hand-authored type with no field for target cost, retail price, internal note, internal files, or vendor evidence — not `PoDto` with fields nulled out. A test instantiates the type via reflection and asserts the forbidden field names don't exist on it, so a future careless addition to `PoDto` can't silently leak through a shared base type.
- **File upload security (AC-45, AC-46):** content-signature type check (not extension-only) against an allowlist (PDF/JPG/PNG/XLSX/DOCX), configured max size (default 20 MB) and per-PO file count (default 10), generated (GUID-based) storage keys never derived from the client-supplied filename, `Content-Disposition: attachment` + `X-Content-Type-Options: nosniff` on download. Storage is an `IFileStorage` abstraction (local-disk implementation for dev/Docker Compose, S3-compatible for later) so the module code never talks to a concrete storage SDK directly.
- **No-auth gap, sharpened for this sprint:** Sprint 1's "no auth yet, closes in S8" note still applies, but this sprint's vendor-facing view and file download endpoints are a materially bigger exposure than plain CRUD screens (they're closer to "share this with someone outside the company" in intent, even though nothing outside the company can reach `localhost`/the Docker network yet). Flagged explicitly here — same deliberate, tracked gap, not a new one, but worth the S8 spec seeing it called out by name rather than only inheriting the generic Sprint 1 note.
- **DB naming / architecture tests:** every new table/column above passes the existing naming architecture test (SCRUM-170); every new FK column is indexed.
- **Module boundaries (ADR 0002):** the style guard's `CTLG`→`VNDR` query and the dispatcher's per-module outbox iteration are the only two new cross-module touchpoints this sprint, and both go through a `.Contracts` interface / schema-qualified table read respectively — never a raw cross-schema join.

## Frontend

Admin app, `Purchase Orders` section, extended:
- **PO detail view** gains: a revision-history panel (table: revision, initiator, status, terms diff, impact figures, reason, note, message to vendor, communication details, evidence links), an **Amend** action (opens the amendment form: any subset of terms/lines/files, reason, mandatory internal note, optional message to vendor), and **Record vendor response** replacing the bare Acknowledge button (radio choice: Confirmed as sent / Countered / Declined, with the Countered path opening the same amendment form pre-flagged as vendor-initiated).
- **Pending-revision actions** (Accept / Reject / Withdraw) appear only when a Pending revision exists, with the decider named per spec section D.
- **New route:** `/purchase-orders/{id}/vendor-view` — a separate, minimal, phone-readable/print-friendly layout (no admin chrome, no nav) built from `VendorPoViewDto` only.
- **File management panel** on the PO detail view: grouped by vendor-visible vs. internal, upload with category selector, download, remove (Draft-only for vendor-visible; internal removable pre-Send only) — greyed out / explained via tooltip where a direct action is blocked and points to Amend instead (AC-40's UI-level mirror).
- **Send confirmation dialog** gains the non-blocking checklist (spec section F) with an explicit "send anyway" when no `TechPackSpec` file is attached.
- **Reference Data screen** gains the two new staff-maintained lookup types (`amendment-reasons`, `vendor-comm-channels`) in its existing type selector; the five system-owned ones are not exposed there at all (same treatment as Sprint 1's `po-statuses`).
- **Styles screen:** no new UI, but the existing size/colour removal action now surfaces the style-guard's rejection message (listing blocking PO numbers) via the existing `ProblemDetails` field-error rendering — no new component needed.

All screens: same baseline as Sprint 1 (semantic layout, `<label>` on every control, WCAG 2.1 AA, `noindex, nofollow`, no SSR/SEO requirements — admin only).

## Risks & alternatives considered

- **New ADR 0007 was necessary, not optional** — the revision/amendment pattern is explicitly meant to generalise (spec R16, and this plan's own Data section documents the inbox convention for the same reason) and touches how a whole class of future features will version state; leaving it undocumented would mean Sprint 3/4 re-deriving these same trade-offs from scratch.
- **Considered making the dispatcher per-module instead of a single shared hosted service.** Rejected: SCRUM-181's own description and the spec's Notifications-reuse note (SCRUM-13 will plug into the same dispatcher) both point at one generic platform component; N per-module background services would each need their own poll-interval/lease/backoff configuration surface for no benefit, and would make "is Sprint 3's Production Tracking handler registered with the same guarantees as VNDR's" a question instead of a given.
- **Considered a single shared cross-module inbox table** (e.g. a `PLTF.INBX` schema) instead of one per consuming module. Rejected as inconsistent with ADR 0002 — a module's idempotency bookkeeping for its own handler's side effects belongs in that module's own schema, same reasoning as the outbox itself being per-module rather than shared.
- **Considered storing revision status only as derived** (computed from the latest `PO_REV_STS_HIST` row, no `STS_ID` column on `PO_REV` itself) instead of denormalised. Chose denormalised (ADR 0007 leaves this open, resolved here): every query that needs "is there a Pending revision on this PO" (the one-Pending-at-a-time check, run on every amendment attempt) would otherwise need a correlated subquery per PO instead of an indexed column equality check — worth the minor duplication given how often that check runs.
- **Considered giving `PO_FILE.CATG_ID`'s vendor-visible flag as a staff-editable lookup field** instead of a fixed system-owned seed. Rejected: the categories in section F are a closed, deliberately curated set (getting the vendor-visible/internal split wrong is a real data-leak risk, per AC-36's own leak test) — this is exactly the kind of lookup where "staff can edit it" is a liability, not flexibility, so it follows the same system-owned treatment as `PO_STS_LKP`/`PO_REV_STS_LKP`.
- **The six items still open in spec.md** (tolerance defaults, dead-letter blocking trade-off, capacity cut line, BRD errata logging, Jira housekeeping, local compliance verification) don't block this plan — none of them change a schema, a command shape, or an endpoint, only a config default or a documentation follow-up. They're carried forward into `tasks.md` as explicit non-blocking notes rather than re-litigated here.
