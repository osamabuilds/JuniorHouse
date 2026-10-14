# Putaway: technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md) · **ADR:** [0012](../../../adr/0012-stock-ledger-and-balances.md)

## Domain model
- `PutawayItem` (aggregate root): `SubBatchId`, `QcBatchId`, `PoId`, `StyleId`, `ColourId`, `SizeId`, `Grade` (from the QC outcome: `Accepted` → Accepted, `Concession` → Concession), `QcQty` (> 0), `PutQty` (≥ 0, ≤ `QcQty`), optional `VariantRefId`, `Status` (`AwaitingGtin` | `Pending` | `Complete`). Methods: `LinkVariant(variantRefId)` (AwaitingGtin → Pending, or Complete if already full), `Record(qty)` (fails if `qty ≤ 0` or `qty > Pending`), `Pending => QcQty − PutQty`. Unique on (`SubBatchId`, `SizeId`, `ColourId`).
- `PutawayRecord` (immutable): item, bin, quantity, `Scanned` flag (GTIN scanned vs chosen from the list, PUT-19), idempotency key, actor.

## Application layer
| Use case | Covers |
|---|---|
| `QcBatchAcceptedMapper` (anti-corruption): contract event → `RecordAcceptedBatchCommand`, or rejection reason | PUT-1, PUT-12 |
| `RecordAcceptedBatchCommand` (inbox-guarded outbox handler): validate, resolve style via `IPurchaseOrderQueries`, upsert items, link to existing `VariantRef` when present | PUT-1, PUT-2 |
| `LinkVariantToItemsCommand` (called when `stock-ledger` records a variant; via `IVariantLinkObserver` declared here, implemented in this feature and invoked by a second handler on the same event) | event-order safety |
| `ConfirmPutawayCommand` (item id, bin code, quantity, `scanned`, GTIN (when scanned), idempotency key, acting person): one transaction that checks the item, bin active (`IBinReader`), GTIN matches the item's variant and is not retired, quantity ≤ pending; then `IStockPosting` (+qty, kind `Putaway`), inserts the record, publishes `StockPutawayConfirmedEvent` | PUT-4..11, PUT-19..21 |
| `ScanLookupQuery` (GTIN → item(s) with pending, retirement status; one indexed query) | PUT-6, PUT-7, PUT-17 |
| `SearchPutawayItemsQuery` (paged; filter status; oldest first), `SearchPutawayRecordsQuery` (paged history) | PUT-14 |

Confirm sequence inside one transaction: lock the item row (`SELECT … FOR UPDATE`) → validate → `IStockPosting.PostAsync` → insert record → update `PutQty`/status → outbox. A repeated idempotency key short-circuits before any write and returns the original record.

## API endpoints
| Method | Route | Policy |
|---|---|---|
| GET | `/api/warehouse/putaway/items` | Staff |
| GET | `/api/warehouse/putaway/scan/{gtin}` | Staff (light query, p95 < 300 ms) |
| POST | `/api/warehouse/putaway/items/{id}/confirm` | Staff, `strict` rate limit |
| GET | `/api/warehouse/putaway/records` | Staff |

