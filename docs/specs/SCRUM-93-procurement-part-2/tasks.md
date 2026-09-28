# SCRUM-93: Tasks

Ordered steps, each small enough for one commit and naming the test that proves it. Backend tests are xUnit v2, `Method_Scenario_Expectation` naming, tagged `[Trait("Spec", "<ID>")]`; integration tests that touch PostgreSQL use Testcontainers. Priority letters (P0–P3) match plan.md's delivery order — if capacity runs short, cut from the bottom of P3 first, never P0–P2.

Ordering here follows build dependency (what has to exist before what), not raw priority order — a P2 task can appear before a later P1 task if the P2 task's schema is a prerequisite for it, same as Sprint 1's tasks.md did with Foundation-first ordering.

## 0. Housekeeping (P0)

- [ ] 1. Confirm PR #17 (SCRUM-179: `includeInactive` 500 fix + 4 E2E locator fixes) is merged into `main`; rebase this branch on top of it once merged. No new test — this is picking up already-verified work.

## REF: new lookups (P0/P1/P2/P3 foundation — everything below depends on these existing)

- [x] 2. Migration: 7 new `REF` lookup tables — `PO_REV_STS_LKP`, `AMND_INIT_LKP`, `PO_VNDR_COMM_TYP_LKP`, `PO_FILE_CATG_LKP` (+ `VNDR_VSBL_IND boolean`), `FBRC_RESP_LKP` as system-owned/read-only (same treatment as `PO_STS_LKP` — `FBRC_RESP_LKP` moved here from the original staff-CRUD plan since it drives real branching logic, same reasoning as the other four); `AMND_RSN_LKP`, `VNDR_COMM_CHNL_LKP` as staff-maintained CRUD. Seed rows per plan.md's Data section. Test: `RefMigrationTests.Migrate_CreatesSprint2LookupTables_WithSeedRows`
- [x] 3. `ListLookupQuery`/CRUD wiring for the 2 new staff-maintained lookups, read-only listing for the 5 system-owned ones — reuses Sprint 1's generic lookup machinery, no new handler code expected. Test: `ListLookupQueryHandlerTests.Handle_Sprint2Lookups_ReturnActiveRowsByDefault`

## VNDR: style guard (P0)

- [x] 4. New `Romp.Modules.Vendor.Contracts` project + `IPurchaseOrderUsageQueries.GetActiveSizeColourUsage(styleId)` (queries `PO_LINE` only at this point — extended in task 20 once `PO_REV_LINE`/Pending revisions exist). Test: `PurchaseOrderUsageQueriesTests.GetActiveSizeColourUsage_ReturnsSizesAndColoursInNonCancelledPos`
- [x] 5. Extend `UpdateStyleCommand`'s validator to call it and reject removal of an in-use size/colour, naming the blocking PO number(s). Test: `UpdateStyleCommandHandlerTests.Handle_RemovingSizeOrColourInUseByActivePo_RejectedWithPoNumbers` (AC-2, AC-3)

## Platform: outbox dispatcher + inbox (P1 — blocks Sprint 3, build early)

