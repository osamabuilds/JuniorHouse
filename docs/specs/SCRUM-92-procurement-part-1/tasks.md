# SCRUM-92: Tasks

Ordered steps. Each is small enough for one commit and names the test that proves it. Backend tests are xUnit v2, `Method_Scenario_Expectation` naming, tagged `[Trait("Spec", "<ID>")]` with the AC/FR/ADR id(s) in the third column; integration tests that touch PostgreSQL use Testcontainers (CLAUDE.md). Windows Application Control blocks `Romp.Api.IntegrationTests` locally — those steps are verified on CI (Linux), not on this machine.

One clarification made while breaking the plan into tasks, not re-litigated in spec.md/plan.md: **`PO_CNCL_RSN_LKP` gets the same staff-maintained CRUD as the other nine lookups** (only `PO_STS_LKP` is system-owned/read-only) — a cancel reason is exactly the kind of reason code §12.4 says should be addable from the admin panel without a deployment, same as the BRD's own `ReturnReasons` example.

## Foundation (SCRUM-162, 170, 171, 165 — blocks everything below)

- [x] 1. Wire `REF`, `CTLG`, `VNDR` `DbContext`s (Npgsql, one schema each, design-time factories, migrations applied at API startup in Development only). Test: `RompApiHostTests.Startup_ConfiguresThreeDbContexts_NoException` (SCRUM-162)
- [x] 2. Add the audit + concurrency EF `SaveChangesInterceptor` (sets `INSR_DTE`/`INSR_BY` on insert, `UPDT_DTE`/`UPDT_BY` on update; relies on PostgreSQL `xmin` for the concurrency token — no app-set column needed for that part). Test: `AuditInterceptorTests.SaveChanges_NewEntity_SetsInsrDteAndInsrBy` / `SaveChanges_ModifiedEntity_SetsUpdtDteAndUpdtBy` (SCRUM-162)
- [x] 3. Add the naming-convention architecture test: every table/column/constraint/index name in every `DbContext`'s model matches `^[A-Z0-9]{2,4}(_[A-Z0-9]{2,4})*$`, and every FK column has a covering index. Write it now, against the still-empty contexts, so every entity added from here on is checked from its first commit. Test: `NamingConventionTests.AllTableAndColumnNames_MatchAbbreviationPattern` / `AllForeignKeyColumns_AreIndexed` (SCRUM-170)
- [x] 4. Add the MediatR pipeline: `ValidationBehavior<TRequest,TResponse>` (FluentValidation → `ProblemDetails` 400 with field errors), a logging behaviour, and a unit-of-work/transaction behaviour wrapping each command. Test: `ValidationBehaviorTests.Handle_InvalidCommand_ReturnsProblemDetailsWithFieldErrors` (SCRUM-171, AC-15)
- [x] 5. Add the outbox building block: `OUTB_MSG` shape + `SaveChangesInterceptor` that writes queued domain events to it in the same transaction as the business change (writer only — no dispatcher this sprint, per SCRUM-165's Sprint 1 scope note). Instantiate the table as `VNDR.OUTB_MSG` for this module. Test: `OutboxInterceptorTests.SaveChanges_WithDomainEvent_WritesOutboxRowInSameTransaction` (SCRUM-165, ADR 0004)

## REF: reference data (SCRUM-172)

- [x] 6. Migration: create the `REF` schema and all eleven lookup tables (`SIZE_LKP`, `CLR_LKP`, `FBRC_LKP`, `GNDR_LKP`, `AGE_BRKT_LKP`, `CATG_LKP` with `PRNT_CATG_ID`, `CITY_LKP`, `PAYM_TERM_LKP` with `DFLT_ADV_PCT`, `VNDR_SPCL_LKP`, `PO_STS_LKP`, `PO_CNCL_RSN_LKP`), seeded with initial rows (`PO_STS_LKP`: Draft/SentToVendor/Acknowledged/Cancelled at minimum; `PO_CNCL_RSN_LKP`: VendorDeclined/CostDispute/QualityConcern/StyleDiscontinued/DuplicateEntry/Other). Test: `RefMigrationTests.Migrate_CreatesAllLookupTables_WithSeedRows` (AC-1)
- [x] 7. `Romp.Modules.Reference.Contracts` project + a generic `ListLookupQuery<TLookup>` (returns active rows only by default, `includeInactive` for admin) with one endpoint per lookup type under `/api/ref/{lookup}`. Test: `ListLookupQueryHandlerTests.Handle_DefaultsToActiveOnly_IncludesInactiveWhenRequested` (AC-1, AC-2)
- [x] 8. Generic `CreateLookupCommand<T>` / `UpdateLookupCommand<T>` / `RetireLookupCommand<T>` for all ten staff-maintained lookups (everything except `PO_STS_LKP`). Test: `LookupMutationCommandTests.Retire_SetsActIndFalse_ExcludedFromDefaultList` (AC-2)

## CTLG: style master (SCRUM-173)

- [x] 9. Migration: `CTLG` schema (`STYL_MAIN`, `STYL_CLR_MAP`, `STYL_SIZE_MAP`, `STYL_TGT_LINE`). Test: `CtlgMigrationTests.Migrate_CreatesStyleTables`
- [x] 10. `CreateStyleCommand` + validator (unique `STYL_CODE`, ≥1 colourway, ≥1 size). Test: `CreateStyleCommandHandlerTests.Handle_ValidStyle_PersistsWithMapsAndTargetLines` (AC-3)
- [x] 11. Test: `CreateStyleCommandHandlerTests.Handle_DuplicateCode_RejectedWithValidationError` (AC-4)
- [x] 12. Test: `CreateStyleCommandHandlerTests.Handle_TargetLineForSizeOrColourNotInStyle_RejectedWithValidationError` (plan.md's `STYL_TGT_LINE` invariant)
- [x] 13. `UpdateStyleCommand`. Test: `UpdateStyleCommandHandlerTests.Handle_ValidEdit_UpdatesFieldsAndAuditColumns` (AC-3)
- [x] 14. `Romp.Modules.Catalog.Contracts` project (exposes `IStyleQueries` for `VNDR` to validate PO lines against) + `GetStyleByIdQuery` / `SearchStylesQuery` + endpoints. Test: `SearchStylesQueryHandlerTests.Handle_FilterByCategoryAndActive_ReturnsMatchingPage`

## VNDR: vendor (SCRUM-91)

- [x] 15. Migration: `VNDR` schema, `VNDR` table + `VNDR_SPCL_MAP`. Test: `VndrMigrationTests.Migrate_CreatesVendorTables`
- [x] 16. `CreateVendorCommand` (one or more specialisations, default payment term, `ACT_IND` defaults true, `ONTM_PCT`/`ONQT_PCT`/`DFCT_RATE_PCT` left `null`). Test: `CreateVendorCommandHandlerTests.Handle_MultipleSpecialisations_PersistsAllInMap` (AC-5, AC-5a)
- [x] 17. `UpdateVendorCommand`. Test: `UpdateVendorCommandHandlerTests.Handle_ValidEdit_UpdatesAuditColumns` (AC-6)
- [x] 18. `GetVendorByIdQuery` / `SearchVendorsQuery` + endpoints. Test: `SearchVendorsQueryHandlerTests.Handle_FilterBySpecialisation_ReturnsMatchingPage`

## VNDR: purchase order (SCRUM-92, ADR 0006)

- [x] 19. Migration: `PO_MAIN`, `PO_LINE`, `PO_STS_HIST` (append-only — `INSR_DTE`/`INSR_BY` only, no `UPDT_*`/`xmin`), `PO_NO_SEQ`. Test: `PoMigrationTests.Migrate_CreatesPurchaseOrderTables`
- [x] 20. PO number allocator: atomic `INSERT ... ON CONFLICT ("YR") DO UPDATE ... RETURNING "SEQ"` against `PO_NO_SEQ`, formatted as `PO-{YYYY}-{NNNNN}`. Test: `PoNumberAllocatorTests.Allocate_20ConcurrentCalls_ReturnsNoDuplicates` — real Testcontainers Postgres, actual concurrent tasks, not mocked (AC-7b, ADR 0006)
- [x] 21. `CreatePurchaseOrderCommand`: vendor must be active (via `VNDR` data directly — same module) and style must exist (via `IStyleQueries`, `Romp.Modules.Catalog.Contracts`); payment term + advance % default from the vendor's `PAYM_TERM_ID` unless overridden. Test: `CreatePurchaseOrderCommandHandlerTests.Handle_ValidPo_CreatesInDraftWithGeneratedPoNo` (AC-7)
- [x] 22. Test: `CreatePurchaseOrderCommandHandlerTests.Handle_NoOverride_DefaultsPaymentTermAndAdvancePctFromVendor` (AC-7a)
- [x] 23. Test: `CreatePurchaseOrderCommandHandlerTests.Handle_LineSizeOrColourNotInStyle_RejectedWithValidationError` (AC-8)
- [x] 24. `UpdatePurchaseOrderCommand`, Draft only. Test: `UpdatePurchaseOrderCommandHandlerTests.Handle_DraftPo_SavesChanges` (AC-9)
- [x] 25. Test: `UpdatePurchaseOrderCommandHandlerTests.Handle_AcknowledgedPo_RejectedWithDomainError` (AC-13)
- [x] 26. `SendPurchaseOrderCommand` (Draft → SentToVendor): writes `PO_STS_HIST` row + outbox event in the same transaction. Test: `SendPurchaseOrderCommandHandlerTests.Handle_DraftPo_TransitionsAndWritesHistoryAndOutboxEvent` (AC-10)
- [x] 27. `AcknowledgePurchaseOrderCommand` (SentToVendor → Acknowledged). Test: `AcknowledgePurchaseOrderCommandHandlerTests.Handle_SentPo_TransitionsAndWritesHistoryAndOutboxEvent` (AC-11)
- [x] 28. `CancelPurchaseOrderCommand` (Draft/SentToVendor/Acknowledged → Cancelled, mandatory `PO_CNCL_RSN_ID`). Test: `CancelPurchaseOrderCommandHandlerTests.Handle_ValidReason_CancelsAndWritesHistoryAndOutboxEvent` (AC-12)
- [x] 29. Test: `CancelPurchaseOrderCommandHandlerTests.Handle_MissingReason_RejectedWithValidationError` (AC-12a)
- [x] 30. `GetPurchaseOrderByIdQuery` (includes the `PO_STS_HIST` timeline, oldest first) / `SearchPurchaseOrdersQuery` + endpoints. Test: `GetPurchaseOrderByIdQueryHandlerTests.Handle_ReturnsStatusHistoryTimelineInOrder` (AC-14)

## Admin frontend (SCRUM-174)

- [ ] 31. Admin shell: semantic layout (`header`/`nav`/`main`), nav for Reference Data / Styles / Vendors / Purchase Orders, typed API client, `ProblemDetails` → inline field errors, loading/empty/error states, `noindex, nofollow`. Test: `app.spec.ts` extended to assert the landmark elements and nav render
- [ ] 32. Reference Data screen: lookup-type selector, searchable table, add/edit/retire forms for the ten mutable lookups; `PO Statuses` rendered read-only (AC-2 in the UI).
- [ ] 33. Styles screen: list (search/filter), create/edit form with the colour×size target-quantity grid (only cells for that style's own colourways/size run are editable) (AC-3, AC-4).
- [ ] 34. Vendors screen: list (search by name/city/specialisation), create/edit form with a specialisation multi-select (AC-5, AC-5a, AC-6).
- [ ] 35. Purchase Orders screen: list (filter by vendor/status/date), create/edit form (Draft only, locked otherwise), detail view with the status timeline and Send/Acknowledge/Cancel-with-reason actions, each disabled when illegal for the current status (AC-7–AC-14).

## End-to-end / Definition of Done (SCRUM-175)

- [ ] 36. `docker compose up` starts PostgreSQL + the API (migrations + `REF` seed) + the admin app in one command; add an optional demo-data seed (a handful of sample vendors and styles).
- [ ] 37. Playwright E2E covering the full Sprint 1 flow. Test: `procurement.e2e.spec.ts: Staff_CreatesStyleVendorAndPo_ThroughToAcknowledged` — create style → create vendor → raise PO → send → acknowledge, asserting the status timeline at the end.
- [ ] 38. README "Try Sprint 1" section + Friday demo script.
- [ ] 39. Update `spec.md`'s status to **Implemented**, and note any point where the implementation had to differ from `plan.md`.
