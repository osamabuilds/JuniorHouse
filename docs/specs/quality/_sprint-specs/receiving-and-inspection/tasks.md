# SCRUM-197: Tasks

Ordered steps, each small enough for one commit and naming the test that proves it. Backend tests are xUnit v2, `Method_Scenario_Expectation` names, tagged `[Trait("Spec", "<ID>")]`; database tests use Testcontainers. Web tests are Vitest; user flows are Playwright. Commit messages start `SCRUM-<ticket>: ` (real keys assigned when the tickets are created). Follows [`docs/CONVENTIONS.md`](../../../../CONVENTIONS.md): API feature folders with matching namespaces, paged lists, no N+1, NgRx per web feature.

**How to use this file:** each section is one group (letter) and is meant to be taken by **one agent on one branch** off `sprint-4-receive-inspect`. Read [plan.md](plan.md) "Shared contracts" first: names, events, tables and lookup codes there are fixed. An agent that needs a change outside its group reports it to the integrator; it does not edit another group's area.

**Collision owners:** `ModuleNames`, host registration, naming glossary, **all QC migrations** and `Directory.Packages.props` = **B only**. Any `Romp.Modules.Vendor.*` change = **H only**. `Vendor.Contracts` scorecard additions and scorecard tables = **G only**. Compose, `Qc:*` settings and the demo seeder = **J only**. Traceability, README and spec status = **K only**.

**Standing checks after every group:** `dotnet build` + `dotnet test` in the Docker SDK container, `dotnet ef migrations has-pending-model-changes` for every changed context, `ng test admin` + `ng build admin` when web files change. Architecture and naming tests must stay green.

**Blocked until approval:** nothing below starts until spec.md and plan.md are approved. Tasks marked **[Qn]** depend on that open question's answer; they assume the recommended default only after the product owner approves it.

Dependency order (waves): 0 = A; 1 = B; 2 = C, D, H (H also needs Q7), J-part; 3 = E, F, G; 4 = I, J; 5 = K.

## A. Spec gate [wave 0]

- [x] A1. `spec.md` (AC-1..AC-46, open questions Q1–Q10) written and approved. Test: none (document); every AC has a source and `(new)` where the BRD is silent.
- [x] A2. `plan.md` (shared contracts fixed) written and approved; ADR 0009 written only if Q7 is answered so that a GRN changes PO status. Test: none (document).
- [ ] A3. `tasks.md` (this file) written and approved; Jira tickets created per group and linked to SCRUM-15, with SCRUM-94/95/91 links. Test: none.
- [ ] A4. Real Jira keys assigned; the Jira key is recorded in the spec header (the folder is named by module and feature, so no rename is needed). Test: none.

## B. Foundation: module skeleton, lookups, migrations, inbox [wave 1] (collision owner)

