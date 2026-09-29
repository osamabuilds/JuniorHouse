# SCRUM-183: Technical plan

- **Spec:** [spec.md](spec.md) (Approved). Decisions D1–D13 referenced below.
- **Module(s):** new **Production** (PROD); changes to **Vendor** (VNDR), **Reference** (REF) and a new shared **BuildingBlocks.Storage**; web: new `production` feature, changes to `purchase-orders`.
- **ADR:** [0008](../../adr/0008-production-vendor-coordination-and-shared-storage.md) (events drive PO status, contracts for consistent guards, storage as a building block).
- **Structure:** follows [`docs/CONVENTIONS.md`](../../CONVENTIONS.md): feature folders with namespaces matching folders, one `IEntityTypeConfiguration` per entity, paged lists, no N+1, NgRx per web feature.

## Domain model (`Romp.Modules.Production.Domain`)

Feature folders `ProductionRuns/`, `Milestones/`, `Samples/`, `Deliveries/`. One run per PO (D9). The run stores no ordered quantities and no prices; ordered quantities, tolerances and dates come from `Vendor.Contracts` at the moment they are needed.

| Aggregate / entity | Purpose | Rules (invariants) |
|---|---|---|
| **`ProductionRun`** (root) | One per PO. Holds `PoId`, `PoNo`, fabric responsibility, PO send date, a copy of the In-force latest acceptable date (refreshed by events), status, expected completion date, bulk-cutting-started timestamp, Romp fabric-dispatch date, and the flags "cutting before fabric dispatch" (AC-36) and "re-sample recommended" (AC-27/38). | Created only from `PoAcknowledged`; unique per PO. Status `Open → Cancelled | Closed`. Nothing can be recorded on a Cancelled/Closed run (AC-29). `BulkCuttingStarted` requires an Approved PP round (AC-9) and is recorded once. |
| **`Milestone`** (child) | Timeline row: type, claimed date, recorded timestamp, reporter (vendor contact, free text), acting person (D13), note, `recorded late` flag, optional PP round id. | Type must be valid for the run (`FabricBooked` only when `VendorSupplied`; `FabricDispatchedByRomp` only when `RompSupplied`, AC-4/D14). `BulkCuttingStarted` on a `RompSupplied` run without an earlier fabric dispatch, or with an unresolved re-sample recommendation, is recorded and flagged loudly, never blocked (AC-36, AC-38). Claimed date between PO send date and today (D11, AC-6). `recorded late` = recorded date − claimed date > configured days (AC-7). Optional milestones may be skipped (AC-8). |
| **`PpSampleRound`** (child) | Round number, submitted date/note, decision (Pending/Approved/Rejected), decider name, reason, decided timestamp. | Round numbers are 1..n with no cap (D6). One Pending round at a time. Reject needs a reason (AC-11). Once Approved, no new round unless the approval was superseded by an amendment of either tier (AC-13/27); a new round clears the "re-sample recommended" flag. |
| **`PpSampleFile`** (child of round) | Attachment metadata: storage key (generated), original name, detected content type, size, uploader. | Same allowlist, size and count limits as Sprint 2, from configuration (AC-12). Bytes live behind `IFileStorage`. |
| **`FinishedQuantityLine`** (child of the `FinishedReadyToShip` milestone) | Size, colour, quantity. | Positive; the size × colour must exist on the PO (validated against the contract) (AC-17). Gates nothing (D8). |
| **`ExpectedCompletionChange`** (child) | Old date, new date, reason, acting person, timestamp. | Append-only history (AC-15). |
| **`DeliveryNote`** (child) | Vendor DN number, dispatch date, `final shipment` flag (D3), revision number judged against, timing outcome (OnTime / Late / BeyondAcceptable) and `days late`, quantity outcome (OnQuantity / Short / Over) with totals, the Romp fabric-dispatch date and "Romp fabric delay" indicator for `RompSupplied` runs (AC-37), acting person, timestamp. | Unique `(run, vendor DN no)` (AC-20). Dispatch date between PO send date and today. Judged against the revision in force **on the dispatch date** (D12, AC-23). Over is flagged, never refused (D4, AC-21). |
| **`DeliveryNoteLine`** | Size, colour, quantity, plus per-line over/short quantity at that time. | Size × colour on the PO; quantity positive whole number (AC-18); unique per note. |

