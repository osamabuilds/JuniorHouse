# Catalog: acceptance criteria and the tests that prove them

Tests are tagged with `[Trait("Spec", "<ID>")]` (.NET) or name the ID in the `describe`/`it` text (web). Backend tests are in
`src/api/tests/Romp.Modules.Catalog.Tests/<Feature>/` (real PostgreSQL through Testcontainers); HTTP-level tests are in
`src/api/tests/Romp.Api.IntegrationTests/Catalog/`; module-boundary and structure rules are in `Romp.ArchitectureTests`
(`CatalogFeatureDependencyTests`); web tests sit beside the component in `src/web/projects/admin/src/app/features/catalog/<feature>/`;
end-to-end specs are in `src/web/e2e/catalog/`, and every screen is also covered by the layout suite
`src/web/e2e/journeys/responsive-layout.e2e.spec.ts`.

Requirement IDs from the BRD: **FR-53** (products, variants, pricing, media, size chart, care), **FR-08** (size chart), **FR-09** (composition and care),
**SEO-02** (clean URLs), **SEO-07** (image alt text). The BRD's `TC-PDP-01` (size chart) and `TC-PDP-18..21` (composition and care) are covered by CHT-1 and CMP-1..4.

## Requirement IDs

| ID | Proven by |
|---|---|
| FR-53 | PRD-1..15 (create, edit, price, media, size chart, care, publish); e2e `products` "a complete product is published, taken off sale and archived" |
| FR-08 | CHT-1..8; `TC-PDP-01` is CHT-1 |
| FR-09 | CMP-1..9; `TC-PDP-18..21` are CMP-1..4 |
| SEO-07 | MED-1..6 (alt text required at publish and enforced by the database); e2e `product-media` |
| SEO-02 | URL-1, URL-10 (`ProductPathTests`); e2e `urls` |

## Products (`products`)

