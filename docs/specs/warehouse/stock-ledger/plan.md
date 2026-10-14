# Stock ledger: technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md) · **ADR:** [0012](../../../adr/0012-stock-ledger-and-balances.md)

## Domain model
- `VariantRef`: warehouse's copy of a variant (`VariantId` from Labelling, `StyleId`, `ColourId`, `SizeId`, `SkuCode`, `Gtin`, `GtinRetired`). Changed only by `VariantEventMapper` results: assign sets the active GTIN; void marks it retired.
- `StockGrade` (lookup `Accepted` | `Concession`) and `MovementKind` (lookup `Putaway`, `Adjustment`).
- `StockBalance` row: (`VariantRefId`, `GradeId`, `BinId`) → `Qty ≥ 0`. `StockMovement` (append-only): kind, signed non-zero `QtyChange`, `BalanceBefore`, `BalanceAfter`, `SourceId` (putaway record or adjustment id), actor, time. Invariant: `After = Before + QtyChange` and `After ≥ 0`.
- No aggregate loads balances into memory: the rules live in the posting port's SQL (ADR 0012).

## Application layer
| Use case | Covers |
|---|---|
| `IStockPosting.PostAsync(IReadOnlyList<StockPost>)` (published port, runs in the caller's transaction): atomic conditional balance change + ledger row + one `StockLevelChangedEvent` per touched variant | LED-1, LED-3, LED-4, LED-10 |
| `IStockVariantReader` (published port): resolve a variant by GTIN or id, with retirement flag | PUT-2.. (used by putaway/adjustments/counts) |
| `IBinStockQuery` implementation (units per bin) | BIN-4 |
| `VariantEventMapper` + `RecordVariantAssignedCommand` / `RecordVariantVoidedCommand` (inbox-guarded outbox handlers) | variant reference |
| `SearchStockQuery` (paged by variant; one query for the page's variants, one grouped query for their bins) | LED-5, LED-6, LED-8 |
| `GetBinContentsQuery` (paged) | LED-7 |
| `GetMovementsQuery` (paged, newest first, filter variant/bin/kind) | LED-9 |
| `StockConsistencyCheckQuery` (compares `SUM(movement)` with balances; used by tests and an admin endpoint) | LED-2 |

## API endpoints
| Method | Route | Policy |
|---|---|---|
| GET | `/api/warehouse/stock` | Staff |
| GET | `/api/warehouse/stock/{variantId}/bins` | Staff |
| GET | `/api/warehouse/stock/by-bin/{binId}` | Staff |
| GET | `/api/warehouse/stock/movements` | Staff |
| GET | `/api/warehouse/stock/consistency` | Admin |

No public write endpoint: stock is changed only by putaway, adjustments and cycle counts through the port.

## Events
Consumes `VariantGtinAssignedEvent`, `GtinVoidedEvent`. Publishes `StockLevelChangedEvent` v1 (variant id, style/colour/size ids, SKU, GTIN, `OnHand`, `Available` (= on hand), `OnHandAccepted`, `OnHandConcession`, `Reason` = kind code). A placeholder handler logs it until the Catalog read model exists (alerts add the first real handler).

## Data
`WHSE.VRNT_REF` (unique `VRNT_ID`; unique `GTIN`; index on `STYL_ID, CLR_ID, SIZE_ID`), `WHSE.STCK_BAL` (unique `VRNT_REF_ID, GRD_ID, BIN_ID`; `CK_STCK_BAL_QTY_NONNEG`; index on `BIN_ID`; FK indexes), `WHSE.STCK_MVMT` (append-only trigger `TR_STCK_MVMT_NO_CHNG`; indexes `VRNT_REF_ID, ID DESC` and `BIN_ID, ID DESC`; partitioning key `INSR_DTE` recorded in ADR 0012), lookups `GRD_LKP`, `MVMT_KIND_LKP` (seeded by migration). Audit columns and `xmin` on transactional tables. The posting SQL is raw with every identifier quoted.

## Frontend
Web feature `warehouse/stock-ledger` (container `StockPageContainer`, route `/warehouse/stock`): paged table per variant with grade columns; expandable row listing bins; bin search; a Movements tab with filters. NgRx store per CONVENTIONS.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | reads in `Staff`; consistency check in `Admin`; no write endpoint exists |
| Input validation | filters typed and bounded; ids positive; page size 1..100; posting port validates non-zero integer change and known bin/variant/grade |
| Output encoding | SQL parameterised, identifiers quoted; no HTML |
| Outbound calls | none |
| Resource limits | paged queries; a single posting accepts at most 500 lines |
| Concurrency | atomic conditional SQL per bin row; check constraint; ledger trigger; unique key on balance row |
| Idempotency | variant events guarded by inbox + unique `VRNT_ID`; posting itself is made idempotent by its callers' keys |
| Audit | every movement has actor, time, kind, source, before/after; immutable |
| Data protection | events carry ids, SKU, GTIN, quantities only |
| Secrets/config | none |
| Error responses | insufficient stock and unknown variant/bin are domain errors with fixed messages |
| New dependencies | none |
| Coupling | `Labelling.Contracts` events only, through `VariantEventMapper`; implements `bins`' `IBinStockQuery`; nothing reads `SKU.*` |
| Abuse cases | (1) a client posts a total or "balance after" value: the endpoint does not exist and the port ignores caller totals; (2) two pickers drain the last 3 units at once: one succeeds, one gets insufficient stock, balance stays ≥ 0; (3) someone with database access edits a movement: the trigger raises |

## Tests
Unit: `StockPost` validation. Integration (Testcontainers): 50 parallel negative postings against 30 units leave exactly 30 applied and never negative; ledger sum equals balance after random mixed operations; trigger blocks UPDATE/DELETE; variant events in either order; one event per variant per transaction; LED-4 end-to-end latency to the placeholder handler; statement count on the stock page (LED-8). Architecture: stock-ledger references no other feature but `bins`' port.
