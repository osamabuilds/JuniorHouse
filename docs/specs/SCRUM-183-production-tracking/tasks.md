# SCRUM-183: Tasks

Ordered steps, each small enough for one commit and naming the test that proves it. Backend tests are xUnit v2, `Method_Scenario_Expectation` names, tagged `[Trait("Spec", "<ID>")]`; database tests use Testcontainers. Web tests are Vitest; user flows are Playwright. Ordering follows build dependency (what must exist before what). Commit messages start `SCRUM-<ticket>: ` with the Jira ticket in brackets after each group heading. Follows [`docs/CONVENTIONS.md`](../../CONVENTIONS.md): every new API type goes in a feature folder whose namespace matches, paged lists, no N+1, NgRx per web feature.

**Standing checks after every group:** `dotnet build` + `dotnet test` in the Docker SDK container, `dotnet ef migrations has-pending-model-changes` for every changed context, `ng test admin` + `ng build admin` when web files change. The Architecture and naming tests must stay green (add the new module to `ModuleNames` in task 12).

## 0. Housekeeping

- [ ] 1. Confirm the `refactor/feature-architecture` PR (conventions, NgRx, paging) is merged to `main`, then rebase this branch. No new test: this is picking up verified work. **Needs you.**
- [ ] 2. Transition finished Sprint 2 Jira items (SCRUM-180 and others) and log the SCRUM-94 split note (already commented). No test.

## A. Shared building block: file storage (ADR 0008) [SCRUM-185, SCRUM-184]

- [ ] 3. Move `IFileStorage`, `LocalFileStorage`, the storage options and the content-signature detection from `Vendor` into `Romp.BuildingBlocks.Storage`; keep Vendor's categories, limits and DI keys unchanged. Test: the existing Sprint 2 file tests pass unmodified (`LocalFileStorageTests.*`, `UploadPoFileCommandHandlerTests.*`) (AC-12 groundwork)
- [ ] 4. Architecture test: `BuildingBlocks.Storage` has no dependency on any module. Test: `ModuleBoundaryTests.BuildingBlocksStorage_DoesNotDependOnModules` (ADR 0002)

## B. REF: lookup rows [SCRUM-190]

- [ ] 5. `PO_STS_LKP` rows 5–8 (`InProduction`, `PartiallyDelivered`, `Delivered`, `Closed`) and `PO_CNCL_RSN_LKP` row 7 `ProductionAlreadyStarted` ("Cancelled after production started"), by migration, with the seed and the Vendor `PoStatus` constants changed together. Test: `RefMigrationTests.Migrate_PoStatusesAndCancelReason_SeededWithStableIds` (AC-14, AC-28)
- [ ] 6. Both lookups still list correctly and `po-statuses` stays read-only in Reference Data. Test: `ListLookupQueryHandlerTests.Handle_PoStatuses_IncludesProductionStates` (AC-14)

## C. VNDR: PO lifecycle after Acknowledged [SCRUM-190]

- [ ] 7. `PoStatus` constants and forward-only, idempotent `StartProduction()`, `MarkPartiallyDelivered()`, `MarkDelivered()`, `Close()` on `PurchaseOrder`, each writing status history and raising its event (`PoProductionStarted`, `PoPartiallyDelivered`, `PoDelivered`, `PoClosed`, schema v1 with the standard envelope). Test: `PurchaseOrderLifecycleTests.Transitions_ForwardOnly_IdempotentAndRaiseEvents` (AC-14, AC-24)
- [ ] 8. `Cancel()` allowed from Acknowledged, InProduction, PartiallyDelivered; refused from Delivered/Closed; the `ProductionAlreadyStarted` reason accepted. Test: `CancelPurchaseOrderCommandHandlerTests.Handle_InProduction_CancelsWithReason` and `Handle_DeliveredOrClosed_Refused` (AC-28)
- [ ] 9. `ClosePurchaseOrderCommand` (Delivered → Closed, acting person required); refused in any other state; endpoint `POST /api/purchase-orders/{id}/close` in the `PurchaseOrders` feature. Test: `ClosePurchaseOrderCommandHandlerTests.Handle_Delivered_Closes` and `Handle_NotDelivered_Refused` (AC-25)
- [ ] 10. Revision classification: `CreateRevision` sets **Construction** (files added/retired, fabric responsibility changed, a new colour) or **Commercial** (everything else); additive migration `PO_REV.CNSTR_CHNG_IND` (existing rows default to commercial); exposed on `PoRevisionDto` and in revision events. Test: `PurchaseOrderRevisionTests.Classify_FilesFabricColour_Construction` and `Classify_CostDateTerms_Commercial` (AC-27)
- [ ] 11. Amendments refused on Delivered/Closed with a clear message (all three amendment paths). Test: `CreateAmendmentCommandHandlerTests.Handle_DeliveredOrClosed_Refused` (AC-26)