**Delivery judgement (domain service `DeliveryJudge`, pure, unit-tested):** given the terms at the dispatch date (ordered lines, expected date, latest acceptable date, over/under tolerance %), cumulative shipped before this note, and this note:
- `maxAllowed(line) = floor(ordered × (1 + over%/100))`, `minRequired(line) = ceil(ordered × (1 − under%/100))`.
- Over: any line with cumulative > `maxAllowed` → flagged Over by the sum of excess (AC-21).
- Delivered: every line cumulative ≥ `minRequired`, **or** the note is marked final shipment (D3). A final shipment below `minRequired` is Delivered and recorded Short by the missing total (AC-24). Otherwise the PO is Partially Delivered.
- Timing: `dispatch > latestAcceptable` → BeyondAcceptable; `dispatch > expected` → Late by N days; else OnTime (AC-22).

**Vendor domain changes (`Romp.Modules.Vendor.Domain/PurchaseOrders`):** `PoStatus` gains `InProduction = 5`, `PartiallyDelivered = 6`, `Delivered = 7`, `Closed = 8`. `PurchaseOrder` gains forward-only, idempotent `StartProduction()`, `MarkPartiallyDelivered()`, `MarkDelivered()`, `Close()`, each writing the status history and raising its event; `Cancel()` is allowed from Acknowledged, InProduction, PartiallyDelivered (AC-28) and not from Delivered/Closed. `CreateRevision` refuses on Delivered/Closed (AC-26) and classifies every revision as **Construction** or **Commercial** at creation (D17): Construction when it adds/retires any file, changes fabric responsibility, or introduces a colour not on the previous revision; otherwise Commercial. The classification is persisted on the revision, exposed on `PoRevisionDto` and carried in the revision events.

## Application layer

**PROD (`Romp.Modules.Production.Application`)**

| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `OpenProductionRunCommand` (from the `PoAcknowledged` handler) | PoId | Run id (no-op if exists) | AC-1, AC-2, AC-3 |
| `RecordMilestoneCommand` | RunId, type, claimed date, reported by, acting person, note, finished lines? | Milestone | AC-4..8, AC-17, AC-30, AC-35 |
| `SubmitPpSampleCommand` | RunId, note, acting person | Round | AC-11, AC-13 |
| `DecidePpSampleCommand` | RunId, round no, approve/reject, reason, acting person | Round | AC-10, AC-11 |
| `UploadPpSampleFileCommand` / `RemovePpSampleFileCommand` / `GetPpSampleFileQuery` | RunId, round no, file | File / bytes | AC-12 |
| `SetExpectedCompletionCommand` | RunId, date, reason, acting person | Run | AC-15, AC-16 |
| `RecordDeliveryNoteCommand` | RunId, vendor DN no, dispatch date, lines, final shipment, acting person | Delivery note (with flags) | AC-18..24, AC-31, AC-35 |
| `SearchProductionRunsQuery` | status, PO no text, at-risk, page, page size | `PagedResult<RunSummaryDto>` | AC-32, AC-16 |
| `GetProductionRunQuery` | RunId | Detail: milestones, rounds (+files), history, DNs, ordered vs shipped per size × colour | AC-19, AC-34 |
| `MarkRunCancelledCommand` / `MarkRunClosedCommand` (from `PoCancelled` / `PoClosed`) | PoId | Run | AC-29 |
| `RefreshRunTermsCommand` (from `PoRevisionPutInForce`) | PoId, latest acceptable date, revision classification | Run (updates the date copy; sets "re-sample recommended" when the revision is Construction and a PP round was approved) | AC-16, AC-27 |

Validators (FluentValidation) enforce lengths, ranges and the acting-person rule (2–100 characters, AC-35). Handlers run in the module's transaction behaviour, so state, inbox row and outbox row commit together.

**VNDR additions:** `ClosePurchaseOrderCommand` (Delivered → Closed, acting person; AC-25); inbox handlers `ApplyProductionStartedHandler`, `ApplyDeliveryRecordedHandler`; `CancelPurchaseOrderCommand` accepts the new states and the production-stage reason (AC-28); `CreateAmendmentCommand` (and the vendor-request/counter paths) call `IProductionQueries` for delivered quantities and PP approval (AC-26/27).

## API endpoints

There is no authentication until Sprint 8 (SEC-08), so no role or ownership check applies yet; every endpoint is marked for the S8 sweep. The acting person is a required field on every write.

