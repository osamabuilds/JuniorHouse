# Print jobs: tasks

Ordered, test-first. Every step is done (2026-10-14). The full acceptance-criterion table is in [../traceability.md](../traceability.md).

- [x] P1. `PrintJob` aggregate (state machine, backoff, price snapshot, attempts, events) and `SKU.PRT_JOB`, `PRT_JOB_ATMP`, `JOB_NO_SEQ`, printer and status lookups; migrations. Test: `PrintJobTests`, `NamingConventionTests`.
- [x] P2. Ports `ILabelPrinter`, `IPrintJobClaims`, `IJobNumberAllocator`, `IRowLocks`; SQL implementations (`FOR UPDATE SKIP LOCKED` claim, stuck-job recovery, job numbers). Test: `EightWorkersRacingForOneJob_ExactlyOneWinsTheClaim`, `AJobStuckInPrinting_GoesBackToTheQueue`.
- [x] P3. `QueueOriginalPrintJobCommand` (GTIN active, configured printer only, queue limit, saves without touching a printer). Test: `QueueingAJob_SavesItInstantly…`, `APrinterQueueHasALimit`, `APrinterIsChosenByConfiguredCodeOnly…`.
- [x] P4. `PrintJobProcessor` + `CompletePrintJobCommand` + `PrintWorker`: per-printer pass, backoff without blocking, outcome recorded in a separate transaction. Test: `AJammedPrinter_RetriesWithBackoff_EndsFailed…`, `AJammedPrinter_DoesNotStopTheOtherPrintersJobs`, `APrintedJob_RecordsTheAttempt…`.
- [x] P5. `RetryPrintJobCommand`, `CancelPrintJobCommand` (Admin-ready). Test: `…StaffCanRetryThatSameJob`, `Cancel_WorksOnQueuedAndFailedJobs…`.
- [x] P6. `ReprintTagCommand` (same GTIN, reason, person, current price, keeps the concession mark, refuses a retired GTIN). Test: `AReprint_UsesTheSameGtin…`, `Reprint_NeedsAReasonAPersonAndASaneCount`, `QueueingOrReprinting_ForARetiredGtin_IsRefused`.
- [x] P7. Queries: `SearchPrintJobsQuery`, `GetPrintHistoryQuery`, `ListPrintersQuery`, `IPrintProgress`. Test: `TheQueueScreen_IsPaged_FilteredByStatus…`.
- [x] P8. Printers: `ZplLabelPrinter` (host allow-list, address parsing, timeouts), `PdfPreviewLabelPrinter` (development only), `LabelPrinterRouter`. Test: `LabelPrinterTests` (including a real loopback TCP listener).
- [x] P9. Endpoints through `MapSecuredGroup`; no endpoint accepts a printer address. Test: `EndpointSecurityTests`; e2e `print-jobs`.
- [x] P10. Web feature `labelling/print-jobs`: store, service, `PrintQueuePageContainer` (self-refreshing), `JobTableComponent`, `ReprintFormComponent`. Test: `print-jobs.reducer.spec`, `job-table`, `reprint-form` specs; e2e `print-jobs`.
