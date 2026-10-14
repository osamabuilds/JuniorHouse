# Stock alerts: technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md) · **ADR:** [0012](../../../adr/0012-stock-ledger-and-balances.md)

## Domain model
- `StockAlert` (aggregate root): variant ref, `Level` (`Low` | `Out`), `Status` (`Open` | `Closed`), `OpenedAt`, `ClosedAt`, `AcknowledgedAt/By`. At most one Open alert per variant (partial unique index). Methods: `Escalate()` (Low → Out, same alert, ALR-2), `Close()`, `Acknowledge(person)`.
- `LowStockThreshold`: per-variant override (integer 0..100,000); effective threshold = override or `Warehouse:DefaultLowStockThreshold` (5).
- Pure `AlertPolicy.Evaluate(available, threshold, hasEverHadStock, current)` returns `Open(Low)`, `Open(Out)`, `Close`, or `NoChange`: ≤ threshold and > 0 → Low; = 0 → Out only if the variant has ever had stock (ALR-4); > threshold → Close.

## Application layer
| Use case | Covers |
|---|---|
| `StockLevelChangedHandler` (outbox handler for the module's own `StockLevelChangedEvent`, inbox-guarded, idempotent): applies `AlertPolicy`, opens/escalates/closes, publishes `StockAlertRaisedEvent` when an alert opens or escalates | ALR-1..4, ALR-7, ALR-9 |
| `SetThresholdCommand` (variant, threshold, acting person): validates, stores, re-evaluates from the ledger's current available quantity (`IStockVariantReader`) | ALR-6 |
| `AcknowledgeAlertCommand` (id, acting person) | ALR-5 |
| `SearchAlertsQuery` (paged; filter state Low/Out/Acknowledged), `CountOpenAlertsQuery` (navigation badge) | ALR-8 |
| `SearchThresholdsQuery` (paged variants with effective threshold, so the screen needs no N+1) | ALR-6 |

Because evaluation runs from the outbox, never inside the stock transaction, a failure in alerts is retried and dead-lettered by the dispatcher and never rolls back a stock change (ALR-9, NFR-FT-08). Events are processed in order per variant (the dispatcher claims oldest first); `AlertPolicy` is idempotent on the current state, so a redelivered or out-of-order event converges on the right state because it re-reads the current available quantity from the ledger rather than trusting the event's number alone.

## API endpoints
| Method | Route | Policy |
|---|---|---|
| GET | `/api/warehouse/alerts` | Staff |
| GET | `/api/warehouse/alerts/open-count` | Staff |
| POST | `/api/warehouse/alerts/{id}/acknowledge` | `Inventory` |
| GET | `/api/warehouse/alerts/thresholds` | Staff |
| PUT | `/api/warehouse/alerts/thresholds/{variantId}` | `Inventory` |

## Events
Consumes `StockLevelChangedEvent` (own module). Publishes `StockAlertRaisedEvent` v1 (variant id, SKU, level code, available, threshold). Placeholder logging handler until notifications exist (D6).

## Data
`WHSE.ALRT_THRS` (unique `VRNT_REF_ID`; `CK_ALRT_THRS_QTY_RANGE`), `WHSE.ALRT_MAIN` (partial unique on `VRNT_REF_ID` where `STS_ID` = Open; indexes `STS_ID, INSR_DTE`; ack columns), lookups `ALRT_LVL_LKP` (`LOW`, `OUT`), `ALRT_STS_LKP` (`OPEN`, `CLSD`). "Has ever had stock" is `EXISTS` on the ledger for the variant (indexed), not a stored flag. Audit columns and `xmin`.

## Frontend
Web feature `warehouse/stock-alerts` (container `StockAlertsPageContainer`, route `/warehouse/alerts`): paged alert table with state filter and Acknowledge button; thresholds tab with an inline editable value per variant (labelled input); a navigation badge (text count with an accessible name) polling `open-count` every 60 s while the admin app is open. NgRx store per CONVENTIONS.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | acknowledge and threshold edits in `Inventory`; reads in `Staff` |
| Input validation | threshold integer 0..100,000; ids positive; page size 1..100; acting person bounded |
| Output encoding | SQL parameterised, identifiers quoted; badge rendered as text |
| Outbound calls | none (delivery is out of scope, D6) |
| Resource limits | paged; badge endpoint is a single count query, cached 30 s in memory per host |
| Concurrency | partial unique index guarantees one open alert; conditional updates on status |
| Idempotency | inbox on the consumed event; policy converges on current state |
| Audit | threshold changes and acknowledgements record who and when; alerts are never deleted |
| Data protection | none |
| Secrets/config | default threshold validated on start |
| Error responses | problem details; unknown variant 404 |
| New dependencies | none |
| Coupling | consumes only its own module's event and the ledger's read interface; publishes an event for future notification wiring |
| Abuse cases | (1) a burst of movements creates an alert storm: one open alert per variant, state changes only on crossing (ALR-3); (2) a threshold of 0 or an absurd value: validated range, 0 allowed (alert only at stock-out); (3) alert evaluation throws on a poisoned event: dispatcher retries then dead-letters, stock is untouched |

## Tests
Unit: `AlertPolicy` matrix (crossing down, to zero, restock, never-stocked, threshold 0). Integration (Testcontainers): TC-STK-02 one alert on crossing, none on further movements; TC-STK-03 Out escalation and event with available 0; TC-STK-08 restock closes; replayed event creates nothing new; handler failure leaves the stock change committed (ALR-9); threshold edit re-evaluates; paging and no N+1; badge count. Architecture: depends only on the ledger's read interface.