## D. PROD module skeleton and contracts [SCRUM-184]

- [ ] 12. New projects `Romp.Modules.Production.{Domain,Application,Infrastructure,Contracts}` and test project `Romp.Modules.Production.Tests`, `ProductionModule : IModule` (schema `PROD`), feature folders `ProductionRuns/ Milestones/ Samples/ Deliveries/`, register the module in the host and add `"Production"` to `ModuleNames`. Add the new abbreviations to `docs/db/naming.md` (`RND`, `FNL`, `DSPT`, `RCRD`, `RPTR`, `CUT`, `FNSH`, `RDY`, `SHRT`, `SHIP`, `DCSN`, `TMNG`). Test: `ModuleBoundaryTests.*` (all three theories cover Production), `NamingConventionTests.*` (ADR 0002)
- [ ] 13. `Vendor.Contracts`: `GetTermsAtAsync(poId, asOfDate)` (terms, tolerances and lines of the revision in force on that Pakistan-time date; Rev 0 counts as in force from the PO send date when no put-in-force timestamp exists) and `GetProductionContextAsync(poId)` (PO no, send date, status, fabric responsibility). Test: `VendorContractsTests.GetTermsAt_AfterAmendment_ReturnsRevisionInForceOnThatDate` and `GetTermsAt_BackfilledRevisionZero_UsesSendDate` (AC-23)
- [ ] 14. `Production.Contracts`: `IProductionQueries` (`GetDeliveredQuantitiesAsync`, `HasApprovedPpSampleAsync`) with a stub implementation registered by the PROD module. Test: `ProductionContractsTests.Queries_NoRun_ReturnEmptyAndFalse` (AC-26)

## E. PROD persistence [SCRUM-187, SCRUM-188, SCRUM-189, SCRUM-191]

- [ ] 15. Lookup tables `PROD_RUN_STS_LKP`, `MLST_TYP_LKP` (seven types incl. `FabricDispatchedByRomp`), `PP_SMPL_DCSN_LKP`, `DLVR_TMNG_LKP`, `DLVR_QTY_STS_LKP`, seeded by the first `PROD` migration, standard lookup shape. Test: `ProdMigrationTests.Migrate_Lookups_SeededWithStableCodes` (AC-4)
- [ ] 16. Tables `PROD_RUN_MAIN`, `PROD_MLST`, `PROD_MLST_FNSH_LINE`, `PROD_EXPC_HIST`, `PP_SMPL_RND`, `PP_SMPL_FILE`, `DLVR_NOTE`, `DLVR_NOTE_LINE`, `INBX`, `OUTB_MSG`, one `IEntityTypeConfiguration` per entity, audit columns, concurrency token, declared and indexed FKs, uniques `(PO_ID)`, `(RUN_ID, RND_NO)`, `(RUN_ID, VNDR_DN_NO)`. Test: `ProdMigrationTests.Migrate_Schema_MatchesNamingRuleAndFkIndexes`, `Model_HasNoPendingChanges` (AC-2, AC-20)

## F. PROD domain [SCRUM-187..192]