| ID | Proven by |
|---|---|
| PRD-1 | `CatalogEndpointsTests.ANewProduct_ListsItsPlannedVariants_AndTheOneLabelledVariantIsSellable`; `ProductCommandTests.CreatingAProduct_ListsSixPlannedVariants_AndOnlyLabelledOnesShowSkuAndGtin`; `ProductCommandTests.APlannedVariant_StartsAtTheStylesTargetRetailPrice`; `ProductCommandTests.ASecondProductForAStyle_IsRefused_NamingTheExistingOne`; and 7 more; web: `product-table`, `product-create-form`, `variants-table`, `publish-panel` and `products.reducer` specs; e2e `products` |
| PRD-2 | `CatalogEndpointsTests.ACompareAtPriceNotAbovePrice_Is400`; `PlannedVariantPriceTests.CompareAtPrice_MustBeHigherThanThePrice`; `ProductCommandTests.ACompareAtPriceNotAboveThePrice_IsRefusedWithAMessage`; `ProductTests.SetPrices_RefusesAVariantThatIsNotTheProducts`; web: `product-table`, `product-create-form`, `variants-table`, `publish-panel` and `products.reducer` specs; e2e `products` |
| PRD-3 | `ProductImageCommandTests.ChangingAnImageOfAPublishedProduct_TellsTheStorefront`; `ProductCommandTests.Unpublish_TakesTheProductOffSale_KeepsItsData_AndWritesTheEvent`; `ProductCommandTests.ChangesToAPublishedProduct_TellTheStorefront_WithoutAnyPrice`; `ProductTests.Unpublish_TakesAPublishedProductOffSale_AndRaisesTheEvent`; web: `product-table`, `product-create-form`, `variants-table`, `publish-panel` and `products.reducer` specs; e2e `products` |
| PRD-4 | `CatalogEndpointsTests.TheWholeJourney_CreateFill_PublishRefusedThenAccepted_RenameAndResolve`; `ProductCommandTests.PublishingAnIncompleteProduct_NamesEveryMissingItem_AndChangesNothing`; `PublishGateTests.ACompleteProduct_Passes_WithNoWarnings`; `PublishGateTests.EveryMissingItem_IsNamed_AtOnce`; and 1 more; web: `product-table`, `product-create-form`, `variants-table`, `publish-panel` and `products.reducer` specs; e2e `products` |
| PRD-5 | `CatalogEndpointsTests.AStaleVersion_Is409_WithProblemDetails`; `ProductCommandTests.TheSecondEditorWithAStaleVersion_IsRefused`; `ProductCommandTests.AChangeToTheChildren_MovesTheVersion_SoAnotherEditorSeesAConflict`; `ProductCommandTests.APublishWithAStaleVersion_IsRefused`; and 1 more; web: `product-table`, `product-create-form`, `variants-table`, `publish-panel` and `products.reducer` specs; e2e `products` |
| PRD-6 | `ProductCommandTests.Archive_IsFinal_NeverDeletes_AndWritesTheEvent`; `ProductTests.Archive_IsFinal_AndNothingCanBeEditedAfterwards`; web: `product-table`, `product-create-form`, `variants-table`, `publish-panel` and `products.reducer` specs; e2e `products` |
| PRD-7 | `CatalogFeatureDependencyTests.PriceHistory_HasNoUpdateOrDeletePath_InTheApplication`; `PlannedVariantPriceTests.AChangedPrice_ReturnsOneHistoryRow_WithOldNewAndWho`; `PlannedVariantPriceTests.SettingTheSamePriceAgain_RecordsNothing`; `ProductCommandTests.APriceChange_RecordsTheOldAndNewPrice_WhoAndWhen`; and 1 more; web: `product-table`, `product-create-form`, `variants-table`, `publish-panel` and `products.reducer` specs; e2e `products` |
| PRD-8 | `PlannedVariantPriceTests.Price_MustBePositiveAndWithinTheLimit`; `PlannedVariantPriceTests.Price_IsRoundedToTwoDecimals_AsMoney`; `PlannedVariantPriceTests.ANewProduct_IgnoresAnUnusableDefaultPrice`; `ProductCommandTests.ANonPositivePrice_IsRefused`; and 3 more; web: `product-table`, `product-create-form`, `variants-table`, `publish-panel` and `products.reducer` specs; e2e `products` |
| PRD-10 | `CatalogEndpointsTests.TheWholeJourney_CreateFill_PublishRefusedThenAccepted_RenameAndResolve`; `ProductCommandTests.APartlyLabelledProduct_Publishes_WithTheOthersNotYetAvailable_AndTheEventIsWritten`; `ProductCommandTests.PublishingTwice_IsIdempotent_AndWritesOneEvent`; `ProductCommandTests.Unpublish_TakesTheProductOffSale_KeepsItsData_AndWritesTheEvent`; and 2 more; web: `product-table`, `product-create-form`, `variants-table`, `publish-panel` and `products.reducer` specs; e2e `products` |
| PRD-11 | `CatalogEndpointsTests.TheList_IsPaged_AndClamped`; `ProductCommandTests.TheList_IsPaged_Ordered_Clamped_AndRunsAConstantNumberOfQueries`; `ProductCommandTests.TheList_FiltersByStatusCategoryAndSearch_AndCountsSellableVariants`; web: `product-table`, `product-create-form`, `variants-table`, `publish-panel` and `products.reducer` specs; e2e `products` |
| PRD-12 | `ProductCommandTests.ACollectionPlacement_IsKept_AndUnknownCollectionsAreRefused`; `ProductTests.SetCollections_ReplacesTheSet_WithoutDuplicates`; web: `product-table`, `product-create-form`, `variants-table`, `publish-panel` and `products.reducer` specs; e2e `products` |
| PRD-13 | `LabellingVariantConsumerTests.AVariantBecomesSelectable_AsSoonAsItsGtinEventArrives_WithNoManualStep`; `LabellingVariantConsumerTests.AVariantOfAPublishedProductBecomingSellable_TellsTheStorefront`; `ProductCommandTests.APartlyLabelledProduct_Publishes_WithTheOthersNotYetAvailable_AndTheEventIsWritten`; `PublishGateTests.SomeVariantsStillWaitingForAGtin_DoNotBlockThePublish`; and 1 more; web: `product-table`, `product-create-form`, `variants-table`, `publish-panel` and `products.reducer` specs; e2e `products` |
| PRD-14 | `LabellingVariantConsumerTests.AVoidedGtin_StopsTheVariantBeingSellable`; `LabellingVariantConsumerTests.AVoidedGtinThatLabellingReplaced_IsSellableAgain`; `SellableVariantsTests.AVoidedGtin_MakesTheVariantNotSellable`; `SellableVariantsTests.AVoidedGtinThatWasReplaced_IsSellableAgainWithTheNewSku`; and 1 more; web: `product-table`, `product-create-form`, `variants-table`, `publish-panel` and `products.reducer` specs; e2e `products` |
| PRD-15 | `LabellingVariantConsumerTests.TheSameMessageDeliveredTwice_ChangesNothingTheSecondTime`; `LabellingVariantConsumerTests.EventsInTheWrongOrder_GiveTheSameResultAsInOrder`; `LabellingVariantConsumerTests.RefreshFromLabelling_GivesTheSameRowsAsTheEvents_AndRunningItTwiceChangesNothing`; `SellableVariantsTests.TheOrderOfTheLabelledRows_DoesNotChangeTheResult`; web: `product-table`, `product-create-form`, `variants-table`, `publish-panel` and `products.reducer` specs; e2e `products` |
| PRD-9 | `CataloguePolicyTests` (the policy exists under its own name and admits the catalogue role); `CatalogFeatureDependencyTests.EveryCatalogueEndpointGroup_IsBehindTheCataloguePolicy`; `EndpointSecurityTests` (no unsecured endpoint). Enforced with authentication in S8. |

