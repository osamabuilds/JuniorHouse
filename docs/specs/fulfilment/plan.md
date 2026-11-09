# Fulfilment: technical plan (Sprint 14)

- **Specs:** [allocations](allocations/spec.md), [pick-lists](pick-lists/spec.md), [pick-exceptions](pick-exceptions/spec.md), [pack-station](pack-station/spec.md), [packing-slips](packing-slips/spec.md), [warehouse-app](warehouse-app/spec.md)
- **Module:** new `Fulfilment` (schema `FULF`), plus small additions to `Warehouse.Contracts`, `Orders.Contracts`, `Notifications`, the admin app and Identity permissions. ADR: [0030](../../adr/0030-fulfilment-module-allocation-and-scan-flow.md).
- **Status:** Draft, applied by delegation while the owner is away (2026-11-09).

## Domain model (`Fulfilment.Domain`)
- **`FulfilmentOrder`** (aggregate): one per confirmed or paid order (unique on order id). Status: Queued → Picking → Picked → Packing → Packed (also Held when every line is delayed, Cancelled). Holds `Lines`, the picker and pack claim (who, when), the cash to collect (cash on delivery only), and the pack record.
- **`FulfilmentLine`**: a snapshot of what to ship (Labelling variant id, SKU, GTIN, name, colour, size, quantity, unit price). Status: Allocated, AwaitingRestock, Picked, Packed.
- **`Allocation`**: a bin-located claim for a line (bin id, bin code, walking-order key, quantity, picked quantity, packed quantity). A line may have several (ALC-4).
- **`PickException`**: line, bin, reason, system count, found count, staff, time, status (Open, Resolved).
- **`PackRecord`**: weight in grams, expected weight, packaging, deviation flag, note, staff, time.
- **Rules (invariants):** picked ≤ allocated per allocation and a scan can never exceed it (PCK-5); a line is Picked only when its allocations are complete; an order is Picked only when every non-delayed line is Picked; pack scans are counted separately from pick scans; only a Picked order can be packed (PAK-10); a state change that is a repeat is a no-op (ALC-5, PCK-8, PAK-13). `PackagingRule` and `WeightCheck` are pure functions (Q-FUL-1, Q-FUL-2).

## Cross-module contracts
| Contract | Owner | Purpose |
|---|---|---|
| `IStockAllocator.AllocateAsync(orderKey, lines)` | Warehouse.Contracts (new) | Allocates each line to bins, oldest putaway first, under bin row locks; idempotent on the order key; returns bin, quantity, GTIN, SKU, walking-order key per allocation, and short lines (ALC-3, 4, 8, 9) |
| `IStockAllocator.ReleaseAsync(orderKey)` | same | Drops the allocation (ALC-6) |
| `IStockAllocator.ConfirmPickAsync(orderKey, picks, staff)` | same | Decrements each picked unit from its bin in the stock ledger once and converts the reservation (PCK-7, RSV-9); idempotent |
| `IStockAllocator.FlagBinAsync(binId, reason)` | same | Marks a bin "under review" so it is not allocated from (PEX-6) |
| `IOrderFulfilmentQueries.FindAsync(orderId)` | Orders.Contracts (new) | The order's lines, totals, payment method and delivery city/name for the slip: read at the moment of need, never in an event (ADR 0026) |

## Application layer (`Fulfilment.Application`, MediatR)
| Command / query | Covers |
|---|---|
| `CreateFulfilmentOrderCommand` (from `OrderConfirmedEvent` / `OrderPaidEvent`) | ALC-1, 2, 5, 8, 11 |
| `CancelFulfilmentOrderCommand` (from `OrderCancelledEvent`) | ALC-6, 7 |
| `ListPickQueueQuery` (paged), `GetPickListQuery` | PCK-1, 11, APP-8 |
| `ClaimPickCommand`, `ReleasePickClaimCommand` | PCK-9 |
| `ScanPickCommand(orderId, binCode or gtin)` | PCK-2 to 6, 10, 12 |
| `CompletePickCommand` | PCK-7, 8 |
| `RaiseExceptionCommand`, `ResolveExceptionCommand`, `ListExceptionsQuery` | PEX-1 to 10 |
| `OpenPackCommand`, `ScanPackCommand`, `RecordWeightCommand`, `CompletePackCommand` | PAK-1 to 13 |
| `GetPackingSlipQuery`, `PrintPackingSlipCommand` | SLP-1 to 8 |

