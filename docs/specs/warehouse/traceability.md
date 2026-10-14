# Warehouse: acceptance criteria and the tests that prove them

Tests are tagged with `[Trait("Spec", "<ID>")]` (.NET) or name the ID in the `describe`/`it` text (web). Backend tests are in
`src/api/tests/Romp.Modules.Warehouse.Tests/<Feature>/` (real PostgreSQL through Testcontainers); HTTP-level tests are in
`src/api/tests/Romp.Api.IntegrationTests/Warehouse/`; module-boundary and structure rules are in `Romp.ArchitectureTests`
(`WarehouseFeatureDependencyTests`); web tests sit beside the component in `src/web/projects/admin/src/app/features/warehouse/<feature>/`;
end-to-end specs are in `src/web/e2e/warehouse/`, and every screen is also covered by the layout suite `src/web/e2e/journeys/responsive-layout.e2e.spec.ts`.

Test-case IDs that came from the Jira tickets: **TC-PUT-01..05** (SCRUM-98) and **TC-STK-01..09** (SCRUM-79). The BRD itself has no `TC-*` cases for this area.

## Bins (`bins`)

| ID | Proven by |
|---|---|
| BIN-1 | `BinTests.TheCode_IsBuiltFromZoneAisleAndShelf_UpperCased`; `BinHandlerTests.ANewBin_GetsItsCode_IsListed_AndIsAttributable`; `BinEndpointsTests.ANewBin_IsCreated_FoundByItsPrintedCode_AndListed`; e2e `bins` "adding a bin shows its code" |
| BIN-2 | `BinHandlerTests.ASecondBinWithTheSameCode_IsRefused…`, `TenParallelCreatesOfTheSameCode_LeaveExactlyOneBin`; `BinEndpointsTests.ADuplicateCode_IsRefused_As409…`; e2e `bins` |
| BIN-3 | `BinTests.APartOutsideThePattern_IsRefused`; `BinHandlerTests.InvalidParts_AreRefused…`; `BinEndpointsTests.APartOutsideThePattern_Is400…`; `BinFormComponent` spec; e2e `bins` "outside the pattern" |
| BIN-4 | `BinTests.ABinWithStock_CannotBeDeactivated`, `AnEmptyBin_CanBeDeactivated…`; `BinHandlerTests.ABinWithStock_CannotBeDeactivated_ButAnEmptyOneCan…`; `StockQueriesAndEventsTests.TheRealBinStockQuery_…`; `BinTableComponent` spec; e2e `bins` deactivate |
| BIN-5 | `BinHandlerTests.TheReader_FindsBinsByExactCode…`; `BinEndpointsTests.AnUnknownCode_Is404…`; putaway and adjustment tests for inactive bins |
| BIN-6 | `BinHandlerTests.TheList_IsPaged_OrderedByAUniqueKey…`, `TheList_RunsAConstantNumberOfQueries…`, `TheListShowsUnitsOnHand…`; `BinTableComponent` spec; layout suite |
| BIN-7 | `BinHandlerTests.ANewBin_…IsAttributable`, `TheActingPerson_IsRequired…`; `BinEndpointsTests.WithoutTheActingPerson_Is400` |

## Stock ledger (`stock-ledger`)

