# Published catalog: acceptance criteria and the tests that prove them

Tests are tagged with `[Trait("Spec", "<ID>")]`. Backend tests are in `src/api/tests/Romp.Modules.Storefront.Tests/PublishedCatalog/` (real PostgreSQL through
Testcontainers where a database matters). The producer side sits in the `Romp.Modules.Catalog.Tests` and `Romp.Modules.Warehouse.Tests` projects (version
counters, snapshot queries, event contracts). The HTTP and outbox journey is `Romp.Api.IntegrationTests/Storefront/`; module-boundary rules are in
`Romp.ArchitectureTests/StorefrontFeatureDependencyTests`. The BRD has no `TC-*` cases for this area, so the criteria are the spec's own `PCT-*` rows.

Each cell lists the test class, then its tests.

## PCT-1

- `ApplyProductSnapshotTests`: `Apply_NewSnapshot_MarksTheLowestSortImageAsMain_AndQueuesEveryWidthAndFormat`, `Apply_NewSnapshot_StoresTheProductAndEveryRowItOwns`, `Apply_Variants_TakeTheirSizeOrderFromTheSizeLookup`
- `ApplySizeChartTests`: `Apply_NewChart_StoresItWithItsLines_InSizeOrder`, `Apply_NewerChart_ReplacesItsLines`, `Apply_NullChart_RemovesItAndItsLines_AndUnknownIsAbsent`
- `EventConsumerTests`: `ProductPublished_ReadsTheSnapshotAndProjectsIt`, `SizeChartChanged_ProjectsTheChart_AndRemovesItWhenTheAnswerIsAnotherChart`
- `PublishedCatalogEndToEndTests`: `APublishedProduct_AppearsInTheReadModel_WithinFiveSeconds_AndLeavesWhenUnpublished`
- `PublishedProductQueriesTests`: `APublishedProduct_HasASnapshotWithEverythingThePageShows`
- `ReadModelMigrationTests`: `AProductWithEverythingAPageShows_RoundTripsWithItsExactValues`, `Migrate_CreatesTheReadModelTables_AndTheInbox_AndNoPendingModelChanges`, `Migrate_Lookups_AreSeededWithTheEnumIds_AndTheFormatCodesMatchTheDomain`
- `SizeChartQueriesTests`: `AChart_ComesWithItsCategoryGenderSizeNamesAndMeasurements`
- `StorefrontFeatureDependencyTests`: `NoOtherModule_ReadsTheStorefrontSchema`, `StorefrontApplicationAndDomain_ReadOtherModules_OnlyThroughTheirContracts`, `StorefrontSources_NeverQueryAnotherModulesSchema`

## PCT-2

- `ApplyProductSnapshotTests`: `Apply_NewerSnapshot_ReplacesContent_AndDropsVariantsAndImagesNoLongerPublished`, `Apply_NullSnapshot_RemovesTheProductAndEveryRowItOwns`, `Apply_NullSnapshotForAnUnknownProduct_IsAbsent`, `Apply_RemovedThenPublishedAgain_ComesBackWithItsRows`
- `EventConsumerTests`: `ProductUnpublishedOrArchived_RemovesTheProduct`
- `PublishedProductQueriesTests`: `ADraftAnUnpublishedAndAnArchivedProduct_HaveNoSnapshot`
- `ReadModelMigrationTests`: `DeletingAProduct_RemovesEveryRowThatBelongsToIt`

## PCT-3

- `ApplyAvailabilityTests`: `Apply_SkuCaseDiffers_StillFindsTheVariant`, `Apply_StockDroppingToZero_MakesTheProductUnavailable`, `Apply_StockForAPublishedVariant_MakesTheProductAvailableAndPriced`
- `ApplyProductSnapshotTests`: `Apply_NewerSnapshot_KeepsTheStockAlreadyAppliedToAVariant`, `Apply_StockHeldBeforeTheProductArrived_IsPickedUpBySku`
- `EventConsumerTests`: `StockEvents_ReadWarehousesCurrentFigure_AndUpdateTheProduct`
- `PublicResponseTests`: `APublicResponse_CarriesNoExactStockQuantity`, `EveryPublicResponse_ImplementsNoAdminOrCommandMarker`, `TheGuard_AllowsAnAvailabilityClass`, `TheGuard_CatchesAQuantity_OnTheTypeAndNested`
- `PublishedCatalogRuleTests`: `ANegativeThreshold_IsRefused`, `AProductWithAnInStockVariantAndAReadyImage_IsListable`, `AProductWithNothingToBuy_IsNotInStock_AndNotListable`, `AThresholdOfZero_MeansThereIsNoLowStockBand`, `TheClassFollowsTheQuantityAndTheThreshold`
- `StockAvailabilityTests`: `TheFirstPosting_GivesVersionOne_AndEveryLaterPostingAddsOne`
- `StorefrontOptionsValidatorTests`: `ALowStockThreshold_OutsideTheRange_IsRefused`, `AThresholdOfZero_IsAllowed`, `TheDefaults_AreValid`

