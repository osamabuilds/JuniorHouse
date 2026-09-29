# SCRUM-197: Technical plan

- **Spec:** [spec.md](spec.md) (Draft; open questions Q1–Q10 unresolved, so anything that depends on them is marked "per Q<n> default, awaiting approval").
- **Module(s):** new **Quality** (schema `QC`, projects `Romp.Modules.Quality.*`); changes to **Vendor** (VNDR: receipt progress consumer, scorecard); web: new `quality` feature, changes to `vendors`.
- **ADR:** none is required so far. Cross-module traffic reuses ADR 0004 (outbox), ADR 0006 (business numbers) and ADR 0008 (events drive PO state, contracts for consistent guards). If Q7 is answered in a way that lets a GRN change PO status, write ADR 0009 first.
- **Structure:** follows [`docs/CONVENTIONS.md`](../../CONVENTIONS.md): feature folders with namespaces matching folders, one `IEntityTypeConfiguration` per entity, paged lists, no N+1, NgRx per web feature. The Production module (Sprint 3) is the pattern to copy for projects, module class, outbox/inbox wiring, endpoints and tests.

## Shared contracts (fixed here so parallel agents cannot collide)

Every group builds against this section. **Nothing in it may be renamed by a single group.** An agent that needs a change reports it to the integrator instead of editing another group's area.

### Collision owners

| Area | Owner (only this group may edit) |
|---|---|
| `ModuleNames` in `tests/Romp.ArchitectureTests`, host registration (`Romp.Api` module list, DI, endpoint mapping hook), `docs/db/naming.md` glossary, **all QC migrations and the `QcDbContext` model snapshot**, `Directory.Packages.props` | **B** |
| `Romp.Modules.Vendor.*` (any VNDR change: consumer, tables, migrations, endpoints, `PoStatus` use) | **H** |
| `Romp.Modules.Vendor.Contracts` scorecard additions (`IVendorScorecardQueries`, DTOs) and the scorecard tables/read model | **G** |
| `Quality.Contracts` (interfaces and event records below) | **B creates them exactly as written here**; later changes go through the integrator |
| `docker-compose*`, `appsettings*` `Qc:*` keys, demo seeder | **J** |
| README, traceability, spec status | **K** |

Because B owns every migration, **B creates the persistent entity shells and configurations for every QC table listed below** (properties with private setters, constructor/factory only, no behaviour). C, D, E and F add behaviour, commands, queries and endpoints on those entities **without schema changes**. A group that needs a column or table reports it to the integrator; B (or a follow-up B task) adds it.

### Module, schema, projects

- Module name `Quality`; schema `QC`; short code `QC` (BRD §9.10). Add `"Quality"` to `ModuleNames`.
- Projects: `Romp.Modules.Quality.Domain`, `.Application`, `.Infrastructure`, `.Contracts`, tests `Romp.Modules.Quality.Tests`. `QualityModule : IModule`. DbContext `QcDbContext` (default schema `QC`).
- Feature folders (identical in Domain/Application/Infrastructure/Api; namespaces match): `InboundDeliveries/` (B), `Receiving/` (C), `Batches/` and `Inspections/` (D), `Outcomes/` (E), `SafetyChecks/` (F). Inspection sampling lives in `Inspections/Aql/` (D).
- Outbox registered with `AddOutboxModule("QC", …)`; one `IOutboxMessageHandler<T>` per consumed type; `INBX` per platform convention.

### Events QC publishes (all in `Romp.Modules.Quality.Contracts`, all schema v1, via the QC outbox)

Envelope `QualityEvent(long PoId, string PoNo) : IOutboxEvent` (same shape as `ProductionEvent`): `EventId`, `OccurredAt` (UTC), `EventType` = type name, `AggregateType`, `AggregateId`, abstract `SchemaVersion`. Payloads never contain notes, defect text or personal data (AC-29, AC-45). Line records use the ids PROD already uses (`SizeId`, `ColourId` as `short`).