| ID | Proven by |
|---|---|
| LED-1 | `StockDomainTests.AStockPost_NeedsAKnownGrade…`, `AMovement_MustBalance…`; `StockPostingTests.APosting_ChangesTheBalance_AppendsAMovementWithBeforeAndAfter…`, `TheDatabase_RefusesToUpdateOrDeleteALedgerRow` |
| LED-2 | `StockPostingTests.TheLedgerSum_AlwaysEqualsTheStoredBalances_AfterRandomMixedOperations`; `StockQueriesAndEventsTests.TheConsistencyCheck_ReportsNoMismatch…` |
| LED-3 | `StockPostingTests.ARemovalBeyondTheBalance_IsRefused…`, `RemovingFromABinThatNeverHeldTheVariant…`, `ARefusalLaterInTheSameTransaction_RollsBackEarlierChangesAndEvents`, `FiftyParallelRemovalsAgainstThirtyUnits…`, `TheDatabase_RefusesANegativeBalance…` |
| LED-4 | `StockPostingTests.ManyChangesToOneVariantInOneTransaction_PublishExactlyOneEvent…`; `WarehouseEventContractTests`; `WarehouseLatencyTests.ThePollInterval_…`, `AStockChange_IsDeliveredToItsConsumers…` (p95 under 5 s) |
| LED-5 | `StockQueriesAndEventsTests.StockPerVariant_ShowsOnHandByGrade…`, `AVariantThatHasNoStock_IsNotListed…`; `StockTableComponent` spec; `WarehouseEndpointsTests.AFoundStockAdjustment_…` |
| LED-6 | `StockQueriesAndEventsTests.StockPerVariant_…EveryBinItSitsIn`; `StockTableComponent` spec; e2e `stock-ledger` |
| LED-7 | `StockQueriesAndEventsTests.ABinsContents_ListEveryVariantAndGradeInIt` |
| LED-8 | `StockQueriesAndEventsTests.TheStockList_RunsAConstantNumberOfQueries…`, `ThePageSize_IsClampedToOneHundred`; layout suite |
| LED-9 | `StockQueriesAndEventsTests.TheHistory_IsNewestFirst_PagedAndFilterable…`; `MovementListComponent` spec; e2e `stock-ledger` |
| LED-10 | `StockPostingTests.ThePostingPort_TakesNoTotalsFromTheCaller…`; `WarehouseFeatureDependencyTests.TheStockRoutes_AreReadOnly…`; `WarehouseEndpointsTests.TheStockSurface_IsReadOnly_AnyWriteIsRefused` |

## Putaway (`putaway`)

| ID | Proven by |
|---|---|
| PUT-1 (TC-PUT-01) | `PutawayFlowTests.QcAcceptance_CreatesAPendingItem_ButNoStockIsAvailableUntilPutaway`; `PutawayItemTests.ANewItem_WaitsForItsGtin…`; `PutawayQueueComponent` spec |
| PUT-2 | `PutawayFlowTests.ARedeliveredMessage_DoesNotDoubleThePendingQuantity`, `AnItemCreatedBeforeTheGtinExists_…InEitherOrder`, `AnItemStillAwaitingItsGtin_CannotBePutAway`; `StockQueriesAndEventsTests.AssigningTheSameVariantTwice_…` |
| PUT-3 | `PutawayFlowTests.QcAcceptance_…NoStockIsAvailableUntilPutaway` |
| PUT-4 (TC-PUT-02) | `PutawayFlowTests.Confirming_MovesTheBalance_TheLedger_TheRecordAndTheEvents_InOneTransaction`; e2e `putaway` "scan the tag…" |
| PUT-5 | `PutawayFlowTests.PartialPutaway_AcrossTwoBins…`, `AnInvalidQuantity_IsA400StyleValidationError`; `PutawayItemTests` quantity rules; `PutawayConfirmFormComponent` spec |
| PUT-6 (TC-PUT-03) | `PutawayFlowTests.AWrongGtin_AnUnknownBin_AndAnInactiveBin_AreRefused…`, `TheScanLookup_KnowsTheBarcode…`; `PutawayItemTests.AGtin13_…`; `WarehouseEndpointsTests.TheScanLookup_Is400…`; e2e `putaway` "a barcode nobody knows" |
| PUT-7 | `PutawayFlowTests.ARetiredGtin_IsRefusedAtPutaway…`; `StockDomainTests.VoidingTheCurrentGtin_…`; `PutawayQueueComponent` and reducer specs |
| PUT-8 | `PutawayFlowTests.TheSameKeyTwice_AppliesOnce…` |
| PUT-9 | `PutawayFlowTests.TwentyParallelConfirmsOfOneUnit_AgainstTenPending_ApplyExactlyTen`; `PutawayItemTests` |
| PUT-10 | `PutawayFlowTests.Confirming_…` (record, movement and both events in one transaction); `WarehouseEventContractTests` |
| PUT-11 | `WarehouseLatencyTests.AStockChange_IsDeliveredToItsConsumers…` |
| PUT-12 | `PutawayFlowTests.OnlyReleasedOutcomes_ReachThePutawayQueue`, `AMessageWithNonPositiveIds_IsRefused`, `MalformedLines_AreRefused_BeforeAnythingIsStored`; `StockQueriesAndEventsTests.AMalformedVariantEvent_IsRefused…` |
| PUT-13 | `PutawayFlowTests.APutawayRecord_HasNoUpdatePath…` |
| PUT-14 | `PutawayFlowTests.TheQueueAndHistory_ArePaged_OldestFirst…`, `TheQueue_CanBeSearchedBySkuOrExactBarcode…`; component and reducer specs; layout suite |
| PUT-15 (TC-PUT-04) | `PutawayFlowTests.Confirming_…` (actor, time, scanned flag) |
| PUT-16 (TC-PUT-05) | `WarehouseFeatureDependencyTests.NoOtherModule_ReadsTheWarehouseSchema` |
| PUT-17 | `WarehouseLatencyTests.TheScanLookup_AnswersUnderThreeHundredMilliseconds…` (1,000 scans, p95) |
| PUT-18 | e2e `putaway` "on a slow 3G connection…" (one request per scan and per confirm, small payloads); `PutawayScanComponent` spec |
| PUT-19 | `PutawayFlowTests.AnItemChosenFromTheList_CanBePutAwayWithoutAGtinScan`; `PutawayConfirmFormComponent` and reducer specs; e2e `putaway` "chosen from the list" |
| PUT-20 | `PutawayFlowTests.ConcessionStock_IsShelvedInItsOwnGrade…`; `PutawayItemTests.TheGrade_ComesFromTheQcOutcome` |
| PUT-21 | `PutawayFlowTests.MoreThanIsPending_IsRefused_PointingAtAFoundStockAdjustment…`; `AdjustmentPolicyTests` (large change needs reconfirm, `FOUND_STOCK` not exempt) |