## Product media (`product-media`)

| ID | Proven by |
|---|---|
| MED-1 | `ProductImageCommandTests.APublishWithAnImageLackingAltText_NamesThatImage`; `PublishGateTests.EachImageWithoutAltText_IsNamed`; web: `images-panel` spec; e2e `product-media` |
| MED-2 | `AltTextTests.EmptyOrWhitespace_IsRefused`; `AltTextTests.AFileName_IsNotADescription`; `AltTextTests.RepeatedKeywords_AreRefused`; `AltTextTests.OnlyDigitsOrPunctuation_IsRefused`; and 1 more; web: `images-panel` spec; e2e `product-media` |
| MED-3 | `AltTextTests.MoreThan125Characters_IsRefused`; `ProductSchemaTests.AltTextOver125Characters_IsRefusedByTheDatabase`; web: `images-panel` spec; e2e `product-media` |
| MED-4 | `AltTextTests.RealDescriptions_AreAccepted`; `AltTextTests.MarkupCharacters_AreRefused_SoItIsPlainText`; `ProductImageCommandTests.TheAnswerCarriesASuggestedAltText_ButStoresNone`; web: `images-panel` spec; e2e `product-media` |
| MED-5 | `ProductImageCommandTests.OnAPublishedProduct_AnUploadNeedsAltText_AndAnAltCannotBeCleared`; `ProductSchemaTests.APublishedProductsImage_CannotLoseItsAltText_ByAnyRoute`; `ProductSchemaTests.AProductCannotMoveToPublished_WhileAnImageHasNoAltText`; web: `images-panel` spec; e2e `product-media` |
| MED-6 | `ProductImageCommandTests.AProductWithNoImage_CannotBePublished`; web: `images-panel` spec; e2e `product-media` |
| MED-7 | `CatalogEndpointsTests.ANonImageUpload_Is400_AndAnUnknownProduct_Is404`; `ImageSnifferTests.APng_GivesItsTypeAndDimensions`; `ImageSnifferTests.AFileThatIsNotAnAcceptedImage_IsRefused_WhateverItIsCalled`; `ImageSnifferTests.ATruncatedHeader_IsRefused`; and 6 more; web: `images-panel` spec; e2e `product-media` |
| MED-8 | `ProductImageCommandTests.Reorder_ChangesTheOrderReturned_AndTheFirstIsTheMainImage`; `ProductImageCommandTests.Reorder_MustListExactlyTheProductsImages`; web: `images-panel` spec; e2e `product-media` |
| MED-9 | `ProductImageCommandTests.AnUpload_StoresTheOriginal_WithDimensions_AndAGeneratedKey`; `ProductImageCommandTests.AStorageFailure_LeavesNoDatabaseRow`; `ProductImageCommandTests.TheFile_IsServedWithOurRecordOfItsType`; web: `images-panel` spec; e2e `product-media` |
| MED-10 | `ImageSnifferTests.APng_GivesItsTypeAndDimensions`; `ImageSnifferTests.AJpeg_GivesItsTypeAndDimensions_FromTheStartOfFrameMarker`; `ImageSnifferTests.ALosslessWebp_GivesItsTypeAndDimensions`; `ImageSnifferTests.ALossyWebp_GivesItsDimensions`; and 2 more; web: `images-panel` spec; e2e `product-media` |

