# Label work: technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md)

## Domain model
`LabelWorkItem` (aggregate root): `SubBatchId`, `QcBatchId`, `PoId`, `StyleId`, `SizeId`, `ColourId`, `Qty` (> 0), `Outcome` (Accepted | Concession), `Status` (Ready | NeedsAttention), `ParkedReason`, optional `VariantId`. Methods: `Park(reason)`, `MarkReady(variantId)`. Unique on `(SubBatchId, SizeId, ColourId)`. Labelled quantity is **derived** from printed jobs (port `IPrintedQuantityQueries`, implemented by print-jobs), never stored.

## Application layer
| Use case | Covers |
|---|---|
| `QcBatchAcceptedMapper` (anti-corruption): contract event → `RecordAcceptedBatchCommand`, or a rejection reason | LW-3, LW-6 |
| `RecordAcceptedBatchCommand` / handler (inbox-guarded outbox handler): validate, resolve style, upsert items, call `IVariantAssigner` | LW-1, LW-2, LW-4, LW-8 |
| `ResolveWorkItemCommand` (id, acting person) | LW-4 |
| `SearchLabelWorkQuery` (paged; filters status, style, `priceFlag`) | LW-5, LW-7 |

Ports declared here: `IVariantAssigner` (implemented by `variants`), `IPrintedQuantityQueries` (implemented by `print-jobs`), `ILabelStyleLookup` (adapts `Vendor.Contracts` + `Catalog.Contracts`).

## API endpoints
| Method | Route | Policy |
|---|---|---|
| GET | `/api/labelling/work-items` | Staff |
| POST | `/api/labelling/work-items/{id}/resolve` | Staff |

## Events
Consumes `QcBatchAcceptedEvent`. Publishes none of its own (GTIN events come from `variants`).

## Data
`SKU.LBL_WORK_ITEM` with lookups `LBL_WORK_STS_LKP`, `LBL_OUTC_LKP`; unique (`SUB_BTCH_ID`,`SIZE_ID`,`CLR_ID`); index on status + insert date for the worklist; FK index on `VRNT_ID`. Audit columns and concurrency token. The price-changed flag is computed per page from one `GetLabelInfoAsync` call plus one aggregate over `PRT_JOB`, with no per-row queries.

## Frontend
Web feature `labelling/label-work` (container `LabelWorklistPageContainer`, route `/labelling/worklist`): paged table (`<th scope>`), status shown as text, "Queue print" opens the dialog owned by `print-jobs`. NgRx store per CONVENTIONS.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | both endpoints in `MapSecuredGroup(Staff)`; no resource is user-owned (SEC-05 n/a); role checks arrive with S8 |
| Input validation | resolve takes an id and an acting-person name (1..100 chars, no control characters); list filters typed and bounded; page size 1..100 |
| Output encoding | n/a (no foreign-language output); parked reason is a fixed code plus a short system message, rendered as text |
| Outbound calls | none |
| Resource limits | a message creates at most 500 lines (else rejected as malformed); paged list; rate limit on resolve |
| Concurrency | the unique key makes duplicate delivery harmless; resolve uses a conditional update on status |
| Idempotency | inbox guard + unique key (LW-2) |
| Audit | `INSR_BY/UPDT_BY` + acting-person name on resolve; parked reason stored |
| Data protection | event carries ids and quantities only |
| Secrets/config | none |
| Error responses | problem details; unknown id gives 404 with no detail |
| New dependencies | none |
| Coupling | only `Quality.Contracts`, `Vendor.Contracts`, `Catalog.Contracts` (via `ILabelStyleLookup`); the mapper is the one file that knows QC's event; `variants` is reached through a port |
| Abuse cases | (1) a replayed or duplicated QC message cannot create tags twice (LW-2); (2) a forged or malformed event cannot consume GTIN numbers (LW-6 validates before assigning); (3) a huge message cannot exhaust the database (line cap) |

## Tests
Unit: mapper rejection cases, handler idempotency (Testcontainers for the unique key), resolve state machine, paging order and clamp. Architecture: no reference to `Variants` or `PrintJobs` internals.