## Adjustments (`adjustments`)

| ID | Proven by |
|---|---|
| ADJ-0 (TC-STK-01) | `AdjustmentFlowTests.MinusThreeWithAReason_ReducesTheBin_AndTheMovementRecordsActorReasonAndBeforeAfter`; e2e `adjustments` "removing damaged units" |
| ADJ-1 (TC-STK-04) | `AdjustmentFlowTests.AnAdjustmentWithoutAReason_IsRefused…`; `AdjustmentFormComponent` spec; e2e `adjustments` "without a reason" |
| ADJ-2 | `AdjustmentFlowTests.TheReasonLookup_HasTheFourSeededCodes_InOrder`; `AdjustmentPolicyTests.TheReasonCodes_AreTheFourFromTheBrd` |
| ADJ-3 | `AdjustmentPolicyTests` direction tests; `AdjustmentFlowTests.AContradictoryDirection_IsRefused` |
| ADJ-4 (TC-STK-05) | `AdjustmentFlowTests.RemovingMoreThanTheBinHolds_IsRefused…`; `StockPostingTests.TheDatabase_RefusesANegativeBalance…`; e2e `adjustments` "more than the bin holds" |
| ADJ-4a (TC-STK-07) | `AdjustmentFlowTests.ThirtyMixedAdjustmentsAndPutaways_OnOneBin_LoseNoUpdate`; `StockPostingTests.FiftyParallelRemovals…` (order decrements arrive with Sprint 12) |
| ADJ-4b (TC-STK-09) | `AdjustmentPolicyTests` limit tests; `AdjustmentFlowTests.AChangeAboveTheLimit_NeedsTheReconfirmation_ThenApplies`, `TheLimit_IsConfiguration…`; `WarehouseEndpointsTests.ALargeAdjustment_Is409ConfirmationRequired…`; `AdjustmentFormComponent` and reducer specs; e2e `adjustments` "large change" |
| ADJ-4c (TC-STK-06) | `WarehouseFeatureDependencyTests.TheWritesThatChangeStockAreBehindTheInventoryPolicy`; `InventoryPolicyTests`; a real 403 test arrives with authentication in Sprint 8 |
| ADJ-5 | `AdjustmentFlowTests.TheRecord_TheMovement_AndTheEvent_AreWrittenInOneTransaction` |
| ADJ-6 | `AdjustmentPolicyTests.AnAdjustmentRecord_HasNoMutators`; `WarehouseFeatureDependencyTests.TheStockRoutes_AreReadOnly_AndNoAdjustmentRouteCanEditOrDelete`; `WarehouseEndpointsTests.AnAdjustment_CannotBeEditedOrDeleted…`; `AdjustmentTableComponent` spec |
| ADJ-7 | `AdjustmentFlowTests.ABadNote_ZeroChange_UnknownBin_UnknownGtin_AndInactiveBin_AreAllRefused`, `ABadActingPerson_IsRefused`; `AdjustmentPolicyTests.AZeroOrAbsurdChange_IsRefused` |
| ADJ-8 | `AdjustmentFlowTests.TheSameKeyTwice_AppliesOnce…`; `WarehouseEndpointsTests` (same key twice); `AdjustmentFormComponent` spec |
| ADJ-9 | `AdjustmentFlowTests.OnARetiredGtin_RemovingIsAllowed_ButAddingIsRefused` |
| ADJ-10 | `AdjustmentFlowTests.TheHistory_FiltersByReasonAndVariant_IsPagedNewestFirst_AndRunsAConstantNumberOfQueries`; `AdjustmentTableComponent` spec; layout suite |