## PCT-4

- `ApplyProductSnapshotTests`: `Apply_CompareAtAbovePrice_MarksTheProductOnSaleWithTheBiggestSaving`
- `PublishedCatalogRuleTests`: `ACompareAtThatIsNotAbovePrice_IsNotASale_AndTheSavingIsRoundedToTwoPlaces`, `AnInStockVariantWithACompareAtAbovePrice_PutsTheProductOnSale_WithTheBiggestSaving`, `ASaleOnlyOnASoldOutVariant_DoesNotPutAnInStockProductOnSale`, `NoPricedVariant_MeansNoSummary`, `TheRange_IsTheLowestAndHighestPriceOfTheVariantsThatCanBeBought`, `WhenEverythingIsSoldOut_ThePageStillShowsThePricesOfTheVariantsThatExist`

## PCT-5

- `ApplyAvailabilityTests`: `Apply_SameOrOlderStock_ChangesNothing`, `Apply_StockBeforeTheProduct_IsHeld_ThenAppliedWhenTheProductArrives`
- `ApplyProductSnapshotTests`: `Apply_OlderSnapshotAfterNewer_DoesNotRegressTheRow`, `Apply_SameSnapshotTwice_ChangesNothing`, `Apply_SnapshotWithoutAVersion_IsRefused`
- `ApplySizeChartTests`: `Apply_ChartRecreatedForTheSameSlot_ReplacesTheOldOne_WhicheverArrivesFirst`, `Apply_ChartWithoutAVersion_IsRefused`, `Apply_SameOrOlderChart_ChangesNothing`
- `CatalogEventContractTests`: `ArchivingAPublishedProduct_GivesBothItsEventsTheSameVersion`, `EveryProductEvent_CarriesTheVersionItWasRaisedAt`, `TheVersionSurvivesTheRoundTripThroughJson`
- `EventConsumerTests`: `OldEventDeliveredLate_ProjectsTheCurrentStateNotTheEventsMoment`, `SameMessageDeliveredTenTimes_GivesOneState_AndReadsTheProducerOnce`
- `ProductContentVersionTests`: `ANewProduct_StartsAtVersionOne`, `ArchivingAPublishedProduct_BumpsOnce_EvenThoughItRaisesTwoEvents`, `NoteChanged_BumpsOnlyWhenItRaisesAnEvent`, `Publish_BumpsOnce_AndPublishingAgainChangesNothing`, `TheVersionIsSavedWithThePublish_AndWithEveryLaterChangeToAPublishedProduct`, `Unpublish_Bumps`
- `PublishedCatalogRuleTests`: `ASnapshotReplacesTheStoredOne_OnlyWhenItIsNewer`, `AVersionBelowOne_NeverReplacesAnything`
- `PublishedProductQueriesTests`: `TheSnapshotVersion_IsTheProductsContentVersion_AndMovesWithEveryChange`
- `ReconcileTests`: `Reconcile_Charts_AreAddedAndRemoved`, `Reconcile_LostStockEvent_IsAppliedToTheProduct`, `Reconcile_ManyPages_VisitsEveryProduct`, `Reconcile_NothingToRepair_ChangesNothing`, `Reconcile_ProductCatalogNoLongerPublishes_IsRemoved_WhereverItSitsInThePaging`, `Reconcile_ProductWhoseEventWasLost_IsFetchedAndProjected`, `Reconcile_StoredProductOlderThanCatalogs_IsRefreshed_AndAnUpToDateOneIsLeftAlone`
- `SizeChartQueriesTests`: `TheChartVersion_MovesWhenTheChartIsEdited`
- `SizeChartVersionTests`: `AChartDeletedAndCreatedAgainForTheSameCategoryAndGender_GetsAHigherVersionThanTheOneRemoved`, `ANewChart_GetsAVersion_AndEveryEditGivesItAHigherOne`, `AVersionMustGoUp`, `TheChangedEvent_CarriesTheChartsVersion`, `TwoCharts_NeverShareAVersion`
- `StockAvailabilityTests`: `APayloadWrittenBeforeTheVersionExisted_StillReads_AsVersionZero`, `ARefusedPosting_LeavesTheVersionWhereItWas`, `ManyChangesToOneVariantInOnePosting_AddExactlyOne`, `TheFirstPosting_GivesVersionOne_AndEveryLaterPostingAddsOne`, `TheLevelEvent_CarriesTheSameVersionTheQueryReports`, `TwentyParallelPostingsToOneVariant_GetTwentyDistinctVersions_AndNoQuantityIsLost`
- `StockDomainTests`: `PublishingAPutaway_CarriesTheSameStockVersionAsTheLevelEvent`
- `StorefrontOptionsValidatorTests`: `AZeroReconcileInterval_IsRefused`