## Size charts (`size-charts`)

| ID | Proven by |
|---|---|
| CHT-1 | `CatalogEndpointsTests.ASizeChart_IsCreatedOverHttp_AndAppliesToAProductOfThatCategoryAndGender`; `SizeChartCommandTests.ACreatedChart_ReturnsOneRowPerSize_WithNamesInSizeOrder`; `SizeChartCommandTests.TheProductsChart_IsLimitedToItsSizeRun_AndFallsBackToUnisex`; `SizeChartTests.TheChartForAProduct_IsItsGenderThenTheCategorysUnisexChart`; web: `size-chart-editor`, `size-chart-view` and `size-charts.reducer` specs; e2e `size-charts` |
| CHT-2 | `SizeChartCommandTests.CreatingAndEditingAChart_WritesTheChangedEvent_InTheSameTransaction`; `SizeChartCommandTests.AStaleChartEdit_IsRefused`; `SizeChartTests.UpdatingAChart_ReplacesItsLines`; `SizeChartTests.NoteChanged_RaisesTheChartChangedEvent`; and 1 more; web: `size-chart-editor`, `size-chart-view` and `size-charts.reducer` specs; e2e `size-charts` |
| CHT-3 | `PublishGateTests.AMissingSizeChart_IsAWarning_NeverAFailure`; `SizeChartCommandTests.DeletingAChart_IsAllowed_ProductsThenReportNoChart_AndTheEventIsWritten`; `SizeChartCommandTests.APublishWithoutAChart_IsAllowed_AndReportsNoSizeChart`; `SizeChartTests.WithoutAnyChartForTheCategory_ThereIsNone`; web: `size-chart-editor`, `size-chart-view` and `size-charts.reducer` specs; e2e `size-charts` |
| CHT-4 | `ProductSchemaTests.ChartLines_RefuseMinAboveMax_NegativeValues_AndTheSameSizeTwice`; `SizeChartCommandTests.ARowWithMinAboveMax_OrNegative_OrNothing_IsRefused`; `SizeChartTests.AnAgeMinimumAboveItsMaximum_IsRefused`; `SizeChartTests.AHeightOrWeightMinimumAboveItsMaximum_IsRefused`; and 2 more; web: `size-chart-editor`, `size-chart-view` and `size-charts.reducer` specs; e2e `size-charts` |
| CHT-5 | `ProductSchemaTests.ChartLines_RefuseMinAboveMax_NegativeValues_AndTheSameSizeTwice`; `ProductSchemaTests.OnlyOneChartPerCategoryAndGender`; `SizeChartCommandTests.TwoRowsForOneSize_AndASecondChartForTheSameCategoryAndGender_AreRefused`; `SizeChartTests.TwoLinesForTheSameSize_AreRefused`; web: `size-chart-editor`, `size-chart-view` and `size-charts.reducer` specs; e2e `size-charts` |
| CHT-8 | `SizeChartCommandTests.TheList_IsPaged_Ordered_AndConstantInQueries`; web: `size-chart-editor`, `size-chart-view` and `size-charts.reducer` specs; e2e `size-charts` |