## Cycle counts (`cycle-counts`)

| ID | Proven by |
|---|---|
| CC-1 | `CycleCountTests.ANewCount_IsOpen_WithOneEntryPerBin`; `CycleCountFlowTests.ACount_GetsABusinessNumber…`, `AnInactiveOrUnknownBin_CannotBeCounted`; e2e `cycle-counts` |
| CC-2 | `CycleCountFlowTests.TheBlindCount_NeverCarriesASystemQuantity`; `CountSheetComponent` spec; `WarehouseEndpointsTests.ACycleCount_RunsOverHttp_TheBlindReadHidesTheSystemQuantity…`; e2e `cycle-counts` |
| CC-3 | `CycleCountFlowTests.ABinInAnOpenCount_IsRefusedForASecondCount…`, `FiveParallelCountsOfTheSameBin_LeaveExactlyOneOpen`; `BinPickerComponent` spec; e2e `cycle-counts` "already in an open count" |
| CC-4 | `CycleCountFlowTests.Confirming_RaisesOneReasonedAdjustmentPerVariance…`, `AVarianceWithoutAReason_BlocksTheConfirmation…`, `ConfirmingBeforeEveryBinIsCounted_IsRefused`, `AnItemTheSystemHoldsButTheCounterNeverScanned…`; `ReviewTableComponent` and reducer specs |
| CC-5 | `CycleCountFlowTests.Confirming_…`, `ConfirmingTwice_RaisesTheAdjustmentsOnlyOnce`, `ALargeVariance_NeedsTheReconfirmationOnItsReason`; e2e `cycle-counts` "count a bin blind" |
| CC-6 | `CycleCountFlowTests.AVariantFoundWhereTheSystemExpectsNone_IsAPositiveAdjustment…`; `CycleCountTests.SettingABinsCounts_ReplacesItsEarlierLines` |
| CC-7 | `CycleCountFlowTests.StockThatMovesAfterTheCountOpened_FlagsTheLines_BlocksConfirmation…`; `CycleCountTests.ARecount_MovesTheBinsMark…`; `ReviewTableComponent` spec |
| CC-8 | `CycleCountFlowTests.AZeroVarianceLine_RaisesNoAdjustment…`; `ReviewTableComponent` spec |
| CC-9 | `CycleCountFlowTests.Cancelling_ChangesNoStock_AndFreesTheBins`; `CycleCountTests.CancellingAnOpenCount…`; e2e `cycle-counts` "cancelling" |
| CC-10 | `CycleCountFlowTests.AnUnknownBarcode_ARetiredOneCountedAsPresent_ABinOutsideTheCount_AndAnInvalidQuantity_AreRefused`; `CycleCountTests.ABinOutsideTheCount…`; `CountSheetComponent` spec |
| CC-11 | `CycleCountFlowTests.TheCountList_IsPagedNewestFirst_AndTheBinAgeListPutsNeverCountedBinsFirst`, `TheBinAgeList_CanBeNarrowedToOneZone…`; `CountListComponent` and `BinPickerComponent` specs; layout suite |
| CC-12 | `CycleCountFlowTests.Confirming_…` (creator, counter, confirmer, times; each variance an audited adjustment) |

