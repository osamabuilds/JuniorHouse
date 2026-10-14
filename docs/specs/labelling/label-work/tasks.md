# Label work: tasks

Ordered, test-first. Every step is done (2026-10-14). The full acceptance-criterion table is in [../traceability.md](../traceability.md).

- [x] L1. `LabelWorkItem` entity and `SKU.LBL_WORK_ITEM` (unique per sub-batch line), lookups for status, outcome and park reason; migration. Test: `LabelWorkItemTests`, `NamingConventionTests`.
- [x] L2. `QcBatchAcceptedMapper` (anti-corruption layer, validation, 500-line cap). Test: `QcBatchAcceptedMapperTests`.
- [x] L3. `RecordAcceptedBatchCommand` + `LabelWorkResolver`: style from the PO, codes from Reference, GTINs via `IVariantAssigner`; parks instead of failing. Test: `LabelWorkTests` (idempotent, no style, retired style, unknown colour).
- [x] L4. `QcBatchAcceptedConsumer` through the inbox; a refused message throws so the dispatcher retries and dead-letters it. Test: covered end to end by the e2e `label-work` journey and the integration fixture.
- [x] L5. `ResolveWorkItemCommand` (with `ConfirmRetiredStyle`). Test: `APoWithNoStyle_IsParked…`, `ARetiredStyle_IsParkedUntilStaffConfirm…`.
- [x] L6. `SearchLabelWorkQuery` (paged, status, style, price flag) and `GetLabelWorkItemQuery`; labelled counts and price flag from `IPrintProgress`. Test: `TheWorklist_IsPaged_Filtered_AndRunsAConstantNumberOfQueries`, `PriceChangedAfterTheTagsWereQueued…`.
- [x] L7. `QueueTagsCommand` (row lock, spare allowance, hands the job to print-jobs) and `GetTagPreviewQuery`. Test: `QueueingTags_NeverExceedsTheAcceptedQuantity…`, `TwoClicksAtOnce…`, `TheTagBuilder_PutsTheCurrentCatalogPrice…`.
- [x] L8. Catalog `IStyleQueries.GetLabelInfoAsync` and `TGT_PRIC_CHG_DTE` (migration); Reference `IApparelCodeQueries`. Test: `StyleLabelInfoTests`, `ApparelCodeQueriesTests`.
- [x] L9. Endpoints through `MapSecuredGroup`, strict rate limit on resolve, queue and preview. Test: `EndpointSecurityTests`; e2e `label-work`.
- [x] L10. Web feature `labelling/label-work`: store, service, `LabelWorklistPageContainer`, `WorklistTableComponent`, `QueueTagsFormComponent`. Test: `label-work.reducer.spec`, `worklist-table`, `queue-tags-form` specs; e2e `label-work`.
