# Print jobs: technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md) · **ADR:** [0010](../../../adr/0010-label-printing-queue-and-printer-port.md)

## Domain model
- `PrintJob` (aggregate root): `JobNo`, `Kind` (Original | Reprint), `VariantGtinId`, `WorkItemId?`, `Qty`, `Outcome` (for the "2ND" mark), `PriceSnapshot` (decimal), `PrinterId`, `Status` (Queued | Printing | Printed | Failed | Cancelled), `AttemptCount`, `NextAttemptAt`, `LastError`, reprint-only `ReasonCode` + `Note`, `RequestedByName`. State machine: Queued → Printing → Printed | Queued (retry) | Failed; Failed → Queued (manual retry); Queued/Failed → Cancelled. Owns `PrintAttempt` rows.
- `Printer`: `Code`, `Name`, `Format` (Zpl | Pdf), `Address` (host:port), `Active`. Seeded `FAKE-PDF`. Changed only by migrations or configuration seeding, not by the API.

## Application layer
| Use case | Covers |
|---|---|
| `QueuePrintJobCommand` (workItemId, qty, printer?, acting person) | PJ-1, PJ-8, PJ-11 |
| `ReprintTagCommand` (variantId, qty, reason, note, acting person) | PJ-5..PJ-8, PJ-11 |
| `RetryPrintJobCommand`, `CancelPrintJobCommand` | PJ-3 |
| `SearchPrintJobsQuery` (paged), `GetPrintHistoryQuery` (per variant, paged) | PJ-6, PJ-13 |
| `PrintWorker` (hosted service) + `ProcessDueJobHandler` | PJ-2, PJ-4, PJ-9 |
| `IPrintedQuantityQueries` (implements label-work's port) | label-work display |

Ports: `ILabelPrinter` (send a rendered document to a named printer, typed failures), `ITagDocumentBuilder` (declared here, implemented by `tags`), `IVariantLookup` (adapts `variants`), `IPrintClock`. Implementations in Infrastructure: `PdfPreviewLabelPrinter` (writes to `IFileStorage` under generated keys), `ZplLabelPrinter` (TCP, timeouts).

## API endpoints
| Method | Route | Policy |
|---|---|---|
| POST | `/api/labelling/work-items/{id}/print-jobs` | Staff |
| POST | `/api/labelling/variants/{id}/reprints` | Staff |
| GET | `/api/labelling/print-jobs` | Staff |
| POST | `/api/labelling/print-jobs/{id}/retry`, `/cancel` | Staff |
| GET | `/api/labelling/printers` | Staff |
| GET | `/api/labelling/work-items/{id}/tag-preview` | Staff, strict rate limit |

## Events
Publishes `TagJobPrintedEvent` v1. Consumes none.

## Data
`SKU.PRT_JOB` (unique `JOB_NO` via `SKU.JOB_NO_SEQ`; `PRIC_SNAP numeric(12,2)`; indexes on (`PRT_JOB_STS_ID`,`NXT_ATMP_DTE`) for the worker and on `VRNT_GTIN_ID`, `WORK_ITEM_ID`, `PRTR_ID`), `SKU.PRT_JOB_ATMP`, `SKU.PRTR_MAIN`, lookups `PRT_JOB_STS_LKP`, `PRT_JOB_KIND_LKP`, `RPRT_RSN_LKP`, `PRT_FMT_LKP`. Audit columns and concurrency token. Growth: revisit partitioning on `INSR_DTE` beyond about 5 million rows.

## Frontend
Web feature `labelling/print-jobs`: `PrintQueuePageContainer` (route `/labelling/queue`, polling with a bounded interval, Retry/Cancel buttons), the Queue-print and Reprint dialogs, tag preview (PDF in an `<iframe title=…>` or `<object>`). Status always shown as text.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | all endpoints in `MapSecuredGroup`; reprint and cancel declared `Admin`-ready; no endpoint changes printers |
| Input validation | counts 1..5,000 and integers; reason from the lookup; note ≤ 200 chars without control characters; acting person as in variants; ids are `long` route values |
| Output encoding | all text reaches ZPL only through the `tags` escaper (PJ-12, TAG-6); file keys for previews are generated GUIDs, never from input |
| Outbound calls | only to `Printer.Address` from the database, host:port allow-list from configuration, connect/send timeouts, one connection per job, no redirects or name resolution of request data (SSRF-safe) |
| Resource limits | 5,000 tags per job, 200 queued jobs per printer, attempt limit 4, backoff, paged lists, rate limits on preview and queue creation |
| Concurrency | `SKIP LOCKED` claim, conditional status updates, stuck-job recovery, unique job number |
| Idempotency | duplicate "queue print" clicks are guarded by remaining-quantity checks inside the transaction; retry is idempotent on a Queued job |
| Audit | job rows, attempts and reprint reasons are append-only history; cancel records who and when |
| Data protection | no personal data; error text from printers is trimmed to 500 chars and never includes the payload |
| Secrets/config | printer addresses are config, not secrets; the dev PDF printer is the only one seeded for non-production |
| Error responses | problem details; printer errors surface as a job status, not as an HTTP 500 |
| New dependencies | none: the PDF preview is written by hand (ADR 0010) |
| Coupling | `variants` and `tags` only through ports; printers behind `ILabelPrinter` with a fake; no knowledge of QC or label-work internals |
| Abuse cases | (1) a user mass-queues jobs to flood the printer or database: caps and rate limits; (2) a hostile product name contains ZPL commands: handled by the escaper; (3) an attacker tries to point the app at an internal host: addresses cannot come from requests |

## Tests
Unit: state machine, backoff schedule, limits. Integration (Testcontainers + fake printer that fails on demand): retry then Failed, one printer down while another prints, two workers never claim the same job, stuck-job recovery, reprint reuses GTIN and refuses Retired.
