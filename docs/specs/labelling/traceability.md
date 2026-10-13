# Labelling: acceptance criteria and the tests that prove them

Tests are tagged with `[Trait("Spec", "<ID>")]` (.NET) or name the ID in the `describe`/`it` text (web). Backend tests are in
`src/api/tests/Romp.Modules.Labelling.Tests/<Feature>/`; module-boundary and structure rules are in `Romp.ArchitectureTests`; web tests sit
beside the component in `src/web/projects/admin/src/app/features/labelling/<feature>/`; end-to-end specs are in `src/web/e2e/labelling/`.

| ID | Proven by |
|---|---|
| LW-1 | `LabelWorkTests.AcceptedBatch_BecomesOneReadyWorkItemPerLine_WithItsOutcome`; `QcBatchAcceptedMapperTests.AValidMessage_IsMapped`; e2e `label-work` "accepted stock appears on the worklist" |
| LW-2 | `LabelWorkTests.TheSameMessageTwice_ChangesNothing_AndTakesNoGtinNumber` |
| LW-3 | `QcBatchAcceptedMapperTests.AnythingButAcceptedOrConcession_IsRefused`; `LabelWorkItemTests.FromQcCode_*` |
| LW-4 | `LabelWorkTests.APoWithNoStyle_IsParked…`, `ARetiredStyle_IsParkedUntilStaffConfirm…`, `AnUnknownColourCode_ParksOnlyThatLine…`; `WorklistTableComponent` spec |
| LW-5 | `LabelWorkTests.PriceChangedAfterTheTagsWereQueued_IsFlagged_UntilAReprintPrintsTheCurrentPrice`; `StyleLabelInfoTests.PriceChangedAt_MovesOnlyWhenThePriceChanges` |
| LW-6 | `QcBatchAcceptedMapperTests.MalformedMessages_AreRefused_BeforeAnythingIsStored`; `LabelWorkItemTests.New_RefusesANonPositiveQuantity` |
| LW-7 | `LabelWorkTests.TheWorklist_IsPaged_Filtered_AndRunsAConstantNumberOfQueries`; `WorklistTableComponent` spec; layout suite |
| LW-8 | `LabelWorkTests.AcceptedBatch_BecomesOneReadyWorkItemPerLine…` (actor and time on every row) |
| VAR-1 | `GtinTests`, `VariantTests.AssignGtin_GivesOneActiveGtin…`, `VariantAssignmentTests.AcceptedBatch_GivesEachVariantAValidGtinAndSku…` |
| VAR-2 | `VariantAssignmentTests.ALaterDeliveryOfTheSameVariant_ReusesTheGtin_AndTakesNoNewNumber` |
| VAR-3 | `VariantTests.AssignGtin_WhileOneIsActive_IsRefused`; `VariantAssignmentTests.TheDatabaseItself_RefusesASecondActiveGtinForOneVariant`, `TheSameVariantRacedByManyBatches…` |
| VAR-4 | `VariantAssignmentTests.ManyAssignmentsAtOnce_AllGetDistinctGtinsAndItemReferences` |
| VAR-5 | `VariantAssignmentTests.VoidingRetiresTheNumberForGood…` |
| VAR-6 | `VariantAssignmentTests.ARolledBackAssignment_ReleasesItsNumber_SoThereAreNoGaps` |
| VAR-7 | `VariantAssignmentTests.AcceptedBatch_GivesEachVariantAValidGtinAndSku…`; `ApparelCodeQueriesTests`; `VariantTests.Constructor_RejectsSkuCodesWithUnsafeCharacters…` |
| VAR-8 | `VariantAssignmentTests.VoidingRetiresTheNumberForGood…`, `Void_NeedsAReasonAndAPerson…`; `VoidFormComponent` spec; e2e `variants` "voiding a barcode retires it for good" |
| VAR-9 | `VariantTests.Void_RetiresTheGtin_KeepsTheRow_AndAllowsANewNumber`; `VariantAssignmentTests.VoidingRetiresTheNumberForGood…` |
| VAR-10 | `VariantTests.AssignGtin_GivesOneActiveGtin_AndRaisesTheAssignedEvent`; outbox assertions in `VariantAssignmentTests` |
| VAR-11 | `LabellingOptionsValidatorTests`; `GtinTests.CompanyPrefix_*` |
| VAR-12 | `GtinTests.Create_RefusesAReferenceBeyondThePrefixCapacity`; `VariantAssignmentTests.WhenTheRangeIsUsedUp_AssignmentFailsCleanly_AndTakesNothing`; e2e `variants` capacity banner |
| VAR-13 | `VariantAssignmentTests.TheRegister_IsPaged_Ordered_SearchableAndRunsAConstantNumberOfQueries`; `VariantTableComponent` spec |
| VAR-14 | `GtinTests.TryParse_RejectsAnythingThatIsNotAValidGtin13` |
| PJ-1 | `PrintWorkerTests.QueueingAJob_SavesItInstantly…`; `LabelWorkTests.QueueingTags_NeverExceedsTheAcceptedQuantity…`, `TwoClicksAtOnce_CannotBothPass…`, `ACancelledJob_GivesItsTagsBack` |
| PJ-2 | `PrintWorkerTests.AJammedPrinter_RetriesWithBackoff_EndsFailed…`; `PrintJobTests.RecordFailure_*`; e2e `print-jobs` "a jammed printer retries" |
| PJ-3 | `PrintWorkerTests.…StaffCanRetryThatSameJob`, `Cancel_WorksOnQueuedAndFailedJobs…`; `JobTableComponent` spec |
| PJ-4 | `PrintWorkerTests.AJammedPrinter_DoesNotStopTheOtherPrintersJobs`; e2e `print-jobs` "does not stop a working one" |
| PJ-5 | `PrintWorkerTests.AReprint_UsesTheSameGtin…`; e2e `print-jobs` "a lost tag is reprinted with the same barcode" |
| PJ-6 | `PrintWorkerTests.AReprint_UsesTheSameGtin…` (print history); `JobTableComponent` spec |
| PJ-7 | `PrintWorkerTests.Reprint_NeedsAReasonAPersonAndASaneCount`; `ReprintFormComponent` spec; e2e `print-jobs` "without a name is refused" |
| PJ-8 | `PrintWorkerTests.QueueingOrReprinting_ForARetiredGtin_IsRefused`, `AGtinVoidedAfterQueueing_IsNeverPrinted`; e2e `variants` "a voided barcode cannot be used" |
| PJ-9 | `PrintWorkerTests.EightWorkersRacingForOneJob_ExactlyOneWinsTheClaim`, `AJobStuckInPrinting_GoesBackToTheQueue`; `PrintJobTests.MarkPrinted_OnAJobThatIsNotPrinting_IsRefused` |
| PJ-10 | `PrintWorkerTests.APrintedJob_RecordsTheAttempt_FinishesTheLabelledCount_AndRaisesTheEvent`; `PrintJobTests.MarkPrinted_*` |
| PJ-11 | `PrintWorkerTests.APrinterQueueHasALimit`; validators for 1..5,000 tags in `PrintWorkerTests.Reprint_*` and `LabelWorkTests.QueueingTags_NeedsAReadyItem…` |
| PJ-12 | `LabelPrinterTests.*`; `PrintWorkerTests.APrinterIsChosenByConfiguredCodeOnly…`; `LabellingOptionsValidatorTests.FakePrinters_AreRefusedInProduction` |
| PJ-13 | `PrintWorkerTests.TheQueueScreen_IsPaged_FilteredByStatus…`; `JobTableComponent` spec; layout suite |
| TAG-1 | `TagRenderingTests.Zpl_ContainsTheRequiredContent…`; `PrintWorkerTests.TheTagBuilder_PutsTheCurrentCatalogPrice…` |
| TAG-2 | `TagRenderingTests.Ean13_RoundTrips_ThroughAnIndependentDecoder`, `Ean13_RefusesAGtinWithABadCheckDigit`, `Zpl_NeverRendersACorruptGtin` |
| TAG-3 | `TagRenderingTests.PrintedPrice_IsFormattedFromADecimal`; `StyleLabelInfoTests.GetLabelInfo_ReturnsCodeNameAndTargetPrice_AsDecimal` |
| TAG-4 | `TagRenderingTests.Zpl_ForConcessionStock_CarriesThe2ndMark_AndTheSamePrice`; `PrintWorkerTests.AReprint_…KeepsTheConcessionMark` |
| TAG-5 | `LabelWorkTests.PriceChangedAfterTheTagsWereQueued_IsFlagged_UntilAReprintPrintsTheCurrentPrice` |
| TAG-6 | `TagRenderingTests.Zpl_HostileProductNames_CannotInjectCommands`, `ZplEscaper_HexEscapesUtf8Bytes…`, `Pdf_TextIsEscapedAndAscii…` |
| TAG-7 | `TagRenderingTests.LongNames_AreShortenedDeterministically…`, `Truncation_NeverSplitsASurrogatePair` |
| TAG-8 | `TagRenderingTests.Pdf_IsAWellFormedOnePageDocument_WithTheSameTextAsTheZpl`; e2e `label-work` "the tag preview is a real PDF" |
| TAG-9 | `LabellingFeatureDependencyTests.TagBuilding_HasNoDependencyOnPrintersFilesOrTheNetwork` |

Cross-cutting (ADR 0011), not tied to one criterion: `EndpointSecurityTests` (every endpoint through `MapSecuredGroup`),
`SecurityBaselineTests` (deployment gate, headers, rate limits, policies), `LabellingFeatureDependencyTests` (one-way features, pure Domain),
`StructureSyncTests` (spec, API, web and e2e names agree), `NamingConventionTests` (every SKU table, column, key and index).
