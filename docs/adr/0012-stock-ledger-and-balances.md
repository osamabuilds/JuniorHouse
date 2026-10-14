# 0012 — Stock ledger, bin balances and stock events

- Status: Accepted (Sprint 6, SCRUM-200). Consequence: the outbox dispatcher poll interval is now configuration (`Outbox:PollIntervalSeconds`, default 1 s, clamped 0.5–5 s) so stock changes reach consumers within the 5 s target (D8).
- Date: 2026-10-14

## Context

BRD §5.14 makes WMS the single source of truth for physical stock per SKU and bin, requires every stock change to be attributable and never a silent overwrite (NFR-SC-05), and requires the storefront's stock view to follow putaway within a few seconds (NFR-SC-06; owner decision D8: 5 s p95). `docs/db/naming.md` rule 6 says stock changes are append-only ledgers. Owner decision D2 makes the stock **grade** (`Accepted` | `Concession`) a first-class dimension. Reservations (FR-SC-09) and pick (FR-SC-10) will arrive later and will touch the same numbers concurrently with adjustments.

## Decision

- **Two tables, written together.** `WHSE.STCK_MVMT` is the append-only ledger (one row per change: variant, grade, bin, kind, signed quantity, balance before, balance after, source id, actor, time). `WHSE.STCK_BAL` holds the current quantity per (variant, grade, bin). Both are changed only through one in-process port, `IStockPosting`, inside the caller's transaction. No other code writes either table.
- **Balances are updated atomically in SQL, never read-modify-write.** A positive change is an upsert (`INSERT … ON CONFLICT (VRNT_REF_ID, GRD_ID, BIN_ID) DO UPDATE SET QTY = STCK_BAL.QTY + @d RETURNING …`). A negative change is `UPDATE … SET QTY = QTY - @d WHERE … AND QTY >= @d RETURNING …`; zero rows means insufficient stock and the whole transaction is refused. The row lock serialises concurrent changes to one bin, and `before`/`after` come from the same statement, so the ledger can never disagree with the balance. A `CHECK (QTY >= 0)` is the last line of defence (TC-STK-05).
- **The ledger cannot be edited.** A trigger raises on `UPDATE` and `DELETE` of `STCK_MVMT`; there is no code path that does either. A corrective change is a new row.
- **On-hand is derived.** A variant's on-hand quantity is the sum of its balances (indexed by variant); there is no separate total to drift. *Available = on-hand* until reservations exist; a `RSRV_QTY` is added with FR-SC-09 as an additive migration.
- **One stock event per variant per transaction.** `IStockPosting` collects the variants it touched and publishes one `StockLevelChangedEvent` per variant to the outbox (on-hand and per-grade quantities). `StockPutawayConfirmedEvent` is published by putaway in addition, because §9.10.3 names it and the storefront needs a "first time visible" signal.
- **Events are the integration surface.** Alerts consume the module's own `StockLevelChangedEvent` through the normal outbox handler path (so an alert failure is retried and dead-lettered on its own and can never roll back a stock change, NFR-FT-08). The future Catalog read model does the same from another module.
- **Idempotency.** Putaway and adjustment commands carry a client idempotency key with a unique index; a repeat returns the original result and posts nothing.
- **Growth.** `STCK_MVMT` is the table that will grow large. Its partitioning key is `INSR_DTE` (monthly range partitions, BRD §12.6). It is not partitioned yet, because at MVP volume it adds migration cost with no benefit; the primary key already includes nothing that would block adding it later.

## Consequences

- Concurrent putaways, adjustments and (later) reservations and picks serialise per bin row; there is no lost update and no negative balance.
- Reports and "stock by bin" read the balance table directly; history reads the ledger. A consistency test and an optional admin check compare `SUM(ledger)` with the balance.
- Storing balances plus a ledger is deliberate redundancy inside one module (a read-optimised current state next to its audit trail), written only by one port; it is not a 3NF violation across features. Recorded here as the denormalisation reason.
- Before/after on every row costs a few bytes but makes any row self-explanatory in a dispute.