| Method | Route | Notes |
|---|---|---|
| GET | `/api/production/runs?status=&search=&atRisk=&page=&pageSize=` | paged (AC-32) |
| GET | `/api/production/runs/{id}` and `/api/production/runs/by-po/{poId}` | detail |
| POST | `/api/production/runs/{id}/milestones` | generic milestone record (not PP decisions) |
| POST | `/api/production/runs/{id}/pp-rounds` | submit a round |
| POST | `/api/production/runs/{id}/pp-rounds/{no}/decision` | approve / reject |
| GET / POST / DELETE | `/api/production/runs/{id}/pp-rounds/{no}/files[/{fileId}]` | multipart upload, download with `nosniff` + attachment headers |
| PUT | `/api/production/runs/{id}/expected-completion` | date + reason |
| POST / GET | `/api/production/runs/{id}/delivery-notes` | record / list |
| POST | `/api/purchase-orders/{id}/close` | VNDR, Delivered → Closed |
| POST | `/api/purchase-orders/{id}/cancel` | existing; now valid for InProduction / PartiallyDelivered |

Errors reuse the host's exception handlers (validation → 400 problem details, not found → 404, domain rule → 409/400 with the specific reason).

## Events

| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|
| `ProductionRunOpened` v1 | PROD | none yet | Yes |
| `Production.MilestoneReached` v1 (`ProductionMilestoneReachedEvent`) | PROD `RecordMilestone`, PP submit/decide | admin dashboard, Notifications (later) | Yes |
| `ProductionStarted` v1 | PROD when `BulkCuttingStarted` is recorded | **VNDR** (→ In Production) | Yes |
| `DeliveryNoteRecorded` v1 (lines, final flag, timing/quantity facts, PO outcome) | PROD | **VNDR** (→ Partially Delivered / Delivered), Sprint 4 scorecard | Yes |
| `ProductionRunCancelled` / `ProductionRunClosed` v1 | PROD | none yet | Yes |
| `PoProductionStarted`, `PoPartiallyDelivered`, `PoDelivered`, `PoClosed` v1 | VNDR (own transition) | PROD (`PoClosed`), Notifications later | Yes |
| `PoAcknowledged`, `PoCancelled`, `PoRevisionPutInForce` (Sprint 2) | VNDR | **PROD** | Yes (already) |

All payloads carry the standard envelope (message id, schema version, occurred-at UTC, aggregate type/id, `PO_NO`) and never contain notes, files or reporter contact details (AC-30). Registered with `AddOutboxModule("PROD", …)` and `IOutboxMessageHandler<T>` per consumed type.