## PCT-6

- `ApplyProductSnapshotTests`: `Apply_SameVersionInARebuild_IsRecomputedAndStampedWithTheRun`
- `PublishedProductQueriesTests`: `ListVersions_AndListSnapshots_PagePublishedProductsByIdWithoutRepeatsOrGaps`
- `ReadModelMigrationTests`: `TheModelMetaRow_IsSeededAtVersionZero_AndIsTheOnlyRowTheDatabaseAllows`
- `RebuildTests`: `FailRebuild_MarksTheRunFailed_AndAFinishedRunCannotBeRebuiltAgain`, `GetRun_Unknown_IsNull`, `OperatorVerb_RebuildsInline_AsOperatorCli`, `Rebuild_EmptyModel_IsFilledFromTheContracts_AndTheRunAndVersionAreRecorded`, `Rebuild_KeepsAProductAnEventPublishedAfterTheWalkStarted`, `Rebuild_OldRowsKeepServing_WhileTheRunWalksTheCatalogue`, `Rebuild_RecomputesRowsEvenAtTheSameVersion`, `Rebuild_SweepsProductsCatalogNoLongerPublishes`, `Start_WhileARunIsRunning_IsRefused`
- `ReconcileTests`: `Reconcile_LookupsAlreadyCurrent_ChangeNoCopies`, `Reconcile_RenamedLookups_ReachTheRows_AndTheRefreshTimeIsStored`
- `SizeChartQueriesTests`: `ListCharts_PagesByIdWithoutRepeatsOrGaps_AndCostsTheSameQueriesPerPage`
- `StockAvailabilityTests`: `ListAvailability_PagesByVariantIdWithoutRepeatsOrGaps`
- `StorefrontRebuildEndpointsTests`: `GetRebuild_UnknownRun_Is404`, `PostRebuild_Answers202WithTheRunsAddress_AndTheRunCompletesInTheBackground`

## PCT-7