- [ ] 17. `ProductionRun` aggregate: creation from PO context, status, unique per PO, nothing recordable when Cancelled/Closed. Test: `ProductionRunTests.Open_ForAcknowledgedPo_CreatesRunInOpenState`, `Record_OnCancelledRun_Refused` (AC-1, AC-29)
- [ ] 18. Milestone recording rules: valid types per fabric responsibility, date bounds (send date..today, Pakistan time), recorded-late flag from the configured days, optional milestones skippable. Test: `MilestoneTests.Record_TypeNotValidForFabricResponsibility_Refused`, `Record_DateOutsideBounds_RefusedWithRange`, `Record_MoreThanThresholdLater_FlaggedRecordedLate`, `Skip_OptionalMilestones_LaterOnesStillRecordable` (AC-4, AC-5, AC-6, AC-7, AC-8, AC-35)
- [ ] 19. PP sample rounds: submit (one pending at a time), approve, reject (reason mandatory), unlimited rounds with numbers, approval final unless superseded by an amendment. Test: `PpSampleRoundTests.Submit_WhilePending_Refused`, `Reject_WithoutReason_Refused`, `Reject_ThenSubmit_OpensRoundTwo`, `SubmitAfterApproval_WithoutAmendment_Refused` (AC-10, AC-11, AC-13)
- [ ] 20. Bulk-cutting gate: refused without an approved round (AC-9); recorded once; on `RompSupplied` runs without an earlier fabric dispatch it is recorded and flagged (AC-36); with an unresolved re-sample recommendation it is recorded and flagged (AC-38). Test: `BulkCuttingTests.Record_NoApprovedRound_Refused`, `Record_RompSuppliedNoFabricDispatch_RecordedWithFlag`, `Record_ResampleRecommended_RecordedWithFlag` (AC-9, AC-36, AC-38)
- [ ] 21. Expected completion with history, and the at-risk rule (expected > latest acceptable − buffer days, default 7). Test: `ExpectedCompletionTests.Set_KeepsHistory` and `AtRisk_WithinBuffer_True` (AC-15, AC-16)
- [ ] 22. Finished-quantity milestone lines (positive, on the PO), gating nothing. Test: `FinishedQuantityTests.Record_UnknownSizeColour_Refused`, `NotRecorded_DoesNotBlockDelivery` (AC-17)
- [ ] 23. `DeliveryJudge` (pure): `maxAllowed = floor(ordered × (1 + over%))`, `minRequired = ceil(ordered × (1 − under%))`; timing OnTime/Late/BeyondAcceptable; Over flagged and never refused; Delivered when all lines ≥ `minRequired` or final shipment; short recorded on a final shipment. Test: `DeliveryJudgeTests.Judge_Table` (theory covering exact tolerance edges, zero tolerance, null tolerance, over, short final, partial) (AC-21, AC-22, AC-24)
- [ ] 24. Delivery note rules: valid lines, positive whole quantities, dispatch date bounds, duplicate vendor DN number refused, judged against the terms at the dispatch date, `RompSupplied` fabric-dispatch date and "Romp fabric delay" indicator stored. Test: `DeliveryNoteTests.Record_DuplicateDnNo_Refused`, `Record_UsesRevisionInForceOnDispatchDate`, `Record_RompFabricDispatchedAfterCutting_MarksRompFabricDelay` (AC-18, AC-19, AC-20, AC-23, AC-37)
- [ ] 25. Run reactions: re-sample recommended set when a Construction revision comes into force after an approved round, cleared by a new round; run cancelled/closed from PO events. Test: `ProductionRunTests.ConstructionRevisionAfterApproval_SetsResampleRecommended`, `CommercialRevision_OnlyRefreshesDate`, `NewRound_ClearsRecommendation` (AC-27, AC-29)

## G. PROD application layer [SCRUM-187..192]

- [ ] 26. `OpenProductionRunCommand` + the `PoAcknowledged` inbox handler (inbox row and effect in one transaction; run for non-acknowledged POs refused). Test: `OpenProductionRunTests.Handle_TwiceSameMessage_OneRun`, `Handle_NonAcknowledgedPo_NoRun` (AC-1, AC-2, AC-3)
- [ ] 27. `RecordMilestoneCommand` + validator (acting person 2–100 chars) + `Production.MilestoneReached` outbox event, and the `ProductionStarted` event when `BulkCuttingStarted` is recorded. Test: `RecordMilestoneCommandHandlerTests.*` incl. `Handle_BulkCutting_WritesMilestoneAndProductionStartedInOneTransaction` (AC-5, AC-9, AC-30, AC-35)
- [ ] 28. `SubmitPpSampleCommand`, `DecidePpSampleCommand` (approve/reject) with milestone rows and events. Test: `DecidePpSampleCommandHandlerTests.Handle_Approve_RecordsApproverAndEnablesCutting` (AC-10, AC-11)
- [ ] 29. PP-sample files: `UploadPpSampleFileCommand`, `RemovePpSampleFileCommand`, `GetPpSampleFileQuery` on the shared storage, allowlist/size/count limits from `Prod:PpFiles:*`, download with `attachment` and `nosniff`. Test: `PpSampleFileTests.Upload_DisguisedExecutable_Refused`, `Upload_OverLimit_Refused`, `Download_HasSafeHeaders` (AC-12)
- [ ] 30. `SetExpectedCompletionCommand` (date, reason, acting person). Test: `SetExpectedCompletionCommandHandlerTests.Handle_RecordsHistory` (AC-15)
- [ ] 31. `RecordDeliveryNoteCommand` (fetches terms at dispatch date via `Vendor.Contracts`, judges, stores, writes `DeliveryNoteRecorded` with PO outcome in the same transaction). Test: `RecordDeliveryNoteCommandHandlerTests.Handle_OverTolerance_RecordedAndFlagged`, `Handle_FinalShort_DeliveredAndShort`, `Handle_NotificationDown_StillRecorded` (AC-21, AC-24, AC-31)
- [ ] 32. PO event consumers: `PoCancelled` → run cancelled, `PoClosed` → run closed, `PoRevisionPutInForce` → date copy and classification. Test: `PoEventConsumerTests.PoCancelled_ClosesRunAsCancelled`, `RevisionPutInForce_UpdatesLatestAcceptableDateCopy` (AC-16, AC-29)
- [ ] 33. `SearchProductionRunsQuery` (status, PO no text, at-risk, paged, stable order, single SQL projection with shipped-to-date aggregate) and `GetProductionRunQuery` (one contract call for terms, one children query, no internal PO data). Test: `SearchProductionRunsQueryHandlerTests.Handle_TwoPages_OrderedWithTotal`, `Handle_QueryCount_DoesNotGrowWithRuns`, `GetProductionRunQueryHandlerTests.Handle_NeverExposesInternalPoNotes` (AC-19, AC-32, AC-34)
- [ ] 34. `IProductionQueries` real implementation. Test: `ProductionQueriesTests.GetDeliveredQuantities_SumsAllNotesPerSizeColour`, `HasApprovedPpSample_TrueAfterApproval` (AC-26, AC-27)