## Composition and care (`composition-care`)

| ID | Proven by |
|---|---|
| CMP-1 | `CompositionCareCommandTests.Composition_CanBeSavedIncomplete_ButIsFlaggedUntilItTotalsExactly100`; `CompositionCareCommandTests.AFinalCompositionOf95Percent_BlocksThePublishWithTheActualTotal`; `CompositionTests.ATotalOtherThanExactly100_IsIncomplete`; `CompositionTests.NoRowsAtAll_IsIncomplete`; and 1 more; web: `composition-care-panel` spec; e2e `composition-care` |
| CMP-2 | `CompositionCareCommandTests.UnknownCareInstructions_AndHtmlInTheNote_AreRefused`; web: `composition-care-panel` spec; e2e `composition-care` |
| CMP-3 | `CompositionCareCommandTests.GetCompositionAndCare_ReturnsRowsTotalCareAndNote`; web: `composition-care-panel` spec; e2e `composition-care` |
| CMP-4 | `CompositionCareCommandTests.EveryCareInstruction_HasATextNameAndASymbolKey`; web: `composition-care-panel` spec; e2e `composition-care` |
| CMP-5 | `CompositionCareCommandTests.ACompositionEditBySecondEditor_WithAStaleVersion_IsRefused`; `CompositionCareCommandTests.ChangingCompositionOrCareOfAPublishedProduct_WritesTheChangedEvent`; web: `composition-care-panel` spec; e2e `composition-care` |
| CMP-6 | `CompositionCareCommandTests.BadRows_AreRefusedWithAMessage`; `CompositionTests.TheSameFibreTwice_IsRefused`; `CompositionTests.APercentageOutsideZeroToOneHundred_IsRefused`; `ProductSchemaTests.Composition_RefusesPercentagesOutOfRange_AndTheSameFibreTwice`; web: `composition-care-panel` spec; e2e `composition-care` |
| CMP-7 | `CompositionCareCommandTests.ThirdsWithOneDecimal_TotalExactlyOneHundred`; `CompositionTests.ThreeSharesOfThirtyThreePointThreeAndThirtyThreePointFour_TotalExactlyOneHundred`; `CompositionTests.MoreThanOneDecimalPlace_IsRefused`; web: `composition-care-panel` spec; e2e `composition-care` |
| CMP-8 | `CompositionCareCommandTests.ANewFibreInReferenceData_IsUsableWithNoCodeChange`; web: `composition-care-panel` spec; e2e `composition-care` |

## URLs (`urls`)