## API endpoints (all behind staff sign-in; permission checked on every request, SEC-08)
| Method | Route | Permission |
|---|---|---|
| GET | `/api/fulfilment/queue` | `fulfilment.view` |
| GET | `/api/fulfilment/orders/{id}` | `fulfilment.view` |
| POST | `/api/fulfilment/orders/{id}/pick/claim`, `/pick/release`, `/pick/scan`, `/pick/complete`, `/pick/exceptions` | `fulfilment.pick` |
| GET, POST | `/api/fulfilment/exceptions`, `/exceptions/{id}/resolve` | `fulfilment.view`, `fulfilment.manage` |
| POST | `/api/fulfilment/orders/{id}/pack/open`, `/pack/scan`, `/pack/weight`, `/pack/complete` | `fulfilment.pack` |
| GET, POST | `/api/fulfilment/orders/{id}/slip`, `/slip/print` | `fulfilment.pack` |

Every new route gets a line in `authorization-matrix.csv` and a test. No route takes a customer id; the staff member comes from the session, never the body.

## Events
| Event | Published by | Consumed by |
|---|---|---|
| `OrderConfirmedEvent`, `OrderPaidEvent`, `OrderCancelledEvent` (existing) | Orders | Fulfilment (behind the inbox) |
| `PickCompletedEvent` | Fulfilment | (S15 / reporting) |
| `OrderLineDelayedEvent` | Fulfilment | Notifications (a "your item is delayed" message, Q-FUL-5) |
| `OrderPackedEvent` | Fulfilment | S15 shipping |
| `PickExceptionRaisedEvent` | Fulfilment | Warehouse (a cycle-count task / bin flag) |

Payloads carry ids, the order number and codes only (SEC-25).

## Data (`FULF`, names per [docs/db/naming.md](../../db/naming.md); new words go in the glossary with the migration)
`FULF_ORDR`, `FULF_LINE`, `FULF_ALOC`, `FULF_EXCP`, `FULF_PACK`, `FULF_SCAN` (a compact scan log, not audited, purged after 90 days), lookups `FULF_STS_LKP`, `LINE_STS_LKP`, `EXCP_RSN_LKP`, `PACK_KIND_LKP`, and the setting table `FULF_WGHT_CATG` (Q-FUL-3). Unique: fulfilment order per order id; allocation per line and bin. Indexes on every FK, on `(status, queued time)` for the queue, and on `(order id, bin code)` and `(order id, GTIN)` for scans (NFR-SC-01). `xmin` concurrency token on the aggregate. Large tables (scan log) note their partition key: month of scan.

## NFR and security
- **Scan speed (PCK-6):** the pick list is loaded once into a small projection; a scan is two indexed reads and one update. A test asserts under 300 ms and counts queries.
- **Exactly once:** the pick claim, the picked quantity and the stock decrement are guarded by row locks and unique keys; completion is idempotent on the fulfilment order.
- **No personal data in events or logs; the pick list shows only the order number; the slip is built from Orders at print time.**
- **Audit (NFR-SC-05):** every scan, exception, weight and completion records the staff member and time.

## Frontend (admin app, `features/fulfilment/...`)
`pick-queue`, `pick-list`, `pack-station`, `exceptions`, `packing-slip`. NgRx feature stores, paged lists, a focused scan box, large result banners, a web manifest. Follows [CONVENTIONS](../../CONVENTIONS.md).

## Risks and alternatives
- Weights are guesses until real data exists (Q-FUL-3): the check is a safety net, tolerances are settings.
- Allocation and the reservation can drift if stock changes during pick; handled by exceptions (ALC-8) not by silent correction.
- Alternative considered: Fulfilment reading Warehouse tables. Rejected (module rule).

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | Permission per route, checked server-side on every request; authorization matrix updated and tested |
| Input validation | Scan codes, weights and counts are length- and range-checked; DTOs are not entities |
| Ownership (SEC-05) | No customer data is reachable; staff identity from the session |
| Idempotency | Events behind the inbox; scans and completions are idempotent |
| Data protection | Ids and codes in events; order number only on the pick list; slip reads Orders at print time; scan log not audited and purged |
| Coupling | Contracts only; the new module is added to `ModuleNames` |
| Secrets | None new |