- [x] 6. Additive `OUTB_MSG` migration (+10 columns: `AGGR_TYP`, `AGGR_ID`, `MSG_VER`, `ATMP_CNT`, `NXT_ATMP_DTE`, `CLM_BY`, `LEAS_EXPY_DTE`, `PROC_DTE`, `DEDL_IND`, `DEDL_RSN` — column names/count corrected to match plan.md's Data section and the glossary; this task's own text had drifted to `SCHM_VER`/`CLMD_BY`/"+9", neither of which the glossary backs), safe defaults. Test: `OutboxMigrationTests.Migrate_AddsDispatcherColumns_ExistingRowsGetSafeDefaults` (AC-52)
- [x] 7. `IOutboxDispatcher` / `IOutboxMessageHandler<TEvent>` interfaces + generic hosted service: lease-based claim (`FOR UPDATE SKIP LOCKED` in a short transaction, stamps `CLM_BY`/`LEAS_EXPY_DTE` — column name corrected per task 6), handler runs outside that transaction, marks `PROC_DTE` only after success. Talks to each registered schema's `OUTB_MSG` over raw ADO.NET (Npgsql), not through any module's own DbContext, so `Romp.BuildingBlocks.Persistence` never depends on a module's Infrastructure assembly. Not yet wired to any real module (that's task 13) — this task's test registers its own throwaway schema/handler. Test: `OutboxDispatcherTests.Dispatch_CommittedRow_DeliveredAndMarkedProcessed` (AC-53, AC-54)
- [ ] 8. Lease expiry redelivery. Test: `OutboxDispatcherTests.Dispatch_ExpiredLease_RowBecomesClaimableAgain` (AC-55)
- [ ] 9. Retry with exponential backoff + jitter; dead-letter after configured max attempts, payload/history retained, error-level log written. Test: `OutboxDispatcherTests.Dispatch_HandlerFailsRepeatedly_BacksOffThenDeadLetters` (AC-57, AC-58)
- [ ] 10. Per-aggregate ordering: a later message for the same `AGGR_ID` is not dispatched while an earlier one is pending/in-flight/retry-scheduled; different aggregates proceed independently; a dead-lettered message doesn't block later ones for its aggregate. Test: `OutboxDispatcherTests.Dispatch_SameAggregateMultipleMessages_DeliveredInOrder_DeadLetterDoesNotBlockLater` (AC-59)
- [ ] 11. Confirm dispatcher/handler failure never affects the originating business transaction (it only ever reads already-committed rows — mostly a design assertion, cover with a test that a failing handler doesn't roll back the row that was already committed by the business command). Test: `OutboxDispatcherTests.HandlerFailure_DoesNotAffectAlreadyCommittedBusinessRow` (AC-60, NFR-FT-08)
- [ ] 12. Inbox table convention: `<SCHEMA>.INBX` shape (`MSG_ID`, `HNDL_NAME`, `PROC_DTE`, composite PK) + a reusable EF configuration/base class a module applies when it registers its first real handler. Not instantiated in any schema this sprint (no real consumer yet) — test the shape/helper in isolation. Test: `InboxConventionTests.ApplyInboxConfiguration_ProducesExpectedShape` (AC-56, NFR-FT-05)
- [ ] 13. Placeholder: register a logging handler for VNDR events in dev/prod so messages reach `Processed`; a recording test double proving exactly-once invocation on the happy path, and a failing test double driving retry/backoff/dead-letter, in tests. Test: `OutboxDispatcherTests.RecordingHandler_ReceivesEachMessageExactlyOnce` / `FailingHandler_DrivesRetryBackoffDeadLetter` (AC-61)
- [ ] 14. Inbox retention purge + startup guard (refuse to start if retention ≤ max retry window). Test: `InboxRetentionTests.Purge_RemovesRowsOlderThanRetention` / `Startup_RetentionNotLongerThanRetryWindow_Throws` (AC-62)
- [ ] 15. Expose dead-letter count on the health/diagnostics surface if one exists in this codebase; otherwise log-only and note the gap rather than inventing a new surface. Test: `HealthDiagnosticsTests.DeadLetterCount_ExposedWhenSurfaceExists` (AC-58, best-effort)

## VNDR: PO commercial terms (P2)

- [ ] 16. Migration: `PO_MAIN` +4 nullable columns (`LATE_ACPT_DT`, `OVER_TOL_PCT`, `UNDR_TOL_PCT`, `FBRC_RESP_ID`). Test: `PoMigrationTests.Migrate_AddsCommercialTermsColumns_ExistingRowsNull` (AC-7)
- [ ] 17. Extend `UpdatePurchaseOrderCommand` + validator: new fields, Draft-only (unchanged mechanism), latest-acceptable-date ≥ expected-date, tolerances within configured max. Test: `UpdatePurchaseOrderCommandHandlerTests.Handle_NewCommercialTerms_SavesInPlaceNoRevision` (AC-5, AC-8)
- [ ] 18. Extend `SendPurchaseOrderCommand` validation: reject Send without latest-acceptable-date or fabric responsibility. Test: `SendPurchaseOrderCommandHandlerTests.Handle_MissingLatestAcceptableDateOrFabricResponsibility_Rejected` (AC-6)

## VNDR: PO revisions — schema + domain (ADR 0007)

- [ ] 19. Migration: `PO_REV`, `PO_REV_STS_HIST`, `PO_REV_LINE`, `PO_FILE` (incl. nullable `VNDR_COMM_ID`), `PO_VNDR_COMM`; partial unique index `PO_REV (PO_ID) WHERE STS_ID = Pending`. Test: `PoRevisionMigrationTests.Migrate_CreatesRevisionTablesWithPartialUniqueIndex`
- [ ] 20. `PurchaseOrderRevision` domain entity: revision-number allocation (`MAX+1` inside the transaction that bumps `PO_MAIN.xmin`), no update path on snapshot/impact fields after creation. Test: `PurchaseOrderRevisionTests.Create_TwoConcurrentAmendments_OneSucceedsOneConflicts` (AC-24); `PurchaseOrderRevisionTests.NoPublicMethodMutatesSnapshotAfterCreation` (AC-22, ADR 0007)
- [ ] 21. Data migration: back-fill a synthetic Rev 0 (and an acknowledged-revision-0 record where applicable, channel `Unspecified`) for every existing Sprint 1 PO that's ever been Sent. Test: `PoRevisionBackfillTests.Migrate_ExistingSentPos_GetRevZero` (AC-64)
- [ ] 22. `CreateAmendmentCommand` + validator: builds a full snapshot from the in-force revision plus the requested changes; rejects a true no-op (AC-16); requires reason + internal impact note (AC-17); computes and stores impact figures including the "beyond latest acceptable date" flag (AC-18, AC-19); branches on PO status — `SentToVendor` → new revision immediately `InForce`, prior `Superseded` (AC-9); `Acknowledged` → new revision `Pending` (AC-10); `Draft`/`Cancelled` → rejected (AC-20); enforces at most one open `Pending` revision (AC-14); writes `PO_REV_STS_HIST` + outbox event in the same transaction (AC-47). Tests: `CreateAmendmentCommandHandlerTests.Handle_SentToVendor_NewRevisionImmediatelyInForce` / `Handle_Acknowledged_NewRevisionPending` / `Handle_NoOpChange_Rejected` / `Handle_MissingReasonOrImpactNote_Rejected` / `Handle_DraftOrCancelled_Rejected` / `Handle_ExistingPendingRevision_Rejected` / `Handle_AnyRevision_WritesOutboxEvent`
- [ ] 23. Field/line/file scope of an amendment: unit cost, dates, tolerances, payment term, advance %, fabric responsibility, line add/remove/change (validated against the style's size run/colourways, ≥1 line remains), spec-file add/retire; vendor and style rejected as amendable. Test: `CreateAmendmentCommandHandlerTests.Handle_AmendableFieldSet_AppliesOnlyAllowedFields` / `Handle_VendorOrStyleChange_Rejected` (AC-15)
- [ ] 24. `DecideRevisionCommand` (Accept/Reject): Accept moves the revision to `InForce`, prior `InForce` to `Superseded`, updates the `PO_MAIN`/`PO_LINE` mirror in the same transaction; Reject keeps the PO's position unchanged. Test: `DecideRevisionCommandHandlerTests.Handle_Accept_RevisionInForceMirrorUpdated` / `Handle_Reject_PositionUnchangedRevisionVisibleInHistory` (AC-11, AC-12)
- [ ] 25. `WithdrawRevisionCommand`. Test: `WithdrawRevisionCommandHandlerTests.Handle_PendingRevision_BecomesWithdrawn` (AC-13)
- [ ] 26. Extend `CancelPurchaseOrderCommand`: auto-withdraw an open Pending revision in the same transaction as cancellation. Test: `CancelPurchaseOrderCommandHandlerTests.Handle_PoWithPendingRevision_AutoWithdrawsRevision` (AC-21)
- [ ] 27. `GetPurchaseOrderRevisionsQuery`: full history with before/after diff per revision. Test: `GetPurchaseOrderRevisionsQueryHandlerTests.Handle_ReturnsRevisionHistoryWithDiffs` (AC-34)
- [ ] 28. Extend `IPurchaseOrderUsageQueries` (task 4) to also include a Pending revision's lines, now that `PO_REV_LINE` exists. Test: `PurchaseOrderUsageQueriesTests.GetActiveSizeColourUsage_IncludesPendingRevisionLines` (AC-2, AC-3, extended)
- [ ] 29. Endpoints: `POST .../amendments`, `.../amendments/{revNo}/accept`, `.../amendments/{revNo}/reject`, `.../amendments/{revNo}/withdraw`, `GET .../revisions`. Test: covered by the handler tests above plus a thin endpoint-mapping test per Sprint 1's pattern.

## VNDR: vendor response & communication capture

- [ ] 30. `RecordVendorResponseCommand`: `ConfirmedAsSent` transitions the PO to Acknowledged and stores the acknowledged revision number, rejecting a stale acknowledgement (naming the latest revision); `Countered` creates a vendor-initiated Pending revision (reuses task 22's revision-creation logic, parameterised by initiator); `Declined` returns a signal for the API layer to pre-fill Cancel with `VendorDeclined` (no state change itself). Every outcome captures channel/responder/response-time/evidence. Test: `RecordVendorResponseCommandHandlerTests.Handle_ConfirmedAsSent_TransitionsToAcknowledged` / `Handle_StaleRevisionNumber_Rejected` / `Handle_Countered_CreatesVendorInitiatedPendingRevision` / `Handle_Declined_ReturnsCancelSignal` / `Handle_MissingChannel_Rejected` (AC-25, AC-26, AC-27, AC-29, AC-31)
- [ ] 31. `RecordVendorAmendmentRequestCommand`: on an Acknowledged PO, creates a vendor-initiated Pending revision for the buyer to accept/reject. Test: `RecordVendorAmendmentRequestCommandHandlerTests.Handle_Acknowledged_CreatesVendorInitiatedPendingRevision` (AC-30, AC-31)
- [ ] 32. Rewire Sprint 1's `AcknowledgePurchaseOrderCommand` (empty-body) to internally call `RecordVendorResponseCommand` with outcome `ConfirmedAsSent`, revision = current in-force, channel `Unspecified`. Sprint 1's tests and E2E must keep passing unchanged. Test: `AcknowledgePurchaseOrderCommandHandlerTests.Handle_EmptyBody_DelegatesToRecordVendorResponse` (AC-4); re-run Sprint 1's full suite as a regression gate.
- [ ] 33. Endpoints: `POST .../vendor-response`, `POST .../vendor-amendment-request`. Test: endpoint-mapping tests per Sprint 1's pattern.

## VNDR: spec files

- [ ] 34. `IFileStorage` abstraction + local-disk implementation for dev/Docker Compose (generated storage keys, never filename-derived). Test: `LocalFileStorageTests.Save_GeneratesKeyNotDerivedFromFilename` / `Retrieve_ReturnsStoredBytes`
- [ ] 35. `UploadPoFileCommand`/`RemovePoFileCommand`: Draft — free add/soft-remove for any category; internal categories — addable anytime pre-Cancel, never removable after Send; vendor-visible categories — locked after Send (rejected with a message pointing to Amend). Content-signature type check, size/count limits from configuration. Test: `UploadPoFileCommandHandlerTests.Handle_DraftAnyCategory_StoredAndListed` / `Handle_PostSendVendorVisible_RejectedPointsToAmend` / `Handle_PostSendInternal_Allowed` / `Handle_InvalidTypeOrOverLimit_RejectedNothingStored` (AC-37, AC-40, AC-41, AC-45)
- [ ] 36. `DownloadPoFileQuery`: forced-download headers (`Content-Disposition: attachment`, `X-Content-Type-Options: nosniff`), sanitised filename. Test: `DownloadPoFileQueryHandlerTests.Handle_ReturnsAttachmentHeadersAndSanitisedFilename` (AC-46)
- [ ] 37. Wire vendor-visible file add/retire into `CreateAmendmentCommand`'s file-changes parameter (task 22/23): a file's effective window is `ADDD_REV_ID`/`RETD_REV_ID`; Rev 0's effective set is whatever vendor-visible files existed at Send. Test: `CreateAmendmentCommandHandlerTests.Handle_FileChanges_UpdatesEffectiveWindow` / `RevZero_EffectiveSetIsFilesPresentAtSend` (AC-38, AC-43, AC-44)
- [ ] 38. `GetPoFileListQuery`: lists filename/category/uploaded-by/uploaded-at/added-in-revision/retired-in-revision. Test: `GetPoFileListQueryHandlerTests.Handle_ReturnsFilesWithRevisionWindow` (AC-44)
- [ ] 39. Cancelled-PO file rules: no add/remove, existing files stay viewable/downloadable. Test: `UploadPoFileCommandHandlerTests.Handle_CancelledPo_Rejected` / `RemovePoFileCommandHandlerTests.Handle_CancelledPo_Rejected` (AC-42)
- [ ] 40. Endpoints: `POST/GET/DELETE .../files`, `GET .../files/{fileId}` (download). Test: endpoint-mapping tests.

## VNDR: send-time checklist + vendor-facing view

- [ ] 41. Send confirmation: non-blocking checklist text; if no `TechPackSpec` file is attached, require an explicit "send anyway" and record it in history. Test: `SendPurchaseOrderCommandHandlerTests.Handle_NoTechPackSpecFile_RequiresExplicitConfirmAndRecordsIt` (AC-39)
- [ ] 42. `VendorPoViewDto` (hand-authored, no target-cost/retail-price/internal-note/internal-file/vendor-evidence fields) + `GetVendorFacingPoViewQuery`. Reflection-based leak test asserting the forbidden fields don't exist on the type. Test: `VendorPoViewDtoTests.Type_DoesNotExposeInternalFields` (AC-36); `GetVendorFacingPoViewQueryHandlerTests.Handle_ReturnsExpectedShapeIncludingPendingMarker` (AC-35)
- [ ] 43. Endpoint: `GET .../vendor-view`. Test: endpoint-mapping test.

## Events: contract finalization

- [ ] 44. Extend Sprint 1's 4 events additively to schema v2 (+ revision number, + terms snapshot); define v1 payloads for the 5 new revision events; shared envelope (message id, event type, schema version, occurred-at UTC, aggregate type/id, `PO_NO`) on every payload. Test: `EventPayloadTests.Sprint1Events_V2_AdditiveOnly_NoFieldMeaningChanges` / `RevisionEvents_CarryFullEnvelopeAndSnapshot` (AC-48, AC-49)
- [ ] 45. Payload builder excludes internal impact note, vendor evidence, internal files from every payload. Test: `EventPayloadTests.Payload_NeverContainsInternalData` (AC-50)
- [ ] 46. Finalize `Romp.Modules.Vendor.Contracts`: in-force terms query, revision-list query, effective-file-set query, style-usage query (task 4/28) — all read-only, for Sprint 3's future consumption. Test: `VendorContractsTests.Queries_ReturnExpectedShapesForFutureConsumers` (AC-51)

## Admin frontend

- [ ] 47. PO detail view: revision-history panel (number, initiator, status, terms diff, impact figures, reason, note, message to vendor, comm details, evidence, before/after). Test: `po-detail.component.spec.ts` extended to assert the panel renders a given revision list.
- [ ] 48. Amend action/form: any subset of terms/lines/files, reason, mandatory internal note, optional vendor message.
- [ ] 49. Record vendor response action (replaces bare Acknowledge): Confirmed as sent / Countered / Declined, with Countered opening the amendment form pre-flagged vendor-initiated.
- [ ] 50. Pending-revision actions (Accept/Reject/Withdraw), shown only when a Pending revision exists, naming the decider.
- [ ] 51. New route `/purchase-orders/{id}/vendor-view`: minimal, no admin chrome, phone-readable, print-friendly (A4).
- [ ] 52. File management panel: grouped vendor-visible/internal, upload with category selector, download, remove (Draft vendor-visible; pre-Send internal); disabled + tooltip pointing to Amend where a direct action is blocked.
- [ ] 53. Send confirmation dialog: non-blocking checklist, explicit "send anyway" when no tech pack attached.
- [ ] 54. Reference Data screen: add `amendment-reasons`, `vendor-comm-channels` to the existing lookup-type selector (system-owned ones stay unexposed, same as `po-statuses`).
- [ ] 55. Verify the Styles screen's existing `ProblemDetails` field-error rendering surfaces the style-guard's rejection message (blocking PO numbers) without new UI code — add a test if it doesn't already cover this shape.

## End-to-end / Definition of Done

- [ ] 56. `docker compose` demo seed: extend with a sample amendment so the Friday demo has something to show beyond the happy path.
- [ ] 57. Playwright E2E extended: amend an Acknowledged PO, record the vendor's acceptance, assert the revision goes In force and the PO's terms update; assert the dispatcher moved the resulting event to Processed (via the logging handler's observable effect, e.g. a log line or a diagnostics counter).
- [ ] 58. README "Try Sprint 2" section + demo script.
- [ ] 59. Update `docs/db/naming.md`'s glossary with this plan's proposed abbreviations (confirm none collide with an existing entry first).
- [ ] 60. Update `spec.md`'s status to **Implemented**, noting any point where the implementation differed from `plan.md`.
- [ ] 61. Jira housekeeping (spec's open question): update SCRUM-13's description to say it consumes the SCRUM-181 dispatcher rather than building its own; fix SCRUM-92's scope comment (says Cancel from Draft/Sent) to match the Sprint 1 spec's actual Draft/Sent/Acknowledged.
- [ ] 62. Log BRD errata under SCRUM-168: the 3 new PO commercial terms with no FR ID (plan.md's R10), cost sheets having no FR/fields/table (R13), and Sprint 1's still-open "no FR for style master" question.
