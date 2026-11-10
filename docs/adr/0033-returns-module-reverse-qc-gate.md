# 0033: Returns module and the reverse-QC gate

- Status: Accepted by delegation (owner away, 2026-11-10; to be confirmed)
- Date: 2026-11-10

## Context
Sprint 17 puts returned parcels through reverse QC before stock, money or exchange follows (FR-SC-18, FR-SC-19). Stock belongs to Warehouse, order data to Orders, courier events to Shipping (ADR 0002).

## Decision
1. **A new module `Returns`** (schema `RTRN`) owns return cases and their line decisions. It never writes another module's tables.
2. **A case opens from `ShipmentStatusChangedEvent` (RETURNED_TO_ORIGIN)** behind the inbox, reading the order lines once through `IOrderFulfilmentQueries`. The case keeps ids, quantities and codes only.
3. **Stock returns through a new Warehouse port `IStockReturns`**, built over Warehouse's adjustment poster (grade accepted or concession, found-stock reason, a deterministic idempotency key per case line), so every unit has a ledger row and a repeat does nothing.
4. **The gate is a state machine**: AwaitingReceipt, InInspection, Completed. An intact seal restocks at once and completes; otherwise every line is decided before completion.
5. **Completion publishes `ReturnQcCompletedEvent`**; it is the only start for refund, credit or exchange. This sprint builds the event, not the refund.
6. **The case has a kind** (today RTO) so customer returns can use the gate later.

## Alternatives considered
- Putting cases in Warehouse: rejected (warehouse would own order and courier concepts).
- Restocking by publishing an event for Warehouse to consume: rejected for now (the receiver needs to know at once if the bin is wrong, and a synchronous refusal is the better screen).

## Consequences
A fourth schema family for logistics, one migration, one Warehouse contract method, and a clear place for customer returns and refunds to attach later.