## H. VNDR consumers and guards [SCRUM-190]

- [ ] 35. `VNDR.INBX` migration and the inbox configuration from the platform convention. Test: `VndrMigrationTests.Migrate_Inbox_HasCompositeKey` (AC-2)
- [ ] 36. Inbox handlers `ProductionStarted` → `StartProduction()` and `DeliveryNoteRecorded` → partially delivered / delivered (forward-only, idempotent, ignores stale outcomes). Test: `ProductionEventHandlerTests.ProductionStarted_TwiceSameMessage_OneTransition`, `DeliveryRecorded_Delivered_AfterPartially_Advances`, `DeliveryRecorded_OutOfOrder_NoRegression` (AC-14, AC-24)
- [ ] 37. Amendment guard: no line below its delivered quantity, using `IProductionQueries`; a revision after an approved PP round is marked costly (both tiers) and the response says whether a new PP round is suggested (Construction) or optional (Commercial). Test: `CreateAmendmentCommandHandlerTests.Handle_LineBelowDelivered_Refused`, `Handle_AfterPpApproval_FlaggedCostly`, `Handle_ConstructionAfterPpApproval_SuggestsNewRound`, `Handle_CommercialAfterPpApproval_NewRoundOptional` (AC-26, AC-27)

## I. HTTP API [SCRUM-187..191]

- [ ] 38. `Production` endpoints per plan.md (runs list/detail/by-po, milestones, pp-rounds, decision, files, expected completion, delivery notes), one endpoint file per feature, requests in `*Requests.cs`, tagged for the Sprint 8 role sweep. Test: `ProductionEndpointsTests.*` in `Romp.Api.IntegrationTests` (each route returns the expected status; unknown id 404; validation 400 with field errors) (AC-5, AC-9, AC-32)
- [ ] 39. PO endpoints: `close`, and `cancel` for the new states. Test: `PurchaseOrderEndpointsTests.Close_Delivered_Ok`, `Cancel_InProduction_Ok` (AC-25, AC-28)

## J. Sprint 2 carry-over [SCRUM-186]

- [ ] 40. `PoCreatedEvent` carries the PO id (id assigned before the event is raised); the outbox row gets its `AGGR_ID`. Test: `EventPayloadTests.PoCreated_HasPoIdAndAggregateId` (SCRUM-186)
- [ ] 41. Outbox health: `GET /api/admin/outbox` (pending, in-flight, retrying, dead-lettered counts per module and the oldest pending age) and an indicator in the admin shell. Test: `OutboxHealthQueryTests.Counts_ByState` and Vitest `outbox-health.component.spec` (SCRUM-186)

## K. Web: production feature [SCRUM-193]