## Events
Consumes `QcBatchAcceptedEvent`, `VariantGtinAssignedEvent`, `GtinVoidedEvent` (the last two through the ledger's mapper; this feature adds a handler on the same events). Publishes `StockPutawayConfirmedEvent` v1 (variant id, style/colour/size ids, SKU, GTIN, grade code, quantity, `AvailableAfter`). Placeholder handler logs it until the Catalog read model (S7) consumes it.

## Data
`WHSE.PUT_ITEM` (unique `SUB_BTCH_ID, SIZE_ID, CLR_ID`; `CK_PUT_ITEM_PUT_QTY` between 0 and `QC_QTY`; indexes on `STS_ID, INSR_DTE` and `VRNT_REF_ID`; FK indexes), `WHSE.PUT_REC` (unique `IDEM_KEY`; indexes `PUT_ITEM_ID`, `BIN_ID`), lookup `PUT_STS_LKP` (`AWAITING_GTIN`, `PENDING`, `COMPLETE`; seeded). Audit columns and `xmin`. Quantities are `integer`.

## Frontend
Web feature `warehouse/putaway` (container `PutawayPageContainer`, route `/warehouse/putaway`): queue table "put away n of N" (`<th scope>`), a form with a focused GTIN input (Enter submits the lookup), a bin input (scan or choose from a list), quantity, confirm button; **"No tag yet? choose from the list"** switches to a select of pending items (PUT-19); inline messages that say which check failed; a history tab. One small request per scan and one per confirm (PUT-18); no data prefetch beyond the page. NgRx store per CONVENTIONS.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | `MapSecuredGroup(Staff)`; role checks arrive with S8 |
| Input validation | GTIN 13 digits with valid check digit (parsed strictly); bin code pattern; quantity positive integer ≤ pending; idempotency key a GUID; acting person 1..100 chars, no control characters |
| Output encoding | SQL parameterised, identifiers quoted; messages are fixed strings |
| Outbound calls | none |
| Resource limits | message at most 500 lines; paged lists; `strict` rate limit on confirm |
| Concurrency | item row lock + conditional balance SQL; `PutQty ≤ QcQty` check constraint; unique idempotency key |
| Idempotency | inbox + unique item key (PUT-2); idempotency key on confirm (PUT-8) |
| Audit | actor, time, bin, quantity, scanned/chosen on every record; records are immutable; mistakes are fixed by an adjustment (PUT-13) |
| Data protection | events carry ids, SKU, GTIN, grade, quantity only |
| Secrets/config | none |
| Error responses | problem details with a stable `code` per failed check (unknown GTIN, retired GTIN, inactive bin, over quantity) |
| New dependencies | none |
| Coupling | `Quality.Contracts`, `Vendor.Contracts` (style for a PO), `Labelling.Contracts` events only through mappers; reaches bins and the ledger through ports; architecture test TC-PUT-05 proves no module reads `WHSE.*` |
| Abuse cases | (1) replayed QC message cannot double the pending quantity (PUT-2); (2) a forged GTIN for another item or a retired one is refused (PUT-6/7); (3) two staff confirm the last units together: row lock makes one win, total never exceeds `QcQty` (PUT-9); (4) a double-tapped scanner submits twice: idempotency key applies once (PUT-8) |

## Open decision this plan surfaced
- **Q-PUT-2: Retired GTIN with unshelved stock.** If a GTIN is voided while an item is `Pending` (or after labelling but before putaway), D3 refuses the scan, and Sprint 5 only assigns a replacement GTIN when new accepted quantity arrives (S5 D9), so the item has nowhere to go. Options: (a) the item returns to `AwaitingGtin`, and Labelling gains a way to assign a new GTIN to an existing variant on request (a Labelling change, outside this sprint's modules); (b) putaway of a retired-GTIN item is allowed by list selection with a warning; (c) leave it: staff void a GTIN only when it holds no unshelved stock. **Default: (a) in the putaway state machine (item goes back to `AwaitingGtin` and is visible as a stuck item), and flag the Labelling follow-up for the owner.** This does not block implementation of the rest.

## Tests
Unit: item state machine (`Record`, `LinkVariant`), mapper rejections. Integration (Testcontainers): event arrival in both orders; duplicate message; confirm happy path moves balance, ledger, record and outbox together; over-quantity, inactive bin, unknown/retired GTIN refused; double submit with the same key applies once; 20 concurrent confirms never exceed `QcQty`; rollback leaves balance and counters unchanged; scan lookup latency (1,000 requests, p95 < 300 ms); outbox event reaches the placeholder handler within 5 s p95. Architecture: no Catalog/other-module reads of `WHSE`, one-way feature dependencies. Playwright e2e: scan flow with keyboard-wedge input, list-selection flow, throttled-network run.