| ID | Proven by |
|---|---|
| URL-1 | `ProductPathTests.Build_UsesGenderCategoryAndSlug_InLowerCase`; `UrlCommandTests.ChangingTheSlugOfAPublishedProduct_KeepsTheOldAddressMovingToTheNewOneInOneHop`; web: `url-panel` spec; e2e `urls` |
| URL-2 | `CatalogEndpointsTests.TheWholeJourney_CreateFill_PublishRefusedThenAccepted_RenameAndResolve`; `ProductCommandTests.OnAPublishedProduct_GenderAndCategoryChangeOnlyThroughTheUrlTab`; `ProductTests.APublishedProduct_CannotChangeGenderOrCategoryThroughContentEdits`; `UrlCommandTests.ChangingTheSlugOfAPublishedProduct_KeepsTheOldAddressMovingToTheNewOneInOneHop`; and 1 more; web: `url-panel` spec; e2e `urls` |
| URL-3 | `UrlCommandTests.SeveralSlugChanges_EveryOldAddressPointsStraightAtTheCurrentOne`; `UrlCommandTests.AStalePathChange_IsRefused`; web: `url-panel` spec; e2e `urls` |
| URL-4 | `ProductCommandTests.TenProductsWithTheSameNameCreatedAtOnce_GetDistinctStableSlugs`; `SlugPolicyTests.WithSuffix_AddsAStableNumber`; `SlugPolicyTests.WithSuffix_StaysWithinTheMaximum_AndNeverLeavesADoubleHyphen`; `UrlCommandTests.TwoProductsWithTheSameName_GetStableDistinctSlugs`; and 1 more; web: `url-panel` spec; e2e `urls` |
| URL-5 | `ProductPathTests.Canonicalise_LowerCasesAndRemovesTheTrailingSlash`; `UrlCommandTests.ThePathLookup_IsCaseAndSlashInsensitive_ButReportsTheCanonicalPath`; web: `url-panel` spec; e2e `urls` |
| URL-6 | `ProductSchemaTests.ASlugThatIsNotLowerCaseHyphenated_IsRefusedByTheDatabase`; `ProductSchemaTests.TheSameSlugInTheSameGenderAndCategory_IsRefused_ButDifferentOnesMayShareIt`; `SlugPolicyTests.Normalise_TurnsAnyTextIntoALowerCaseHyphenatedAsciiSlug`; `SlugPolicyTests.Validate_AcceptsAClosedAlphabet`; and 3 more; web: `url-panel` spec; e2e `urls` |
| URL-7 | `CatalogEndpointsTests.AReservedSlug_Is400`; `SlugPolicyTests.Validate_RefusesAReservedWord`; `UrlCommandTests.SuggestSlug_NeverReturnsAReservedWord`; `UrlCommandTests.ABadSlug_IsRefusedWithAMessage`; web: `url-panel` spec; e2e `urls` |
| URL-8 | `SlugPolicyTests.Normalise_CutsALongNameOnAWordBoundary_WithinTheMaximum`; `SlugPolicyTests.Normalise_CutsAWordLongerThanTheMaximumHard`; `SlugPolicyTests.Validate_RefusesASlugOverTheMaximum`; `UrlCommandTests.ASlugOverTheMaximumLength_IsRefused`; web: `url-panel` spec; e2e `urls` |
| URL-9 | `UrlCommandTests.AnUnknownPath_IsNotFound_AndAnArchivedProductsPath_IsGone`; `UrlCommandTests.ADraftOrUnpublishedProduct_IsNotFoundToThePublic`; `UrlCommandTests.TheContractResolvesInProcess_ForTheStorefront`; web: `url-panel` spec; e2e `urls` |
| URL-10 | `ProductPathTests.ABuiltPath_NeverHasAnIdOrAQueryString`; `ProductPathTests.Parse_RefusesAnythingThatIsNotThreeSegments`; web: `url-panel` spec; e2e `urls` |
| URL-11 | `ProductSchemaTests.AnAddressInTheHistory_CannotBeTakenByAnotherProduct`; `UrlCommandTests.AnAddressAnotherProductOnceHad_CannotBeTaken`; `UrlCommandTests.ASlugAnotherCurrentProductHas_CannotBeTaken_ButTheSameSlugInAnotherCategoryCan`; web: `url-panel` spec; e2e `urls` |

## Web-only and structural criteria

| ID | Proven by |
|---|---|
| CHT-6 | `size-chart-view.component.spec.ts` (structured rows rendered as a table with `th scope`); `SizeChartCommandTests.TheProductsChart_IsLimitedToItsSizeRun_AndFallsBackToUnisex` (rows in size order) |
| CHT-7 | `size-chart-editor.component.spec.ts` (labelled inputs, `th scope`); layout suite "new size chart form" at seven widths |
| MED-11 | `images-panel.component.spec.ts` (labels, keyboard operable, errors shown); e2e `product-media`; layout suite "product images tab" |
| CMP-9 | `composition-care-panel.component.spec.ts` (labels, announced total); e2e `composition-care`; layout suite "product composition and care tab" |
