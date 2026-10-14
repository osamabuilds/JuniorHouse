# Adjustments: technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md) · **ADR:** [0012](../../../adr/0012-stock-ledger-and-balances.md)

## Domain model
`StockAdjustment` (immutable record, no state machine): variant ref, grade, bin, signed non-zero `QtyChange`, reason (lookup `DAMAGE`, `MISCOUNT`, `THEFT`, `FOUND_STOCK`), note (≤ 200 chars), `Reconfirmed` flag, optional `CycleCountLineId` (set by cycle-counts), idempotency key, actor. Rules in a pure `AdjustmentPolicy`: `FOUND_STOCK` must be positive; `DAMAGE` and `THEFT` must be negative; `MISCOUNT` either (ADJ-3); `|change| > LargeAdjustmentUnits` requires `Reconfirmed` (ADJ-4b, applies to all reasons including a `FOUND_STOCK` raised from a putaway over-receipt, D11).

## Application layer
| Use case | Covers |
|---|---|
| `PostAdjustmentCommand` (GTIN or variant id, bin code, grade, change, reason code, note, `reconfirmed`, idempotency key, acting person): validates, applies `AdjustmentPolicy`, checks bin active and variant known (positive change on a retired GTIN refused, negative allowed, ADJ-9), calls `IStockPosting` (kind `Adjustment`), stores the record | ADJ-0..9 |
| `IAdjustmentPoster` (published port, same logic without HTTP) used by cycle-counts so a count's variances create ordinary adjustments in the count's transaction | CC-5 |
| `SearchAdjustmentsQuery` (paged; filters reason, variant, date range) | ADJ-10 |
| `ListReasonsQuery` | ADJ-2 |
| `AdjustmentOptionsValidator` (`ValidateOnStart`: large-adjustment limit ≥ 1) | config |

If `|change|` exceeds the limit and `Reconfirmed` is false, the API returns a problem with code `reconfirm-required` and the limit, and the screen shows a confirmation step ("You are changing 25 units. Confirm.") before resending with `reconfirmed: true`. The same-person re-confirm is deliberate (D5).

## API endpoints
| Method | Route | Policy |
|---|---|---|
| GET | `/api/warehouse/adjustments` | Staff |
| GET | `/api/warehouse/adjustments/reasons` | Staff |
| POST | `/api/warehouse/adjustments` | `Inventory` (capability policy; allow-all until S8), `strict` rate limit |

## Events
None of its own; `IStockPosting` publishes `StockLevelChangedEvent`.

## Data
`WHSE.ADJ_MAIN` (unique `IDEM_KEY`; `CK_ADJ_MAIN_QTY_CHNG_NONZERO`; indexes `VRNT_REF_ID, ID DESC`, `ADJ_RSN_ID`, `BIN_ID`, `INSR_DTE`; FK indexes), lookup `ADJ_RSN_LKP` (4 rows, seeded; `ACT_IND` allows retiring or adding codes without a deployment, D12). Audit columns; no update or delete path.

## Frontend
Web feature `warehouse/adjustments` (container `AdjustmentsPageContainer`, route `/warehouse/adjustments`): paged history with filters; "New adjustment" form (GTIN scan/select variant, bin, grade, signed quantity, reason select, note) with an explicit confirmation step above the limit. NgRx store per CONVENTIONS.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | write endpoint in the `Inventory` group (TC-STK-06: policy asserted by the architecture test now, a real 403 test in S8); reads in `Staff` |
| Input validation | integer non-zero change; reason from lookup; direction rules; note ≤ 200 chars, control characters rejected; bin pattern; GUID key |
| Output encoding | SQL parameterised, identifiers quoted; notes rendered as text |
| Outbound calls | none |
| Resource limits | paged list; `strict` rate limit; change magnitude capped at 100,000 |
| Concurrency | conditional balance SQL via the ledger; unique idempotency key (TC-STK-07) |
| Idempotency | idempotency key (ADJ-8) |
| Audit | actor, time, reason, note, before/after on the movement; records immutable (ADJ-6) |
| Data protection | none |
| Secrets/config | limit validated on start |
| Error responses | problem details with codes `reason-required`, `direction-invalid`, `insufficient-stock`, `reconfirm-required`, `bin-inactive` |
| New dependencies | none |
| Coupling | reaches the ledger and bins through ports; exposes `IAdjustmentPoster` for cycle-counts; no event of its own |
| Abuse cases | (1) a sequence of small negative adjustments to hide theft: each is audited with actor and reason, and the history filter by reason/actor makes it visible; the large-adjustment rule does not cover this, which is accepted at MVP; (2) re-sending a confirmed large adjustment: idempotency key applies once; (3) a client sets `reconfirmed: true` without the UI step: the server cannot tell, so the rule is a guard against accidents, not fraud (documented, D5) |

## Tests
Unit: `AdjustmentPolicy` matrix (reason × direction × magnitude). Integration (Testcontainers): −3 with reason updates balance and writes before/after (TC-STK-01); missing reason refused; below zero refused and the check constraint also holds when bypassed in SQL (TC-STK-05); 30 concurrent adjustments and putaways on one bin lose no update (TC-STK-07); large change needs reconfirm (TC-STK-09); idempotent resend; paging and no N+1. Architecture: write endpoint in the `Inventory` group.
