# Cycle counts: technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md)

## Domain model
`CycleCount` (aggregate root): business number `CC-yyyy-nnnn` (ADR 0006 sequence `CNT_NO_SEQ`), `Status` (`Open` → `Closed` | `Cancelled`), bins (`CountBin`: bin id, `OpenMovementMark` = the highest ledger movement id for that bin when the count was created), lines (`CountLine`: bin, variant ref, grade, `CountedQty`, and on submit `SystemQty`, `Variance`, `ReasonCode`, `AdjustmentId`). Methods: `Submit(counts)` records counted quantities, `ComputeVariances(systemQtys, changedBins)` (pure), `Confirm(reasons)` (refused while any variance line has no reason or any line is flagged changed), `Cancel()`. Invariants: a bin belongs to at most one Open count (partial unique index, CC-3); counted quantities are non-negative integers; a retired GTIN may not be counted as present (CC-10).

## Application layer
| Use case | Covers |
|---|---|
| `CreateCycleCountCommand` (bin ids or a zone, acting person): refuses a bin already in an Open count, records each bin's movement mark | CC-1, CC-3 |
| `SubmitCountLinesCommand` (count id, lines with GTIN/variant, grade, counted qty): upsert lines; **system quantities are not returned** | CC-2, CC-6, CC-10 |
| `ReviewCountQuery`: after submission returns system qty, variance and "changed since count opened" per line (movement ledger newer than the bin's mark) | CC-4, CC-7 |
| `ConfirmCycleCountCommand` (count id, reason per variance line, idempotency key): in one transaction re-checks staleness, calls `IAdjustmentPoster` once per variance line (`Reconfirmed` carried through for large variances), sets the bins' `LAST_CNT_DTE` through `IBinCountMarker`, closes the count | CC-5, CC-8, CC-12 |
| `CancelCycleCountCommand` | CC-9 |
| `SearchCycleCountsQuery`, `GetCycleCountQuery` (paged lines), `BinCountAgeQuery` (days since last counted per bin, paged) | CC-11 |

Staleness (CC-7) uses the movement mark: any `STCK_MVMT` row for that bin with an id above the mark means the bin moved after the count opened, so lines for that bin are flagged and the count is re-opened for those lines (counted quantities cleared, the counter recounts). No bin freeze (open question Q-CC-1 default).

## API endpoints
| Method | Route | Policy |
|---|---|---|
| GET | `/api/warehouse/cycle-counts` | Staff |
| GET | `/api/warehouse/cycle-counts/{id}` | Staff (hides system quantities until review) |
| GET | `/api/warehouse/cycle-counts/bin-age` | Staff |
| POST | `/api/warehouse/cycle-counts` | `Inventory` |
| PUT | `/api/warehouse/cycle-counts/{id}/lines` | Staff |
| GET | `/api/warehouse/cycle-counts/{id}/review` | `Inventory` |
| POST | `/api/warehouse/cycle-counts/{id}/confirm` | `Inventory`, `strict` rate limit |
| POST | `/api/warehouse/cycle-counts/{id}/cancel` | `Inventory` |

## Events
None of its own; adjustments publish through `IStockPosting`.

## Data
`WHSE.CNT_MAIN` (unique `CNT_NO`; index `STS_ID, INSR_DTE`), `WHSE.CNT_BIN` (unique `CNT_ID, BIN_ID`; **partial unique** on `BIN_ID` where the parent count is Open, enforced by a status copy column `OPEN_IND` in this table kept in step by the aggregate; `OPEN_MVMT_MARK` bigint), `WHSE.CNT_LINE` (unique `CNT_ID, BIN_ID, VRNT_REF_ID, GRD_ID`; `ADJ_ID` FK to `ADJ_MAIN`; reason FK to `ADJ_RSN_LKP`), `WHSE.CNT_NO_SEQ`, lookup `CNT_STS_LKP`. Audit columns and `xmin`.

## Frontend
Web feature `warehouse/cycle-counts` (container `CycleCountsPageContainer`, routes `/warehouse/cycle-counts` and `/warehouse/cycle-counts/:id`): list with "days since counted" bin picker; count sheet with a focused GTIN input, grade select and quantity (system quantity not shown); review step with variance, reason select per line, and the large-variance confirmation. NgRx store per CONVENTIONS.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | create/review/confirm/cancel in `Inventory`; counting lines in `Staff`; system quantities never sent before review (CC-2 is enforced server-side, not only in the UI) |
| Input validation | counted quantity integer 0..100,000; at most 200 bins and 2,000 lines per count; GTIN strict; reason from lookup; acting person bounded |
| Output encoding | SQL parameterised, identifiers quoted |
| Outbound calls | none |
| Resource limits | caps above; paged lists; `strict` rate limit on confirm |
| Concurrency | partial unique index prevents two Open counts on a bin; staleness check and adjustment posting in one transaction; conditional status update |
| Idempotency | confirm carries an idempotency key; a confirmed count is not re-confirmable |
| Audit | count creator, counter, confirmer, times; each variance becomes an audited adjustment linked to the line |
| Data protection | none |
| Secrets/config | none |
| Error responses | problem details with codes `bin-in-open-count`, `reason-required`, `stale-lines`, `count-closed` |
| New dependencies | none |
| Coupling | `adjustments` via `IAdjustmentPoster`; `bins` via `IBinReader` and `IBinCountMarker`; `stock-ledger` via `IStockVariantReader` and a system-quantity query port |
| Abuse cases | (1) a counter peeks at system quantity through the API before submitting: the endpoint omits it until the review step, which needs `Inventory` and requires submitted lines; (2) stock moves during the count: stale lines force a recount instead of adjusting against old numbers; (3) confirming twice: idempotency key and Closed status stop a second set of adjustments |

## Tests
Unit: variance computation, state machine, staleness mark logic. Integration (Testcontainers): create/submit/review/confirm creates one adjustment per variance and none for zero variance; reason required; second Open count on a bin refused; movement after open flags the line; found variant not expected in the bin (CC-6); cancel changes nothing; confirm idempotent; blind-count API test (no system qty in responses before review); paging and no N+1. Playwright: counting with keyboard-wedge input.