- [ ] 42. `features/production` skeleton per conventions (models, service, routes lazy-loaded at `/production`, nav link, store registration on the route). Test: `production.reducer.spec` initial state; `app.spec` nav shows Production
- [ ] 43. Store: state with `total/page/pageSize` and filters, actions (Opened, filters, paging, Load Succeeded/Failed), selectors, effects for list and detail; reset on `Opened`. Test: `production.reducer.spec.*`, `production.selectors.spec.*` (AC-32)
- [ ] 44. `production-runs-page` container + `run-table` component (paged, status/PO-no/at-risk filters, at-risk and flag badges as text). Test: `run-table.component.spec` (AC-16, AC-33)
- [ ] 45. `production-run-page` container + `milestone-timeline` and `milestone-form` (acting person, reporter, claimed date within bounds, recorded-late badge, loud flags for AC-36/AC-38). Test: `milestone-form.component.spec`, `milestone-timeline.component.spec` (AC-5..8, AC-36, AC-38, AC-33)
- [ ] 46. `pp-round-panel` (round history, submit, approve/reject with reason, photo upload with error display). Test: `pp-round-panel.component.spec` (AC-10, AC-11, AC-12)
- [ ] 47. `expected-completion-panel` and the finished-quantity entry. Test: `expected-completion-panel.component.spec` (AC-15, AC-17)
- [ ] 48. `delivery-note-form` and `delivery-note-list`: quantity grid from the PO lines, final-shipment checkbox, outcome shown with loud Over / Short / Late / Recorded-late / Romp-fabric-delay text flags, shipped-to-date vs ordered. Test: `delivery-note-form.component.spec`, `delivery-note-list.component.spec` (AC-18..24, AC-37)
- [ ] 49. Semantic and responsive pass: one `<h1>`, landmarks, labelled controls, 44 px targets, usable at 320 px; no component injects the store or a service. Test: extend `responsive.e2e.spec.ts` with the production screens at all widths (AC-33)

## L. Web: purchase-orders changes [SCRUM-190]

- [ ] 50. Status labels for 5–8; Close button for Delivered POs; link from PO detail to its run; the PO list filter offers the new statuses. Test: `po-detail.component.spec` (Close shown only when Delivered) (AC-25)
- [ ] 51. Cancel panel: production-stage reason offered, prominent sunk-cost / vendor-impact warning for InProduction / PartiallyDelivered before confirming. Test: `po-detail.component.spec.Cancel_InProduction_ShowsWarning` (AC-28)
- [ ] 52. Amend panel: costly notice after PP approval; strong "new PP sample recommended" notice for Construction amendments; delivered-quantity limit message. Test: `amend-form.component.spec.ConstructionAmendment_ShowsResampleNotice`, `CommercialAmendment_ShowsCostlyNoteOnly` (AC-26, AC-27)

## M. Demo data, Docker, docs [SCRUM-194]

- [ ] 53. `DemoDataSeeder` adds one PO in each production state (acknowledged with run, PP rejected then approved, in production, partially delivered, delivered short with final shipment, cancelled in production); idempotent. Test: `DemoDataSeederTests.Seed_Twice_NoDuplicates` (demo)
- [ ] 54. Compose/API configuration for the `PROD` schema and `Prod:*` settings (`RecordedLateDays` 3, `AtRiskBufferDays` 7, PP file limits); README "Try Sprint 3" walkthrough; `docs/db/naming.md` glossary complete. Test: `docker compose up -d --build` starts healthy and `/health/ready` is 200

## N. End-to-end and close-out [SCRUM-195, SCRUM-196]

- [ ] 55. Playwright happy path: acknowledge PO → milestone → PP reject then approve with a photo → bulk cutting (PO shows In Production) → DN 1 partial → DN 2 final short (Delivered with Short flag) → Close. Test: `production.e2e.spec.ts › full production flow` (AC-1..25)
- [ ] 56. Playwright edge cases: cutting blocked without approval; back-date bounds; over-delivery recorded with the Over flag; `RompSupplied` cutting before fabric dispatch flagged; cancel in production with warning; construction amendment shows the re-sample notice; amendment below delivered refused; duplicate DN number refused. Test: `production.e2e.spec.ts › edge cases` (AC-6, AC-9, AC-20, AC-21, AC-26, AC-27, AC-28, AC-36, AC-38)
- [ ] 57. Existing 164 E2E tests and all unit tests pass unchanged (specs that find list rows already use search/filter). Test: full Playwright run against `docker compose up -d --build`
- [ ] 58. Requirement-ID traceability: every acceptance criterion AC-1..AC-38 has at least one test tagged `[Trait("Spec", ...)]` (backend) or named in the test title (web/E2E); list any gap. Test: a scripted check listing untagged ACs is attached to the PR
- [ ] 59. Update `spec.md` to **Implemented** with an "Implementation notes" section listing every deviation, and open the PR with the spec folder link and covered requirement IDs; move Jira issues per workflow.