- `ApplyProductSnapshotTests`: `Apply_ChangedOriginal_StartsItsDerivativesOver_ButAnUnchangedOneKeepsThem`
- `DerivativeHealthCheckTests`: `Check_FailedDerivatives_AreCountedAndDegradeButDoNotFail`, `Check_NoFailedDerivatives_IsHealthy`
- `LocalFileStorageNamedWriteTests`: `SaveNamed_KeyThatIsNotABareFileName_IsRefused`, `SaveNamed_WritesUnderTheGivenKey_ReplacesIt_AndLeavesNoTemporaryFile`
- `ProcessDerivativeTests`: `Process_ImageAboveThePixelCap_FailsTheRowForGood_WithTheRejectionCode`, `Process_NothingPending_ReportsNothingDue`, `Process_OriginalMissingFromStorage_RecordsOriginalMissing`, `Process_PendingDerivatives_WritesEachUnderItsHashedName_AndMarksTheProductImageReady`, `Process_StorageWriteFails_RetriesWithBackoff_ThenFailsAfterTheLastAttempt`
- `PublishedCatalogRuleTests`: `AHashThatIsNotOurs_IsRefused`, `ANameThatWasMade_CanBeReadBack`, `AnInStockProduct_WhoseMainImageIsStillBeingMade_WaitsOffTheListings`, `AnythingThatIsNotAGeneratedName_DoesNotParse`, `AWidthOutsideOneToEightThousand_IsRefused`, `TheName_IsHashWidthAndExtension`
- `ReadModelMigrationTests`: `OneImageHasAtMostOneDerivative_PerWidthAndFormat`
- `SkiaImageResizerTests`: `Probe_AnImageTypeTheStorefrontDoesNotProcess_IsRejected`, `Probe_BytesThatAreNotAnImage_AreRejected`, `Probe_ImageAboveThePixelCap_IsRejectedBeforeDecoding`, `Probe_ReadsTheDimensionsFromTheHeader`, `Resize_ImageAboveThePixelCap_IsRejected`, `Resize_NeverEnlargesPastTheOriginal`, `Resize_OutputOfAnImageWithMetadata_CarriesNoneOfIt`, `Resize_ScalesToTheWidthKeepingProportions_InTheRequestedFormat`, `Resize_TransparentPng_BecomesWhiteInJpeg`
- `StorefrontOptionsValidatorTests`: `AnAttemptCap_OutsideTheRange_IsRefused`, `ANegativeRetryDelay_IsRefused`, `AnImageQuality_OutsideTheRange_IsRefused`, `AnImageWidth_OutsideTheRange_IsRefused`, `APixelCap_OutsideTheRange_IsRefused`, `ARepeatedImageWidth_IsRefused`, `AZeroCacheLifetime_IsRefused`, `AZeroPollInterval_IsRefused`, `NoImageWidths_IsRefused`, `SeveralBadSettings_AreAllReported`, `TheDefaults_AreValid`, `TooManyImageWidths_IsRefused`

## PCT-8

- `ApplyProductSnapshotTests`: `Apply_ProductWithManyVariants_UsesAConstantNumberOfQueries`
- `PublishedProductQueriesTests`: `ASnapshotPage_CostsTheSameNumberOfQueries_HoweverManyProductsItHolds`

## PCT-9

- `ApplyAvailabilityTests`: `Apply_NullReport_LeavesTheVariantNotYetAvailable`
- `ApplyProductSnapshotTests`: `Apply_NoStockEverReceived_IsNotAvailable_AndTheVariantHasNoQuantity`
- `EventConsumerTests`: `StockEventForAVariantWarehouseHasNoRowFor_ChangesNothing`
- `PublishedCatalogRuleTests`: `AVariantWithNoStockRecord_OrThatIsNotSellable_IsNotYetAvailable_NotAFailure`
- `StockAvailabilityTests`: `AVariantThatNeverHadStock_IsNeitherFoundNorListed`

## PCT-10

- `ProcessDerivativeTests`: `Process_ChangedOriginal_GetsNewHashedNames_AndLeavesTheOldFilesAlone`
- `PublishedCatalogRuleTests`: `AChangedImage_GetsNewNames_SoTheOldOnesCanBeRemovedLater`, `TheHash_IsThirtyTwoLowercaseHexCharacters_AndFollowsTheContent`
- `SweepDerivativesTests`: `Sweep_NothingOldEnough_RemovesNothing`, `Sweep_UnreferencedFileOlderThanTheLifetime_IsRemoved_ReferencedAndYoungOnesStay`

## PCT-11

- `ApplyAvailabilityTests`: `Apply_StockForAnUnsellableVariant_StaysNotYetAvailable`
- `PublishedCatalogRuleTests`: `OnlyInStockAndLowStock_CanBeBought`

## Not yet proven

- **PCT-8** is proven for the producers (Catalog snapshot queries) and the projection (`ApplyProductSnapshotTests`). The storefront's own product and listing *reads* do not exist yet, so their constant-query-count tests (tasks.md 23) land with the first read in [listings](../listings/spec.md) and again in product-pages (S9b), reusing `Support/QueryCounter`.
- **PCT-2** (page answers 404 / 410) and **PCT-4** (no cached copy of the old price survives) are proven at the read-model level: the product leaves the model, the price is replaced. The HTTP status and cache-invalidation halves belong to product-pages and seo.
- **PCT-11** is proven as a read-model fact (availability is derived from the last stock event). The "never presented as a reservation" wording is a page-copy rule for product-pages.
- A Playwright spec was not written: no storefront page reads the model yet.