- [x] B1. New projects `Romp.Modules.Quality.{Domain,Application,Infrastructure,Contracts}` and test project `Romp.Modules.Quality.Tests`, `QualityModule : IModule` (schema `QC`), feature folders `InboundDeliveries/ Receiving/ Batches/ Inspections/ Outcomes/ SafetyChecks/`, host registration, add `"Quality"` to `ModuleNames`. Test: `ModuleBoundaryTests.*` (all theories cover Quality) (ADR 0002)
- [x] B2. Glossary: add every abbreviation listed in plan.md "Table names" to `docs/db/naming.md` under "Added in Sprint 4". Test: `NamingConventionTests.*` (all words 2–4 chars, no word with two abbreviations)
- [x] B3. `Quality.Contracts`: the event records (`GoodsReceiptRecordedEvent`, `QcBatchAcceptedEvent`, `QcBatchRejectedEvent`, `QcBatchReworkRequestedEvent`, `QcInspectionRecordedEvent`, `QcLine`, `QualityEvent`) and `IQualityQueries` + `ReleasedLine`, exactly as in plan.md, with a stub `IQualityQueries` returning empty lists. Test: `QualityContractsTests.Queries_NoData_ReturnEmpty`, `EventPayloadTests.Events_HaveEnvelopeAndSchemaV1` (AC-10, AC-29, AC-24)
- [x] B4. Lookup tables and seed (all lookups and codes in plan.md, standard shape) and the seeded AQL data (`QC_AQL_SMPL`, `QC_AQL_LIMT`) in the first `QC` migration. Test: `QcMigrationTests.Migrate_Lookups_SeededWithStableCodes`, `Migrate_AqlPlan_NoGapsOrOverlapsPerLevel` (AC-13, AC-20, AC-31) **[Q1, Q6 for the seeded values]**
- [x] B5. Persistent entity shells and one `IEntityTypeConfiguration` per table for all tables in plan.md (`INBD_DLVR`, `INBD_DLVR_LINE`, `GRN_MAIN`, `GRN_LINE`, `GRN_NO_SEQ`, `QC_BTCH_MAIN`, `QC_BTCH_LINE`, `QC_BTCH_SPLT`, `QC_BTCH_SPLT_LINE`, `QC_INSP_MAIN`, `QC_INSP_CHK`, `QC_INSP_SAFE`, `QC_INSP_SAFE_LINE`, `QC_DFCT`, `QC_RWRK_RTRN`, `INBX`, `OUTB_MSG`), audit columns, `ROW_VER`, declared and indexed FKs, uniques from plan.md. Test: `QcMigrationTests.Migrate_Schema_MatchesNamingRuleAndFkIndexes`, `Model_HasNoPendingChanges`, `Migrate_UniqueConstraints_Present` (AC-2, AC-4, AC-22)
- [x] B6. Outbox and inbox wiring: `AddOutboxModule("QC", …)`, dispatcher registration, `InboxGuard` use. Test: `QcOutboxTests.Publish_WritesRowInSameTransaction`, `Dispatcher_DeliversQcEvent` (AC-10, NFR-FT-08)
- [x] B7. Inbound delivery aggregate and `RecordInboundDeliveryHandler` for `DeliveryNoteRecordedEvent` (upsert by `(RunId, VendorDnNo)` via inbox). Test: `RecordInboundDeliveryHandlerTests.Handle_TwiceSameMessage_OneRecord`, `Handle_StoresClaimedLines` (AC-1, AC-2)
- [x] B8. `PoCancelled` handler marking open inbound deliveries cancelled. Test: `InboundDeliveryConsumerTests.PoCancelled_MarksDeliveriesCancelled` (AC-5)
- [x] B9. `SearchInboundDeliveriesQuery` (paged, stable order, GRN-exists flag in one SQL projection) and `GET /api/quality/inbound-deliveries`. Test: `SearchInboundDeliveriesQueryHandlerTests.Handle_TwoPages_OrderedWithTotal`, `Handle_QueryCount_DoesNotGrowWithRows`; `QualityEndpointsTests.InboundDeliveries_Ok` (AC-42)
- [x] B10. Configuration options classes for `Qc:*` and a `docker`-friendly default in `appsettings.json` (J supplies compose values). Test: `QcOptionsTests.Defaults_MatchPlan` (AC-46)

## C. Goods receipt (GRN) [wave 2] (needs B)

- [x] C1. `ReconciliationJudge` (pure): per-line diff and `Matched` / `Short` / `Over`. Test: `ReconciliationJudgeTests.Judge_Table` (theory: exact, short, over, mixed lines, zero counts) (AC-7)
- [x] C2. `GoodsReceipt` aggregate rules: one per delivery, refused on cancelled/unknown delivery, line validation (on DN or PO terms via `Vendor.Contracts.GetInForceTermsAsync`), counts whole numbers >= 0, receipt-date bounds, acting person 2–100 chars. Test: `GoodsReceiptTests.Open_DuplicateForDelivery_Refused`, `Open_CancelledPo_Refused`, `Open_LineNotOnDnOrPo_Refused`, `Open_DateBeforeDispatch_Refused`, `Open_NoActingPerson_Refused` (AC-3..6, AC-8, AC-30)
- [x] C3. GRN number generation (ADR 0006: `GRN-YYYY-NNNNN` on `GRN_NO_SEQ`, atomic upsert in the same transaction). Test: `GrnNumberTests.Allocate_Concurrent_NoDuplicates` (Testcontainers), `Allocate_NewYear_ResetsSequence` (AC-3)
- [x] C4. `RecordGoodsReceiptCommand` + validator: stores lines with claimed, counted, diff, excess flag; writes `GoodsReceiptRecordedEvent` to the outbox and raises `GoodsReceiptRecordedNotification(GrnId)` in the same transaction. Test: `RecordGoodsReceiptCommandHandlerTests.Handle_ShortCount_RecordedAndFlagged`, `Handle_Over_FlagsExcessLines` **[Q5]**, `Handle_WritesEventAndNotificationInOneTransaction`, `Handle_ConsumerDown_StillRecorded` (AC-7, AC-10, AC-11, AC-12, NFR-FT-08)
- [x] C5. Queries: `SearchGoodsReceiptsQuery` (paged), `GetGoodsReceiptQuery` (lines, reconciliation), `ListGoodsReceiptsByPoQuery` (all GRNs of a PO with totals). Test: `SearchGoodsReceiptsQueryHandlerTests.Handle_TwoPages_OrderedWithTotal`, `Handle_QueryCount_DoesNotGrowWithRows`, `ListByPoQueryHandlerTests.Handle_SeveralDns_SeveralGrns` (AC-9, AC-42)
- [x] C6. Endpoints `POST/GET /api/quality/grns…` in `Receiving/`, one file per feature, tagged for the S8 sweep. Test: `GrnEndpointsTests.Post_Valid_201`, `Post_Duplicate_409`, `Post_UnknownDelivery_404`, `Post_InvalidLine_400WithFieldErrors` (AC-3..6)
- [x] C7. Information hygiene: GRN detail and event exclude internal PO notes and costs. Test: `GetGoodsReceiptQueryHandlerTests.Handle_NeverExposesInternalPoNotes` (AC-45)

