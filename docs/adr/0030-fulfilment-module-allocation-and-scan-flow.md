# 0030: Fulfilment module, allocation in Warehouse, and the scan flow

- Status: Accepted by delegation (the owner was away and asked for the next sprint to continue; to be confirmed), 2026-11-09
- Date: 2026-11-09

## Context
Sprint 14 turns a confirmed or paid order into a packed parcel: allocate stock to shelf locations, pick by scanning, flag exceptions, pack, weigh, print a slip. Stock and bins belong to Warehouse (ADR 0012); orders to Orders (ADR 0024); modules may not read each other's tables (ADR 0002). The BRD asks for scan-to-confirm under 300 ms on cheap devices and a weak network (NFR-SC-01, NFR-SC-07).

## Decision
1. **A new module `Fulfilment`** (schema `FULF`) owns the fulfilment order, the pick list, exceptions, the pack record and the scan log. It starts on `OrderConfirmedEvent` or `OrderPaidEvent` and reads other modules only through contracts.
2. **Warehouse owns allocation and the decrement.** A new contract `IStockAllocator` allocates each line to bins oldest putaway first, under bin row locks taken in a fixed order, idempotent on an order key; it releases on cancellation; and `ConfirmPickAsync` writes the ledger decrement once and converts the reservation (the RSV-9 hook). Fulfilment stores a snapshot of the result. On-hand stock is unchanged until a pick is confirmed.
3. **A scan is cheap by construction.** The pick list is loaded once; a scan is an indexed read by bin code or GTIN against that order's allocation rows plus one update guarded by the allocation's quantities. No cross-module call per scan.
4. **Exceptions never overwrite stock.** A picker's exception holds the line, raises an event for Warehouse to flag the bin and create a count task, and tells the customer through Notifications. Correcting stock is a normal, audited adjustment (BRD §5.14).
5. **Scanner input is keyboard text** and the screens are part of the admin app, installable as a PWA, behind the existing staff sign-in with new permissions.
6. **Business numbers are settings,** not code: the poly-bag limits, the weight tolerance and the garment weights (Q-FUL-1 to 3).

## Alternatives considered
- Fulfilment reading Warehouse tables: rejected (module rule).
- Allocating inside Fulfilment from a stock-by-bin query: two writers to stock truth, and races between orders; rejected.
- A separate native app or a separate web app for the warehouse: more to install and maintain; rejected for a small team (NFR-SC-07).

## Consequences
Warehouse gains a second public contract and a bin "under review" flag. Fulfilment adds a schema, a migration and permissions. The scan log is high volume and is purged on a schedule. Batch picking and multi-parcel orders are later changes to the same model.