## Stock alerts (`stock-alerts`)

| ID | Proven by |
|---|---|
| ALR-1 (TC-STK-02) | `AlertPolicyTests.FallingToOrBelowTheThreshold_OpensOneLowAlert`; `StockAlertFlowTests.CrossingTheThreshold_RaisesExactlyOneLowAlert…`; e2e `stock-alerts` |
| ALR-2 (TC-STK-03) | `AlertPolicyTests.ReachingZero_OpensStockOut_OrEscalates…`; `StockAlertFlowTests.ReachingZero_EscalatesTheSameAlertToStockOut…`; e2e `stock-alerts` "running out" (the storefront side arrives with Sprint 9) |
| ALR-3 (TC-STK-08) | `AlertPolicyTests.FurtherMovementsWhileLow…`, `RestockingAboveTheThreshold_ClosesTheAlert…`; `StockAlertFlowTests.Restocking_AboveTheThreshold_ClosesTheAlert_ByItself`, `ReplayingTheSameEvent_CreatesNothingNew…`; e2e `stock-alerts` |
| ALR-4 | `AlertPolicyTests.AVariantThatNeverHeldStock_RaisesNothing`; `StockAlertFlowTests.AVariantThatNeverHeldStock_RaisesNoAlert` |
| ALR-5 | `AlertPolicyTests.Acknowledging_RecordsWhoAndWhen…`; `StockAlertFlowTests.Acknowledging_RecordsWhoAndWhen…`, `AnUnknownOrClosedAlert_CannotBeAcknowledged…`; `AlertTableComponent` spec; e2e `stock-alerts` |
| ALR-6 | `AlertPolicyTests.AThresholdOfZero…`, `AThresholdOutsideTheRange_IsRefused`; `StockAlertFlowTests.ChangingTheThreshold_IsValidated_Audited_AndReevaluates…`, `TheConfiguredDefault_AppliesUntilAVariantHasItsOwn`; `WarehouseOptionsValidatorTests`; `ThresholdTableComponent` spec; e2e `stock-alerts` "lowering a threshold" |
| ALR-7 | `AlertPolicyTests.OpeningAndChangingLevel_RaiseTheAlertEvent…`; `StockAlertFlowTests.TheAlertEvent_IsWrittenInTheSameTransactionAsTheAlert` |
| ALR-8 | `StockAlertFlowTests.TheAlertAndThresholdLists_ArePaged_Filterable_AndRunAConstantNumberOfQueries`; `AlertTableComponent`, `ThresholdTableComponent`, `AlertBadgeComponent` specs; e2e `stock-alerts` (menu badge); layout suite |
| ALR-9 | `StockAlertFlowTests.TheStockPostingPath_DoesNotDependOnAlerts…` (alerts run from the outbox in their own transaction, retried and dead-lettered by the dispatcher) |

## Cross-cutting

| Requirement | Proven by |
|---|---|
| NFR-SC-01 (scan under 300 ms) | `WarehouseLatencyTests.TheScanLookup_AnswersUnderThreeHundredMilliseconds…` |
| NFR-SC-05 (attributable, timestamped) | audit assertions in `StockPostingTests`, `PutawayFlowTests`, `AdjustmentFlowTests`, `CycleCountFlowTests`; ledger immutability `StockPostingTests.TheDatabase_RefusesToUpdateOrDeleteALedgerRow` |
| NFR-SC-06 (stock reaches the storefront view within seconds) | `WarehouseLatencyTests` (poll interval and p95 commit-to-delivery under 5 s) |
| NFR-SC-07 (low-spec network) | e2e `putaway` slow-3G test; payload sizes |
| NFR-FT-08 (a notification failure never rolls back the business change) | alerts and events run from the outbox; `StockAlertFlowTests.TheStockPostingPath_DoesNotDependOnAlerts…` |
| NFR-17, NFR-19 (mobile-first, accessible) | component specs (labels, `th scope`, captions, roles); layout suite at seven widths |
| SEC-08 (roles on the server) | policies declared and architecture-tested; enforced for real in Sprint 8 |
| ADR 0011 (every endpoint secured) | `EndpointSecurityTests`; `WarehouseFeatureDependencyTests` |