```csharp
public sealed record QcLine(short SizeId, short ColourId, int Qty);

// BRD "GoodsReceipt.Recorded". AggregateType "GoodsReceipt", AggregateId = GrnId.
public sealed record GoodsReceiptRecordedEvent(
    long GrnId, string GrnNo, long RunId, long PoId, string PoNo, string VendorDnNo,
    DateOnly ReceivedDate, string ReconciliationCode,          // Matched | Short | Over
    int ClaimedTotal, int CountedTotal,
    IReadOnlyList<GrnEventLine> Lines) : QualityEvent(PoId, PoNo);
public sealed record GrnEventLine(short SizeId, short ColourId, int ClaimedQty, int CountedQty);

// BRD "QC.BatchAccepted". One per accepted or concession sub-batch. AggregateType "QcBatch", AggregateId = BatchId.
public sealed record QcBatchAcceptedEvent(
    long BatchId, long SubBatchId, long GrnId, long PoId, string PoNo,
    string OutcomeCode,                                         // Accepted | Concession  (the tag)
    IReadOnlyList<QcLine> Lines) : QualityEvent(PoId, PoNo);

// BRD "QC.BatchRejected". One per rejected sub-batch.
public sealed record QcBatchRejectedEvent(
    long BatchId, long SubBatchId, long GrnId, long PoId, string PoNo,
    IReadOnlyList<QcLine> Lines) : QualityEvent(PoId, PoNo);

// One per rework sub-batch sent back (not in the BRD catalogue; needed so the vendor side can see rework). (new)
public sealed record QcBatchReworkRequestedEvent(
    long BatchId, long SubBatchId, long GrnId, long PoId, string PoNo,
    IReadOnlyList<QcLine> Lines) : QualityEvent(PoId, PoNo);

// One per inspection round recorded (first pass and re-inspections). Scorecard input. (new)
public sealed record QcInspectionRecordedEvent(
    long BatchId, long SubBatchId /* 0 when the whole batch */, long GrnId, long PoId, string PoNo,
    short RoundNo,                                              // 1 = first pass
    string ModeCode,                                            // Aql | Full
    int LotQty, int SampleQty, int DefectiveQty,
    int CriticalCount, int MajorCount, int MinorCount,
    string ResultCode) : QualityEvent(PoId, PoNo);              // Pass | Fail
```

Rules: an event is written in the same transaction as the state change; consumers are idempotent. `QcBatchAccepted` is the only trigger SKU-GEN (Sprint 5) will use, so it fires only for `Accepted` and `Concession` quantities (AC-24).

### Events QC consumes

| Event | From | Handler (owner) | Effect |
|---|---|---|---|
| `DeliveryNoteRecordedEvent` (`Production.Contracts`) | PROD | `InboundDeliveries/RecordInboundDeliveryHandler` (**B**) | Upsert `QC.INBD_DLVR` + lines, keyed `(RUN_ID, VNDR_DN_NO)`, via `InboxGuard` (AC-1, AC-2) |
| `PoCancelledEvent` (`Vendor.Contracts`) | VNDR | same folder (**B**) | Mark open inbound deliveries of that PO as cancelled (AC-5) |

### Events VNDR consumes (group H, G)

| Event | Consumer | Owner |
|---|---|---|
| `GoodsReceiptRecordedEvent` | VNDR `ApplyGoodsReceiptRecordedHandler` (per Q7 default, awaiting approval: record receipt progress, no PO status change) | **H** |
| `DeliveryNoteRecordedEvent` (already delivered to PROD-side consumers) | VNDR scorecard fact handler | **G** |
| `QcInspectionRecordedEvent` | VNDR scorecard fact handler | **G** |

### In-process notification between C and D (same module, same transaction)

`Romp.Modules.Quality.Application.Receiving.GoodsReceiptRecordedNotification(long GrnId)` (MediatR `INotification`, defined by **C**). **D** implements `Batches/OpenQcBatchOnGoodsReceiptHandler : INotificationHandler<…>`, which creates the batch (status `Pending`, lines = GRN counted lines) inside the GRN's transaction (AC-12). C never references D's types; D never edits the GRN command. C's tests use a fake handler until D merges.

### `Quality.Contracts` query interface (created by B, implemented by E; consumed by SKU-GEN later)

```csharp
public interface IQualityQueries
{
    // Accepted + Concession sub-batch lines for a PO. Never returns Rejected, Rework, Pending or undecided quantity (AC-24, AC-25).
    Task<IReadOnlyList<ReleasedLine>> GetReleasedQuantitiesAsync(long poId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReleasedLine>> GetReleasedQuantitiesByBatchAsync(long batchId, CancellationToken cancellationToken);
    // Counted-so-far per size x colour for a PO across GRNs (used by VNDR receipt progress if it prefers a pull). 
    Task<IReadOnlyList<QcLine>> GetReceivedQuantitiesAsync(long poId, CancellationToken cancellationToken);
}
public sealed record ReleasedLine(long BatchId, long SubBatchId, short SizeId, short ColourId, int Qty, string OutcomeCode /* Accepted | Concession */);
```