## D. QC inspection: batches, AQL sampling, defects [wave 2] (needs B; C provides the notification)

- [x] D1. `AqlCalculator` (pure) over `IAqlPlanSource`: sample size and accept/reject numbers by lot size, level and AQL; 100% at or below the full-inspection threshold; sample never above lot. Test: `AqlCalculatorTests.Plan_Table` (theory over the anchor rows in plan.md "Risks", both edges of every lot range, lot 1, lot = threshold, lot = threshold + 1), `Plan_NeverExceedsLot` (AC-13, AC-14, AC-15) **[Q1]**
- [x] D2. `IAqlPlanSource` EF implementation reading `QC_AQL_SMPL` / `QC_AQL_LIMT` (no gaps). Test: `AqlPlanSourceTests.Read_SeededTables_MatchesFixture` (Testcontainers) (AC-13)
- [x] D3. `QcBatch` aggregate (from GRN lines, lot = sum) and `OpenQcBatchOnGoodsReceiptHandler` (implements C's `INotificationHandler`, same transaction). Test: `OpenQcBatchOnGoodsReceiptHandlerTests.Handle_GrnRecorded_OneBatchPendingWithLines`, `Handle_Twice_OneBatch` (AC-12)
- [x] D4. `Inspection` rules: checklist has one result per tech-pack item, failed item requires a defect of that type, defect needs severity, quantity and non-empty note, result computed from counts against accept/reject numbers, critical tolerance from config, acting person. Test: `InspectionTests.Record_MissingChecklistItem_Refused`, `Record_FailedItemWithoutDefect_Refused`, `Record_DefectWithoutNote_Refused`, `Result_AtAcceptNo_Pass`, `Result_AtRejectNo_Fail`, `Result_AnyCritical_Fail` (AC-16, AC-17, AC-18, AC-30) **[Q1]**
- [x] D5. `RecordInspectionCommand` + validator: stores round, mode, level, sample, ac/re, checklist, defects; sets batch `InInspection`; writes `QcInspectionRecordedEvent` in the same transaction. Test: `RecordInspectionCommandHandlerTests.Handle_Aql_StoresPlanAndResult`, `Handle_SmallLot_FullInspection`, `Handle_WritesEventInOneTransaction` (AC-14, AC-16..18)
- [x] D6. `SearchQcBatchesQuery` (paged, status/search), `GetQcBatchQuery` (lines, inspections, defects, plan), `PreviewAqlPlanQuery`. Test: `SearchQcBatchesQueryHandlerTests.Handle_TwoPages_OrderedWithTotal`, `Handle_QueryCount_DoesNotGrowWithRows`, `GetQcBatchQueryHandlerTests.Handle_NeverExposesInternalPoNotes` (AC-42, AC-45)
- [x] D7. Endpoints in `Batches/` and `Inspections/`: batches list/detail, `aql/plan`, `POST batches/{id}/inspections`. Test: `QcBatchEndpointsTests.Inspect_Valid_201`, `Inspect_UnknownBatch_404`, `Inspect_Invalid_400WithFieldErrors` (AC-16..18)
- [x] D8. Concurrency: two inspections recorded at once on one batch. Test: `RecordInspectionCommandHandlerTests.Handle_ConcurrentWrites_SecondConflicts` (AC-43)

## E. Outcomes, sub-batch split, rework loop [wave 3] (needs C and D; codes against F's `ISafetyGate`)

- [x] E1. `ReleasePolicy` (pure): only `Accepted` and `Concession` are releasable. Test: `ReleasePolicyTests.IsReleasable_Table` (all four outcomes plus "none" and "pending") (AC-24)
- [x] E2. `BatchSplitPlanner` (pure): sub-batch lines must sum to the batch lines per size × colour; no negative or unknown lines. Test: `BatchSplitPlannerTests.Validate_Table` (exact, one unit short, one over, wrong size, two sub-batches, single whole-batch) (AC-22, AC-25)
- [x] E3. `QcSubBatch` rules: exactly one outcome, reason mandatory for Concession/Reject/Rework, decided once, requires a recorded inspection, calls `ISafetyGate.EnsureCanRelease`. Test: `QcSubBatchTests.Decide_NoInspection_Refused`, `Decide_ConcessionWithoutReason_Refused`, `Decide_Twice_Refused`, `Decide_SafetyGateRefuses_Refused` (fake gate) (AC-19, AC-20, AC-25, AC-31)
- [x] E4. `DecideBatchCommand` (whole batch or split): writes `QcBatchAccepted` / `QcBatchRejected` / `QcBatchReworkRequested` per sub-batch in the same transaction; accepted part released immediately even if another sub-batch is rework. Test: `DecideBatchCommandHandlerTests.Handle_Split_PassingReleasedRejectedNot`, `Handle_Split_ReworkDoesNotHoldAccepted`, `Handle_Concession_TaggedInEvent`, `Handle_WritesEventsInOneTransaction`, `Handle_ConsumerDown_StillRecorded` (AC-20..23, AC-28, AC-29, NFR-FT-08) **[Q3, Q4]**
- [x] E5. `RecordReworkReturnCommand`: returned qty <= sent, shortfall stays outstanding, new inspection round opened (and safety checks required again), round count tracked with no limit. Test: `RecordReworkReturnCommandHandlerTests.Handle_ReturnedMoreThanSent_Refused`, `Handle_ShortReturn_ShortfallOutstanding`, `Handle_Return_OpensNewRound`, `Handle_ThirdRound_Allowed` (AC-26, AC-27, AC-34)
- [x] E6. Real `IQualityQueries` implementation (released, by batch, received). Test: `QualityQueriesTests.Released_OnlyAcceptedAndConcession`, `Released_RejectedReworkPending_Excluded`, `Released_NeverExceedsCounted` (AC-24, AC-25)
- [x] E7. Guard test across the module: no code path other than `ReleasePolicy` produces released quantity. Test: `ReleaseInvariantTests.NoOtherType_ImplementsReleasedQuantity` (architecture-style reflection test) (AC-24)
- [x] E8. Endpoints in `Outcomes/`: `POST batches/{id}/decision`, `POST sub-batches/{id}/rework-return`. Test: `QcOutcomeEndpointsTests.Decision_Valid_200`, `Decision_SplitDoesNotSum_400`, `Decision_AlreadyDecided_409`, `ReworkReturn_Valid_200` (AC-20, AC-22, AC-25, AC-26)

## F. Child safety [wave 3] (needs D)

- [x] F1. `SafetyResult` aggregate rules: both checks present, `NotApplicable` needs a reason, `Fail` records affected lines (empty = whole batch). Test: `SafetyResultTests.Record_MissingCheck_Refused`, `Record_NotApplicableWithoutReason_Refused`, `Record_FailWithLines_StoresAffected` (AC-31, AC-33) **[Q6, Q9, Q10]**
- [x] F2. `ISafetyGate.EnsureCanRelease(batchId, lines)`: refuses Accept/Concession when a check is missing or failed for any listed line; failed lines allow only Reject/Rework. Test: `SafetyGateTests.Ensure_MissingCheck_Refused`, `Ensure_FailedAffectedLine_AcceptRefused`, `Ensure_FailedOtherLines_AcceptAllowedForUnaffected`, `Ensure_FailedLine_RejectAllowed` (AC-31, AC-32)
- [x] F3. `RecordSafetyChecksCommand` + `PUT /api/quality/inspections/{id}/safety-checks` (both checks in one call, acting person, timestamps). Test: `RecordSafetyChecksCommandHandlerTests.Handle_Valid_Stored`, `Handle_Repeated_ReplacesOnlyForNewRound`; `QcSafetyEndpointsTests.Put_Valid_200`, `Put_MissingCheck_400` (AC-30, AC-31)
- [x] F4. Re-check on rework return: a new round has no safety result until recorded. Test: `SafetyGateTests.Ensure_NewRoundWithoutChecks_Refused` (AC-34)
- [x] F5. No override: no parameter, header or config bypasses the gate. Test: `SafetyGateTests.NoBypassExposed_ReflectionCheck` (AC-32)

## G. Vendor scorecard [wave 3] (needs C and D events; only group allowed to edit `Vendor.Contracts` scorecard and scorecard tables)

- [x] G1. `ScorecardCalculator` (pure): on-time %, on-quantity %, defect rate, "no data" as null, `RompFabricDelay` excluded from on-time, rolling window. Test: `ScorecardCalculatorTests.Calculate_Table` (no data, all on time, mixed, Romp delay excluded, only-late-within-acceptable, rolling window cut-off) (AC-35, AC-36, AC-37) **[Q2]**
- [x] G2. VNDR scorecard fact tables (schema change via H-independent migration in `VNDR`, coordinated with the integrator so H's migration and G's do not collide) and inbox handlers for `DeliveryNoteRecordedEvent` and `QcInspectionRecordedEvent` (resolves vendor from the PO inside VNDR; idempotent via inbox). Test: `ScorecardFactHandlerTests.DeliveryNote_TwiceSameMessage_CountedOnce`, `Inspection_TwiceSameMessage_CountedOnce`, `Inspection_Rework_FollowsQ2Rule` (AC-38) **[Q2]**
- [x] G3. `IVendorScorecardQueries` + `VendorScorecard` in `Vendor.Contracts`, `GetVendorScorecardQuery`, `GET /api/vendors/{id}/scorecard` (one SQL aggregate, no per-row calls). Test: `GetVendorScorecardQueryHandlerTests.Handle_NoFacts_ReturnsNulls`, `Handle_QueryCount_IsConstant`; `VendorContractsTests.Scorecard_ReadOnly` (AC-35, AC-37, AC-39)
- [x] G4. Scorecard never exposes PO internal notes or costs. Test: `GetVendorScorecardQueryHandlerTests.Handle_NeverExposesInternalPoNotes` (AC-45)

## H. PO reacts to goods receipt [wave 2] (only group allowed to edit `Romp.Modules.Vendor.*`; starts only after Q7 is approved)

- [x] H1. If Q7 is answered so that a GRN changes PO status: write ADR 0009 and update this group before any code. Otherwise skip. Test: none (document) **[Q7]**
- [x] H2. VNDR receipt-progress storage (additive migration in `VNDR`, coordinated with G's) and `ApplyGoodsReceiptRecordedHandler` (idempotent via inbox, forward-only, ignores stale or duplicate GRNs). Test: `GoodsReceiptConsumerTests.Recorded_TwiceSameMessage_CountedOnce`, `Recorded_OutOfOrder_NoRegression`, `Recorded_ChangesNoPoStatus` (AC-40, AC-41) **[Q7]**
- [x] H3. Read model of received-versus-ordered per PO for the PO detail (paged where listed, no N+1). Test: `GetPoReceiptProgressQueryHandlerTests.Handle_SeveralGrns_SumsPerSizeColour`, `Handle_QueryCount_IsConstant` (AC-40, AC-42)
- [x] H4. Sprint 3 rules unchanged: cancel and close behave as before. Test: `ClosePurchaseOrderCommandHandlerTests.*` and `CancelPurchaseOrderCommandHandlerTests.*` pass unmodified (AC-41)

## I. Admin UI [wave 4] (needs contract stubs from B; final wiring after C..G)

- [x] I1. `features/quality` skeleton per conventions (models, service, lazy routes `/quality`, nav "Receiving & QC", store registration on the route). Test: `quality.reducer.spec` initial state; `app.spec` nav shows Receiving & QC
- [x] I2. Store: state with `total/page/pageSize` and filters, actions, selectors, effects for queue, GRN, batch; reset on `Opened`. Test: `quality.reducer.spec.*`, `quality.selectors.spec.*` (AC-42)
- [x] I3. `receiving-queue-page` + `inbound-table` (paged, status/search filters, GRN-exists state as text). Test: `inbound-table.component.spec` (AC-1, AC-44)
- [x] I4. `grn-page` + `grn-form` + `reconciliation-badge` (counted grid from DN lines with `table.responsive`, claimed vs counted, Short/Over as text). Test: `grn-form.component.spec`, `reconciliation-badge.component.spec` (AC-3, AC-6, AC-7, AC-11, AC-44)
- [x] I5. `batch-page` with `inspection-form` (AQL plan preview, checklist, defects with notes) and `safety-checks-panel`. Test: `inspection-form.component.spec`, `safety-checks-panel.component.spec` (AC-13..18, AC-31..33)
- [x] I6. `outcome-panel` + `split-editor` (single or split, live sum check per size × colour, reason required, released/rejected shown as text) and `rework-return-form`. Test: `outcome-panel.component.spec`, `split-editor.component.spec`, `rework-return-form.component.spec` (AC-20..23, AC-26, AC-27)
- [x] I7. Vendor screen scorecard: `scorecard-panel` (on-time %, on-quantity %, defect rate, denominators, "no data", rolling figure). Test: `scorecard-panel.component.spec.NoData_ShowsNoData` (AC-35, AC-37, AC-39)
- [x] I8. Semantic and responsive pass: one `<h1>`, landmarks, labelled controls, 44 px targets, usable at 320 px, no component injects the store or a service. Test: extend `responsive.e2e.spec.ts` with the quality screens at all widths (AC-44)

## J. Demo data, Docker, docs [wave 2 scaffolding, wave 4 completion]

- [x] J1. (wave 2) Seeder scaffolding: `DemoDataSeeder` extension point for QC that runs after PROD's demo POs; idempotent. Test: `DemoDataSeederTests.Seed_Twice_NoDuplicates` (demo)
- [x] J2. (wave 4) Seed a DN with a GRN for each outcome: matched and accepted; short count; concession; reject; rework awaiting return; rework returned and re-inspected; split with a passing and a rejected part; a safety fail; a received-not-inspected batch. Test: `DemoDataSeederTests.Seed_QcStates_EachOutcomePresent`
- [x] J3. (wave 4) Compose/API configuration for the `QC` schema and `Qc:*` and `Vndr:Scorecard:*` settings; README "Try Sprint 4" walkthrough. Test: `docker compose up -d --build` starts healthy and `/health/ready` is 200 (AC-46)

## K. Tests and close-out [wave 5] (needs everything merged)

- [x] K1. Contract and integration tests: PROD → QC → VNDR event chain over a real API and PostgreSQL (DN → inbound delivery → GRN → batch → decision → events; scorecard updated). Test: `ReceiveInspectFlowTests.*` in `Romp.Api.IntegrationTests` (CI is the authority) (AC-1, AC-10, AC-29, AC-35)
- [x] K2. Architecture and migration suite: module boundaries, naming rule, `has-pending-model-changes` clean for every context. Test: `ModuleBoundaryTests.*`, `NamingConventionTests.*`
- [x] K3. Playwright happy path: DN received → GRN with a short count → inspection with a defect and both safety checks → split (passing part accepted, failing part rework) → rework return and re-inspection accepted → scorecard shown. Test: `quality.e2e.spec.ts › full receive and inspect flow` (AC-3..26, AC-35)
- [x] K4. Playwright edge cases: duplicate GRN refused; outcome without inspection refused; Accept refused without safety checks; failed safety check blocks Accept; split that does not sum refused; over-delivery flagged; rejected quantity absent from released. Test: `quality.e2e.spec.ts › edge cases` (AC-4, AC-11, AC-19, AC-22, AC-24, AC-31, AC-32)
- [x] K5. Existing E2E and unit tests pass unchanged. Test: full Playwright run against `docker compose up -d --build`
- [x] K6. Requirement-ID traceability: every AC-1..AC-46 has at least one test tagged `[Trait("Spec", ...)]` or named in the test title; list any gap in `traceability.md`. Test: a scripted check listing untagged ACs is attached to the PR
- [ ] K7. Update `spec.md` to **Implemented** with an "Implementation notes" section listing every deviation and every resolved open question; open the PR with the spec folder link and covered requirement IDs; move Jira issues per workflow; link SCRUM-94, SCRUM-95, SCRUM-91's remainder. Test: none (document and workflow)
