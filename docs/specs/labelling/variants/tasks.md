# Variants: tasks

Ordered, test-first. Every step is done (2026-10-14). The test that proves each step is named; the full table is in [../traceability.md](../traceability.md).

- [x] V1. `CompanyPrefix` (7-9 digits, test range, capacity) and `Gtin` (create, strict parse, GS1 check digit). Test: `GtinTests` (published GTINs, bad input, capacity).
- [x] V2. `Variant` aggregate: SKU pattern, one active GTIN, `AssignGtin`, `Void`, events. Test: `VariantTests`.
- [x] V3. `SKU` schema for variants: `VRNT_MAIN`, `VRNT_GTIN` (unique GTIN, unique prefix+reference, **partial unique index** on the active GTIN), `GTIN_NO_SEQ`, lookups; migration. Test: `VariantAssignmentTests.TheDatabaseItself_RefusesASecondActiveGtinForOneVariant`, `NamingConventionTests` (SKU context).
- [x] V4. Atomic counter `GtinSequence` (ADR 0006 upsert, quoted identifiers). Test: `ARolledBackAssignment_ReleasesItsNumber…`, `ManyAssignmentsAtOnce…`.
- [x] V5. `VariantAssigner` (`IVariantAssigner`): create variant, take next number, capacity check, SKU via `SkuCodeBuilder`. Test: `AcceptedBatch_GivesEachVariant…`, `ALaterDeliveryOfTheSameVariant_ReusesTheGtin…`, `WhenTheRangeIsUsedUp…`.
- [x] V6. `VariantReader` (`IVariantReader`): the identity other features may read. Test: used by `PrintWorkerTests` and `TagDocumentBuilder`.
- [x] V7. `VoidGtinCommand` with row lock, validator (reason, note, person without control characters). Test: `Void_NeedsAReasonAndAPerson…`, `VoidingRetiresTheNumberForGood…`.
- [x] V8. Queries: `SearchVariantsQuery` (paged, search by SKU or GTIN, status), `GetVariantQuery`, `GetGtinCapacityQuery` (80% warning). Test: `TheRegister_IsPaged_Ordered_Searchable…`, `WhenTheRangeIsUsedUp…`.
- [x] V9. Start-up options guard (`LabellingOptionsValidator`): bad prefix, test prefix in Production, fake printers in Production. Test: `LabellingOptionsValidatorTests`.
- [x] V10. Endpoints through `MapSecuredGroup` (void is `Admin`, rate limited, 16 KB body limit). Test: `EndpointSecurityTests`; e2e `variants`.
- [x] V11. Web feature `labelling/variants`: store, service, `SkuRegisterPageContainer`, `VariantTableComponent`, `GtinHistoryComponent`, `VoidFormComponent`, capacity and test-prefix banners. Test: `variants.reducer.spec`, `variant-table`, `void-form` specs; e2e `variants`.
