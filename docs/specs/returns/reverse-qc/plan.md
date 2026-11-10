# Reverse QC: technical plan (Sprint 17)

- **Spec:** [reverse-qc](spec.md) · **ADR:** [0033](../../../adr/0033-returns-module-reverse-qc-gate.md) · **Status:** applied by delegation (2026-11-10).

## Domain model (`Returns.Domain/ReverseQc`)
- **`ReturnCase`** (aggregate, `RTRN_MAIN`): order id, order number, kind (lookup `RTRN_KND_LKP`: RTO), status (lookup `RTRN_STS_LKP`: AwaitingReceipt, InInspection, Completed), seal intact (nullable), receiving bin code, received by and at, completed by and at, lines. Unique on `(ORDR_ID, RTRN_KND_ID)`.
- **`ReturnCaseLine`** (`RTRN_LINE`): variant id, SKU, name, colour and size (copied at opening so the screen needs no join), ordered quantity, decision (lookup `RTRN_DCSN_LKP`: Restock, Seconds, Reject; null until decided), quantity decided, note, decided by and at.
- **Rules:** received once; decide only in inspection; a line is decided once with 1 to ordered quantity; Reject needs a note of 1 to 200 characters; completion needs every line decided; completed is frozen.

## Cross-module contracts
| Contract | Owner | Purpose |
|---|---|---|
| `ShipmentStatusChangedEvent` (existing) | Shipping.Contracts | RETURNED_TO_ORIGIN opens a case |
| `IOrderFulfilmentQueries.FindAsync` (existing) | Orders.Contracts | the order's lines (variant, SKU, name, colour, size, quantity); only ids and quantities are kept |
| `IStockReturns.RestockAsync(RestockRequest)` (new) | Warehouse.Contracts | adds returned units to a bin as sellable or seconds stock, idempotent on a key; refuses an unknown or inactive bin with a plain result |
| `ReturnQcCompletedEvent` (new) | Returns.Contracts | the only trigger for refund, credit or exchange (FR-SC-19) |

## Application layer (MediatR)
| Command / query | Covers |
|---|---|
| `OpenReturnCaseCommand` (from the event) | RQC-1, 13 |
| `ReceiveReturnCaseCommand` | RQC-2, 3, 9, 10 |
| `DecideReturnLineCommand` | RQC-4 to 6, 9, 10 |
| `CompleteReturnCaseCommand` | RQC-7, 8 |
| `ListReturnCasesQuery` (paged, status filter), `GetReturnCaseQuery` | RQC-11 |

## API (`shipping.view` read, `shipping.ops` act)
| Method | Route |
|---|---|
| GET | `/api/returns/cases`, `/api/returns/cases/{id}` |
| POST | `/api/returns/cases/{id}/receive`, `/api/returns/cases/{id}/lines/{lineId}/decide`, `/api/returns/cases/{id}/complete` |

## Data (`RTRN`, per [docs/db/naming.md](../../../db/naming.md))
`RTRN_MAIN`, `RTRN_LINE`, lookups `RTRN_KND_LKP`, `RTRN_STS_LKP`, `RTRN_DCSN_LKP`; unique `(ORDR_ID, RTRN_KND_ID)`; indexes on every FK and on `(RTRN_STS_ID, INSR_DTE)` for the queue. Growth: cases are rare; no partitioning.

## Restock order of work
Restocking and the line decision must not disagree. Warehouse is called first with the deterministic idempotency key `case:{id}:line:{lineId}`; the decision is saved after it succeeds. A crash between the two leaves the line undecided, and deciding it again restocks nothing twice (the key repeats) and saves the decision (RQC-9, RQC-10).

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | `shipping.view` / `shipping.ops` on every route; rows in the authorization matrix |
| Input validation | bin code, note, quantity and ids are validated; unknown ids answer 404 |
| Idempotency | unique case per order and kind; deterministic stock idempotency key; inbox for the event |
| Data protection | ids, quantities and codes only (RQC-13) |
| Coupling | contracts only; `Returns` added to `ModuleNames` |