**Contracts:**
- `Romp.Modules.Production.Contracts` (new): `IProductionQueries` with `GetDeliveredQuantitiesAsync(poId)` and `HasApprovedPpSampleAsync(poId)` (used by VNDR's amendment guard, AC-26/27).
- `Romp.Modules.Vendor.Contracts` (extended): `GetTermsAtAsync(poId, asOfDate)` returning the terms and lines of the revision in force on that date (from the revision status history, Pakistan-time date), plus `GetProductionContextAsync(poId)` returning PO no, send date, status and fabric responsibility (D11/D12).

## Data

**Schema `PROD`** (naming per `docs/db/naming.md`; new abbreviations added to its glossary in the same PR: round `RND`, final `FNL`, dispatch `DSPT`, recorded `RCRD`, reporter `RPTR`, cutting `CUT`, finished `FNSH`, ready `RDY`, short `SHRT`, shipment `SHIP`, decision `DCSN`, timing `TMNG`, note `NOTE` (exists)).

| Table | Key columns | Notes |
|---|---|---|
| `PROD_RUN_MAIN` | `ID`, `PO_ID`, `PO_NO`, `FBRC_RESP_ID`, `PO_SENT_DTE`, `LATE_ACPT_DT` (copy), `EXPC_CMPL_DT`, `CUT_STRT_DTE`, `FBRC_DSPT_DT`, `CUT_BEF_FBRC_IND`, `RESMPL_RECM_IND`, `PROD_RUN_STS_ID` | unique `PO_ID`; index on status and `EXPC_CMPL_DT`; `ROW_VER` concurrency token |
| `PROD_MLST` | `RUN_ID`, `MLST_TYP_ID`, `CLM_DT`, `RPTR_NAME`, `ACTR_NAME`, `NOTE`, `LATE_RCRD_IND`, `PP_RND_ID?` | FK + index per FK |
| `PROD_MLST_FNSH_LINE` | `MLST_ID`, `SIZE_ID`, `CLR_ID`, `FNSH_QTY` | unique per milestone/size/colour |
| `PROD_EXPC_HIST` | `RUN_ID`, `OLD_DT`, `NEW_DT`, `RSN`, `ACTR_NAME` | append-only |
| `PP_SMPL_RND` | `RUN_ID`, `RND_NO`, `SUBM_DT`, `NOTE`, `PP_SMPL_DCSN_ID`, `DCSN_DTE`, `DCSN_RSN`, `DCSN_NAME` | unique `(RUN_ID, RND_NO)` |
| `PP_SMPL_FILE` | `RND_ID`, `STOR_KEY`, `FILE_NAME`, `CNTT_TYP`, `FILE_SIZE_BYT`, `DELD_IND` | keys generated; never derived from the name |
| `DLVR_NOTE` | `RUN_ID`, `VNDR_DN_NO`, `DSPT_DT`, `FNL_SHIP_IND`, `REV_NO`, `DLVR_TMNG_ID`, `DAYS_LATE`, `DLVR_QTY_STS_ID`, `OVER_QTY`, `SHRT_QTY`, `FBRC_DSPT_DT`, `ROMP_FBRC_DLAY_IND` | unique `(RUN_ID, VNDR_DN_NO)` |
| `DLVR_NOTE_LINE` | `NOTE_ID`, `SIZE_ID`, `CLR_ID`, `QTY`, `OVER_QTY`, `SHRT_QTY` | unique per note/size/colour |
| Lookups (system-owned, seeded by migration): `PROD_RUN_STS_LKP` (Open, Cancelled, Closed), `MLST_TYP_LKP` (the seven milestone types: `FabricBooked`, `FabricDispatchedByRomp`, `PPSampleSubmitted`, `PPSampleApproved`, `PPSampleRejected`, `BulkCuttingStarted`, `FinishedReadyToShip`), `PP_SMPL_DCSN_LKP` (Pending, Approved, Rejected), `DLVR_TMNG_LKP` (OnTime, Late, BeyondAcceptable), `DLVR_QTY_STS_LKP` (OnQuantity, Short, Over) | standard lookup shape |
| `INBX` (inbox), `OUTB_MSG` (outbox) | per the platform convention | inbox row written in the same transaction as the handler effect |

All transactional tables have the audit columns and a concurrency token. Size and colour ids are plain columns (no cross-schema FKs). Lists are read with SQL projections; no partitioning needed at MVP volume (noted for the plan of BRD §12.6 only).

**Other schema changes:**
- **REF migration:** four `PO_STS_LKP` rows (5–8); one `PO_CNCL_RSN_LKP` row `ProductionAlreadyStarted` (id 7, "Cancelled after production started", D16); the `PoStatusLookup` seed and `PoStatus` constants change together.
- **VNDR migration:** `VNDR.INBX` (first VNDR handler with a database effect) and an additive `PO_REV.CNSTR_CHNG_IND` (construction classification; existing revisions default to commercial). No `PO_MAIN` column changes. Sprint 3 carry-over (SCRUM-186): `PoCreatedEvent` gets the PO id (assigned before the event is raised) and an outbox health read endpoint.
- **Storage move:** `IFileStorage`/`LocalFileStorage`/options/content signature to `Romp.BuildingBlocks.Storage`, no data change.
- **Configuration:** `Prod:RecordedLateDays` (default **3**, D15), `Prod:PpFiles:MaxFileSizeBytes` / `MaxFilesPerRound`, `Prod:AtRiskBufferDays` (default **7**, D18); the existing storage root is shared.
- Every migration is additive; existing Sprint 1/2 rows load unchanged. `dotnet ef migrations has-pending-model-changes` must be clean for all four contexts.

## NFR / security design

- **Idempotency (AC-2):** consumer inbox keyed by `(message id, handler)` committed with the effect; run creation also protected by the unique `PO_ID`. Delivery-note entry protected by the unique `(run, DN no)` and the run's concurrency token, so two staff recording at once cannot both apply.
- **Transactional outbox (ADR 0004, NFR-FT-08):** state change, inbox row and event commit together; nothing calls a notification service inline.
- **Attachments (D7):** allowlisted content signatures (not extensions), size and count limits from configuration, generated storage keys, sanitised original names, `Content-Disposition: attachment` and `X-Content-Type-Options: nosniff` on download (Sprint 2 rules unchanged).
- **Information hygiene (AC-34, AC-30):** run detail and event payloads are built from PROD data plus contract results that exclude internal notes, cost sheets and target costs.
- **Attribution (NFR-SC-05, D13):** every write stores the system actor and a validated acting-person name; audit columns cover the rest.
- **Logging (SEC-25):** no file bytes or personal contact details in logs; the logging behaviour already redacts.
- **SEC-08/SEC-05:** no roles or ownership until Sprint 8; endpoints listed for the sweep.
- **Performance:** run list is one COUNT plus one page query with an at-risk flag computed in SQL from PROD's own copy of the latest acceptable date (no per-row contract calls); run detail makes one contract call for the PO terms and one query for children (`AsSplitQuery` where the project references the Relational package).
- **Time:** dates are `date` in Pakistan time for staff entry, timestamps in UTC; "today" for bounds is computed in `Asia/Karachi`.

## Frontend

New web feature `features/production` (per CONVENTIONS.md): route `/production` (list) and `/production/:id` (detail), a "Production" entry in the shell navigation.
- **containers:** `production-runs-page` (filters, `<app-pagination>`, run table) and `production-run-page` (detail, dispatches actions, owns the forms).
- **components (presentational, OnPush):** `run-table`, `milestone-timeline`, `milestone-form`, `pp-round-panel` (rounds, decision, files, "round N" history), `delivery-note-form`, `delivery-note-list` (loud Over / Short / Late / Recorded-late flags as text badges, not colour only), `expected-completion-panel`.
- **store:** `production.{state,actions,reducer,selectors,effects}.ts`; list state with `total/page/pageSize` and filters; detail state with the loaded run and per-panel saving/error; the store resets on `Opened`.
- **forms:** typed forms with the required acting-person name field on every action.
- **purchase-orders changes:** status labels for 5–8; Close button for Delivered POs; the Cancel panel gets the prominent sunk-cost / vendor-impact warning for In Production / Partially Delivered POs and the production-stage reason (AC-28); a link from PO detail to its production run; the Amend panel shows the costly notice for any post-approval amendment, and for a Construction amendment a prominent "a new PP sample is strongly recommended" notice (AC-27), plus the delivered-quantity limit message. The run detail shows the loud flags for AC-36 and AC-38.
- **a11y/responsive:** one `<h1>`, landmarks, `table.responsive`, 44 px targets, labels on every control, tested at 320 px.
- **E2E (Playwright):** acknowledge → milestone → PP reject then approve with a photo → bulk cutting (PO shows In Production) → DN 1 partial → DN 2 final short (Delivered, Short flag) → Close; plus cancel in production with the warning, over-delivery flag, back-date bounds.

## Risks & alternatives considered

- **Two contracts reference each other's module** (Vendor → Production.Contracts, Production → Vendor.Contracts). No cycle at build time, but any growth here must stay read-only and small. Alternative (event-fed counters in VNDR) rejected: the amendment guard would be eventually consistent (ADR 0008).
- **PO status lag after a delivery note** (seconds, via dispatcher). Acceptable; the run detail shows the authoritative PROD outcome and the PO catches up. If the dispatcher is down, the outbox health surface (SCRUM-186) makes it visible.
- **`GetTermsAtAsync` depends on revision status history timestamps.** If Sprint 2 data lacks a put-in-force timestamp for Rev 0 (backfilled POs), Rev 0 is treated as in force from the PO's Send date; covered by a unit test on the backfilled shape.
- **Denormalised latest-acceptable-date copy in PROD** (kept fresh by `PoRevisionPutInForce`) is a read-model choice to avoid N+1 on the list; if a message is lost or late the flag is stale for that moment only, and detail re-reads the contract.
- **Delivered on a final short shipment** means Delivered can hide a large shortfall; mitigated by the Short flag on the note, run and (later) scorecard, and by the manual Close step.
- **Cancelling late is never blocked (D5):** cost implications are a UI warning only; refunds/credits are out of scope.
- **Capacity:** if the week is short, cut the finished-quantity milestone UI (D8) and the demo data before touching the PP gate, delivery notes or the PO status wiring.
- **New ADR:** [0008](../../adr/0008-production-vendor-coordination-and-shared-storage.md).