Until E merges, B registers a stub implementation returning empty lists (like S3's `IProductionQueries` stub).

### `Vendor.Contracts` scorecard interface (created by G only)

```csharp
public interface IVendorScorecardQueries
{
    Task<VendorScorecard?> GetScorecardAsync(long vendorId, CancellationToken cancellationToken);
}
public sealed record VendorScorecard(
    long VendorId, int WindowDays,                       // 0 = all time
    int DeliveriesCounted, int DeliveriesOnTime, int DeliveriesOnQuantity, int DeliveriesExcludedRompDelay,
    int InspectedUnits, int DefectiveUnits,
    decimal? OnTimePct, decimal? OnQuantityPct, decimal? DefectRatePct,   // null = no data (AC-37)
    decimal? OnTimePctRolling, decimal? OnQuantityPctRolling, decimal? DefectRatePctRolling);
```
Formulas follow the Q2 default until approved.

### Table names (schema `QC`; every word 2–4 characters; B adds new abbreviations to the glossary)

New glossary words B must add (none exists today for these): `INSP` inspection, `OTCM` outcome, `RJCT` reject, `RWRK` rework, `CNCS` concession, `SPLT` split, `CHK` check, `SAFE` safety, `RSLT` result, `SVRT` severity, `CNTD` counted, `RCNL` reconciliation, `RCPT` receipt, `INBD` inbound, `LTR` letter, `LIMT` limit, `LOT` lot, `MODE` mode, `AQL` AQL, `CHOK` choking, `HZRD` hazard, `DRWS` drawstring, `TRIM` trim, `EXCS` excess, `AFCT` affected, `SCRD` scorecard, `RTRN` returned. (`ACPT`, `BTCH`, `CLM`, `DFCT`, `GRN`, `SMPL`, `QTY`, `LKP`, `STS`, `RSN`, `NOTE`, `LINE`, `MAIN`, `NO`, `ONTM`, `ONQT`, `OVER`, `SHRT`, `DN`, `DLVR`, `INBX`, `OUTB`, `MSG` exist.) Avoid reserved words; the glossary is the check.

| Table | Key columns | Notes |
|---|---|---|
| `INBD_DLVR` | `ID`, `RUN_ID`, `PO_ID`, `PO_NO`, `VNDR_DN_NO`, `DSPT_DT`, `FNL_SHIP_IND`, `CNCL_IND`, `PROD_TMNG_CODE`? no: none (facts stay in PROD) | unique `(RUN_ID, VNDR_DN_NO)`; index on `PO_ID`. QC's own copy of the DN so the receiving queue needs no cross-module read (AC-1). |
| `INBD_DLVR_LINE` | `INBD_DLVR_ID`, `SIZE_ID`, `CLR_ID`, `CLM_QTY` | unique per delivery/size/colour |
| `GRN_MAIN` | `ID`, `GRN_NO` (unique), `INBD_DLVR_ID` (unique, one GRN per DN), `RUN_ID`, `PO_ID`, `PO_NO`, `VNDR_DN_NO`, `RCVD_DT`, `GRN_STS_ID`, `GRN_RCNL_ID`, `CLM_TOT_QTY`, `CNTD_TOT_QTY`, `ACTR_NAME`, `NOTE` | audit + `ROW_VER` |
| `GRN_LINE` | `GRN_ID`, `SIZE_ID`, `CLR_ID`, `CLM_QTY`, `CNTD_QTY`, `DIFF_QTY`, `EXCS_IND` | unique per grn/size/colour |
| `GRN_NO_SEQ` | `YR` PK, `SEQ` | ADR 0006 counter, private to QC |
| `QC_BTCH_MAIN` | `ID`, `GRN_ID` (unique), `PO_ID`, `PO_NO`, `QC_BTCH_STS_ID`, `LOT_QTY` | audit + `ROW_VER` |
| `QC_BTCH_LINE` | `BTCH_ID`, `SIZE_ID`, `CLR_ID`, `QTY` | counted quantity per line |
| `QC_BTCH_SPLT` | `ID`, `BTCH_ID`, `SPLT_NO`, `QC_OTCM_ID?` (null until decided), `RSN`, `DCSN_DTE`, `ACTR_NAME`, `RWRK_RND_CNT`, `QC_BTCH_STS_ID` | sub-batch. A whole-batch decision is one sub-batch (`SPLT_NO` 1). unique `(BTCH_ID, SPLT_NO)`; `ROW_VER` |
| `QC_BTCH_SPLT_LINE` | `SPLT_ID`, `SIZE_ID`, `CLR_ID`, `QTY`, `RTRN_QTY` | unique per split/size/colour; check sum invariants in the domain and by a deferred integrity test (AC-22) |
| `QC_INSP_MAIN` | `ID`, `BTCH_ID`, `SPLT_ID?`, `RND_NO`, `QC_INSP_MODE_ID`, `INSP_LVL_ID`, `AQL_LVL_ID`, `LOT_QTY`, `SMPL_QTY`, `ACPT_NO`, `RJCT_NO`, `QC_INSP_RSLT_ID`, `INSP_DT`, `ACTR_NAME` | unique `(BTCH_ID, SPLT_ID, RND_NO)` treating null split as 0 |
| `QC_INSP_CHK` | `INSP_ID`, `QC_CHK_TYP_ID`, `QC_CHK_RSLT_ID` | six tech-pack items (AC-16); unique per inspection/type |
| `QC_INSP_SAFE` | `INSP_ID`, `QC_SAFE_TYP_ID`, `QC_CHK_RSLT_ID`, `RSN` | both safety checks (AC-31); unique per inspection/type |
| `QC_INSP_SAFE_LINE` | `INSP_SAFE_ID`, `SIZE_ID`, `CLR_ID` | affected lines of a failed check (AC-32); empty = whole batch |
| `QC_DFCT` | `ID`, `INSP_ID`, `QC_DFCT_TYP_ID`, `QC_DFCT_SVRT_ID`, `QTY`, `SIZE_ID?`, `CLR_ID?`, `NOTE` | defect notes mandatory (AC-17) |
| `QC_RWRK_RTRN` | `ID`, `SPLT_ID`, `RTRN_DT`, `ACTR_NAME`, `NOTE` | one row per rework return; lines in `QC_BTCH_SPLT_LINE.RTRN_QTY` (running) |
| `QC_AQL_SMPL` | `INSP_LVL_ID`, `LOT_MIN_QTY`, `LOT_MAX_QTY`, `CODE_LTR`, `SMPL_QTY` | seeded table (ISO 2859-1 Table I + IIa), no gaps or overlaps per level (AC-13) |
| `QC_AQL_LIMT` | `CODE_LTR`, `AQL_LVL_ID`, `ACPT_NO`, `RJCT_NO` | seeded table (Table IIa) |
| `INBX`, `OUTB_MSG` | platform convention | inbox row in the handler's transaction |

Lookups (standard shape `ID smallint`, `CODE`, `NAME`, `DSCR`, `SORT_SEQ`, `ACT_IND`), seeded by the first migration, **codes are stable and fixed here**:

| Lookup table | Codes (ids follow the listed order starting at 1) |
|---|---|
| `GRN_STS_LKP` | `Recorded` (a GRN is recorded in one step; the table exists so a draft state can be added by a row) |
| `GRN_RCNL_LKP` | `Matched`, `Short`, `Over` |
| `QC_BTCH_STS_LKP` | `Pending`, `InInspection`, `Decided`, `AwaitingRework`, `Closed` (sub-batches use `Pending`, `InInspection`, `Decided`, `AwaitingRework`) |
| `QC_OTCM_LKP` | `Accepted`, `Concession`, `Rejected`, `Rework` |
| `QC_INSP_MODE_LKP` | `Aql`, `Full` |
| `QC_INSP_RSLT_LKP` | `Pass`, `Fail` |
| `QC_INSP_LVL_LKP` | `S1`, `S2`, `S3`, `S4`, `I`, `II`, `III` |
| `QC_AQL_LVL_LKP` | `Aql0_65`, `Aql1_0`, `Aql1_5`, `Aql2_5`, `Aql4_0`, `Aql6_5` (extra column `AQL_VAL numeric(4,2)`) |
| `QC_CHK_TYP_LKP` | `Stitching`, `Measurements`, `FabricColourMatch`, `TrimsSecure`, `VisibleDefects`, `CareLabel` |
| `QC_CHK_RSLT_LKP` | `Pass`, `Fail`, `NotApplicable` |
| `QC_SAFE_TYP_LKP` | `ChokingHazardTrims`, `DrawstringNeckline` |
| `QC_DFCT_TYP_LKP` | `Stitching`, `Measurement`, `FabricColour`, `TrimsSecurity`, `VisibleDefect`, `CareLabel`, `Other` |
| `QC_DFCT_SVRT_LKP` | `Critical`, `Major`, `Minor` |

Denormalisation note: `PO_ID`, `PO_NO`, `RUN_ID`, `VNDR_DN_NO` are copied onto QC rows as plain columns (no cross-schema FK) because QC may not read PROD/VNDR tables; `CLM_TOT_QTY`/`CNTD_TOT_QTY` are totals derivable from lines and are stored for the list query (a read-model choice to avoid N+1 aggregates; they are written only by the aggregate).

### Configuration keys (J adds the settings files; B reads them through `IOptions`)

`Qc:FullInspectionMaxLot` (default **50**, Q1), `Qc:DefaultInspectionLevelCode` (`II`), `Qc:DefaultAqlLevelCode` (`Aql2_5`), `Qc:CriticalDefectTolerance` (default `0`), `Qc:ReceiptFutureDays` (`0`); `Vndr:Scorecard:WindowDays` (default **90**, Q2, owned by G). All defaults await approval.

### REST routes (all under `/api/quality`, one endpoint file per feature, requests in `*Requests.cs`)

| Method | Route | Owner |
|---|---|---|
| GET | `/api/quality/inbound-deliveries?status=&search=&page=&pageSize=` (receiving queue: open = no GRN) | **B** |
| POST | `/api/quality/grns` (body: inbound delivery id, received date, acting person, counted lines) | **C** |
| GET | `/api/quality/grns?search=&page=&pageSize=` and `/api/quality/grns/{id}` and `/api/quality/grns/by-po/{poId}` | **C** |
| GET | `/api/quality/batches?status=&search=&page=&pageSize=` and `/api/quality/batches/{id}` | **D** |
| GET | `/api/quality/aql/plan?lotQty=&inspectionLevel=&aqlLevel=` (calculator preview) | **D** |
| POST | `/api/quality/batches/{id}/inspections` (sample, checklist, defects; safety block is F's contract, see below) | **D** |
| POST | `/api/quality/batches/{id}/decision` (single outcome or list of sub-batches with lines) | **E** |
| POST | `/api/quality/sub-batches/{id}/rework-return` | **E** |
| PUT | `/api/quality/inspections/{id}/safety-checks` (records both checks; used by the same inspection screen) | **F** |
| GET | `/api/vendors/{id}/scorecard` | **G** |

D's inspection endpoint accepts an inspection without a safety block; F's endpoint attaches it. The **decision** endpoint (E) calls F's `ISafetyGate.EnsureCanRelease(batchId, lines)` (interface in `Quality.Application.SafetyChecks`, created by **F**; E codes against it and uses a fake until F merges). All error handling reuses the host exception mapping (validation 400, unknown id 404, domain rule 409/400 with the reason).

### Pure domain services (unit-tested, no I/O)

- `AqlCalculator` (D): `(lotQty, inspectionLevel, aqlLevel, fullInspectionMaxLot) -> AqlPlan(mode, sampleQty, acceptNo, rejectNo)`; reads from the seeded tables through an `IAqlPlanSource` abstraction (Domain has no EF).
- `ReconciliationJudge` (C): claimed vs counted per line -> `Matched | Short | Over` with per-line diff.
- `BatchSplitPlanner` (E): validates sub-batch lines sum to the batch lines per size x colour.
- `ReleasePolicy` (E): the **only** code that decides which outcomes are releasable (`Accepted`, `Concession`); everything else is not.
- `ScorecardCalculator` (G): pure formulas from facts.

## Domain model (`Romp.Modules.Quality.Domain`)

| Aggregate / entity | Purpose | Rules (invariants) |
|---|---|---|
| **`InboundDelivery`** (B) | QC's copy of a DN. | Unique `(RunId, VendorDnNo)`; immutable claimed lines; `Cancelled` when its PO is cancelled. |
| **`GoodsReceipt`** (C) root, `GoodsReceiptLine` | GRN against one inbound delivery. | One per delivery (AC-4); refused when delivery cancelled/unknown (AC-5); line size x colour on the DN or on the PO terms (AC-6); counts >= 0 whole numbers; receipt date in bounds (AC-8); reconciliation computed, never typed (AC-7); raises the recorded notification (AC-10, AC-12). |
| **`QcBatch`** (D) root, `QcBatchLine` | The lot to inspect: one per GRN (Q8). | Created only from a recorded GRN (AC-12); `LotQty` = sum of lines; cannot be decided without an inspection (AC-19). |
| **`Inspection`** (D), `InspectionCheck`, `Defect` | One inspection round on a batch or sub-batch. | Sample size and accept/reject numbers come from `AqlCalculator` (AC-13..15); every checklist type has a result (AC-16); a failed check needs a defect of that type (AC-17); result computed from counts (AC-18); acting person 2-100 chars (AC-30). |
| **`SafetyResult`** (F), `SafetyAffectedLine` | The two mandatory checks of an inspection. | Both checks present before any release (AC-31); `Fail` blocks Accept/Concession on affected lines (AC-32); `NotApplicable` needs a reason (AC-33); repeated on each rework round (AC-34). |
| **`QcSubBatch`** (E) child of batch, `SubBatchLine` | A decided part of a batch. | Lines sum to the batch's lines per size x colour (AC-22); outcome exactly one of the four (AC-20); reason required for Concession/Reject/Rework; decided once (AC-25); accepted/concession quantity released immediately (AC-23); rework -> `AwaitingRework` until a return (AC-26). |
| **`ReworkReturn`** (E) | Return of a rework sub-batch. | Returned qty per line <= sent; shortfall stays outstanding (AC-27); opens a new inspection round and re-requires safety checks (AC-34). |

Batch status flow: `Pending -> InInspection -> Decided` (all sub-batches decided or awaiting rework -> `Decided`; after the last rework sub-batch is decided the batch is `Closed`). Optimistic concurrency via `ROW_VER` on batch, sub-batch and GRN (AC-43).

## Application layer (`Romp.Modules.Quality.Application`)

| Command / Query | Group | Covers |
|---|---|---|
| `RecordInboundDeliveryCommand` (from `DeliveryNoteRecorded` handler) | B | AC-1, AC-2 |
| `MarkInboundDeliveriesCancelledCommand` (from `PoCancelled`) | B | AC-5 |
| `SearchInboundDeliveriesQuery` (paged, one SQL projection with GRN-exists flag) | B | AC-42 |
| `RecordGoodsReceiptCommand`, `SearchGoodsReceiptsQuery`, `GetGoodsReceiptQuery`, `ListGoodsReceiptsByPoQuery` | C | AC-3..11, AC-42 |
| `OpenQcBatchOnGoodsReceiptHandler` (notification), `SearchQcBatchesQuery`, `GetQcBatchQuery`, `PreviewAqlPlanQuery` | D | AC-12, AC-13..15 |
| `RecordInspectionCommand` (checklist + defects + computed result, writes `QcInspectionRecordedEvent`) | D | AC-16..19, AC-30 |
| `RecordSafetyChecksCommand`, `ISafetyGate` | F | AC-31..34 |
| `DecideBatchCommand` (single or split), `RecordReworkReturnCommand`, `IQualityQueries` implementation | E | AC-20..29 |
| VNDR `ApplyGoodsReceiptRecordedHandler` and receipt-progress query | H | AC-40, AC-41 |
| VNDR scorecard fact handlers, `GetVendorScorecardQuery`, `IVendorScorecardQueries` implementation | G | AC-35..39 |

Validators (FluentValidation) enforce lengths, ranges and the acting-person rule. Handlers run in the module's transaction behaviour so state, inbox row and outbox row commit together.

## Web (`features/quality`, admin app; group I)

Route `/quality` (receiving queue), `/quality/grns/:id`, `/quality/batches/:id`; a "Receiving & QC" entry in the shell navigation; the vendor screen gains a scorecard panel (`features/vendors` change, I).
- **containers:** `receiving-queue-page`, `grn-page`, `batch-page` (inspection + outcome panels), `qc-batches-page`.
- **components (presentational, OnPush):** `inbound-table`, `grn-form` (counted grid from the DN lines, `table.responsive`), `reconciliation-badge`, `inspection-form` (AQL plan preview, checklist, defects), `safety-checks-panel`, `outcome-panel` (single or split), `split-editor`, `rework-return-form`, `scorecard-panel`.
- **store:** `quality.{state,actions,reducer,selectors,effects}.ts` with `total/page/pageSize` and filters; reset on `Opened`.
- **a11y/responsive:** one `<h1>`, landmarks, labelled controls, 44 px targets, usable at 320 px, flags as text (AC-44).
- **E2E (Playwright, K):** receive a DN, GRN with a short count, inspection with defects and both safety checks, split with a passing and a rejected part, rework return and re-inspection, scorecard shown.

## NFR / security design

- **Idempotency:** inbound handler inbox keyed `<source schema>/<handler name>`; unique `(RUN_ID, VNDR_DN_NO)`; unique GRN per delivery; concurrency tokens (AC-2, AC-4, AC-43).
- **Transactional outbox (ADR 0004, NFR-FT-08):** state, inbox and outbox commit together; nothing calls a notification service inline.
- **Rejected never sellable (FR-SC-05):** `ReleasePolicy` is the single decision point; `IQualityQueries` reads only decided Accepted/Concession sub-batch lines; a test asserts no other path yields released quantity (AC-24).
- **Child safety:** `ISafetyGate` is called by every decision that could release quantity; no override flag exists.
- **Attribution (NFR-SC-05):** every write stores the system actor and a validated acting-person name.
- **Information hygiene (AC-29, AC-45):** events and screens are built from QC data plus contract results that exclude internal notes, cost sheets and target costs.
- **Logging (SEC-25):** no free-text notes in logs.
- **SEC-05 / SEC-08:** no roles or ownership until Sprint 8; endpoints are listed for the sweep.
- **Performance:** paged lists use one COUNT plus one page query with GRN/batch flags computed in SQL; detail views make one contract call at most; no per-row contract calls (AC-42). Partitioning notes (BRD §12.6): none needed at MVP volume; if QC tables grow large, partition `QC_INSP_MAIN` and `QC_DFCT` by year of `INSP_DT`.
- **Time:** dates are `date` in Pakistan time (fixed +05:00, as in Sprint 3), timestamps UTC.

## Risks & alternatives considered

- **AQL and sub-batch split (D/E) is the heaviest risk.** If the week overruns, the scorecard (G) spills to the start of Sprint 5.
- **The AQL table values must be checked against the published ISO 2859-1 / ANSI Z1.4 tables by the person implementing D.** The plan seeds code letters and accept/reject numbers as data with table tests; an error is a data fix, not a code change. Known anchor rows (to verify): lot 281-500 at level II is code H (sample 50, AQL 2.5 accept 3 reject 4); lot 1,201-3,200 is code K (sample 125, accept 7 reject 8).
- **`DeliveryNoteRecorded` has no DN row id.** QC keys on `(RunId, VendorDnNo)`. Adding an id to the PROD event would be a PROD change; not needed, so not proposed.
- **A GRN is recorded in one step**, so `GRN_STS_LKP` has one value. Keeping the lookup lets a draft state be added later by a row, per the growth rule.
- **B owns every migration and creates all entity shells up front.** This serialises the schema through one group, at the price of B being slightly larger; the alternative (each group with its own migration) risks conflicting model snapshots.
- **Q7 (GRN vs PO status)** may require ADR 0009 and a VNDR change larger than the default; group H must not start its consumer until Q7 is approved.
- **Two contracts reference each other's module** if VNDR reads `IQualityQueries`; the default design avoids it (VNDR uses events only).
- **Scorecard denormalisation:** VNDR stores its own fact rows from events, so a lost message leaves the scorecard stale until redelivery; the outbox health surface (SCRUM-186) makes that visible.

## Verification

Standing checks after every group (from Sprint 3): Docker SDK runner `dotnet build` and `dotnet test`, `dotnet ef migrations has-pending-model-changes` for every changed context, `ng test admin` and `ng build admin` when web files change, Architecture and naming tests green, every AC tagged `[Trait("Spec", ...)]`. Playwright against `docker compose up -d --build`. Integration tests that need locally built DLLs are verified by CI (Linux), per the environment note.
