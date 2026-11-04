# Stock reservations: plan (Sprint 12)

- **Spec:** [spec.md](spec.md) · **Module plan:** [../plan.md](../plan.md) · **Consumer:** [orders plan](../../orders/plan.md)
- **ADRs:** [0025 placement and reservation](../../../adr/0025-order-placement-and-stock-reservation.md) (the design; this plan implements its Warehouse half) · [0012 stock ledger](../../../adr/0012-stock-ledger-and-balances.md) · [0018 storefront read model](../../../adr/0018-storefront-module-read-model-and-edge.md) · [0004 outbox](../../../adr/0004-postgres-transactional-outbox.md)
- **Module:** `Warehouse`, schema `WHSE`, feature folder `Reservations/` (API), tests in `Romp.Modules.Warehouse.Tests`
- **Status:** Plan approved (2026-11-02); tasks written. Owner answers (2026-11-02): reserve at order placement only; card window 30 minutes; COD holds as long as the confirmation workflow; the ADR answers Q-RSV-4.

## Domain model (no EF, no ASP.NET)

- **`StockReservation`** aggregate: placement key (unique), status `HELD → CONFIRMED → CONVERTED`, or `HELD | CONFIRMED → RELEASED`; lines (variant reference, quantity); `HeldUntil` (the hold expiry, set only while `HELD`); order number once confirmed. Transitions are methods that return `Unchanged` when repeated, so a repeat call or a redelivered event is a no-op (RSV-4, RSV-5).
- **`ReservedBalance`**: reserved units per variant reference. **Available = on-hand − reserved**, where on-hand stays the sum of `STCK_BAL` over all bins and grades as the existing query defines it (LED-5); this plan does not change what counts as on-hand.
- **`ReservationRequest` / `ReservationResult`**: `Reserved(reservationId)` or `Short(list of variant id, requested, available)` (RSV-2).

## Application layer

| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `ReserveStockCommand` (`IStockReservations.ReserveAsync`) | placement key, lines | `Reserved` or `Short` | RSV-1, 2, 3, 4 |
| `ReleaseReservationCommand` (`ReleaseAsync`) | placement key | released once, or no-op | RSV-5 |
| `ConfirmReservationHandler` (event `OrderPlacedEvent`) | event via `OrderEventMapper` | `HELD → CONFIRMED`, order number stored | ADR 0025 step 7 |
| `ReleaseOnCancelHandler` (event `OrderCancelledEvent`) | event | `RELEASED`, available restored | RSV-5, RSV-6 |
| `ConvertReservationCommand` | placement key, pick id | hook defined, **built in S14** | RSV-9 |
| `SweepHeldReservationsJob` | n/a | releases `HELD` rows past `HeldUntil` (skips while the outbox backlog is older than the hold time) | RSV-6 sibling, ADR 0025 |
| `GetManyAvailabilityQuery` (`IStockAvailabilityQueries.GetManyAsync`) | variant ids (max 100) | availability = on-hand − reserved, one statement | CRT-4, FR-10 |
| existing `StockAvailabilityQueries` and the storefront events | | subtract reserved so listings and the read model follow | RSV-7 |

**Reserve algorithm** (one transaction): insert the reservation header keyed by the placement key (a unique violation means "already reserved": load and return it); sort the lines by variant reference id ascending; for each line run the single conditional statement of ADR 0025 against `STCK_RSRV_BAL` (insert-or-increase only where on-hand minus reserved covers the quantity); if any line returns no row, roll back and report every short line with its current availability (a second read after the rollback, so the answer is whole); otherwise insert the lines, bump `STCK_VER` for each variant and write one `StockLevelChangedEvent` per variant to the outbox (RSV-7). Release is the mirror: decrease `STCK_RSRV_BAL` in the same order, mark `RELEASED` only if it was `HELD` or `CONFIRMED`, emit the stock event.

**Guard on the other side** (RSV-8): `IStockPosting`, cycle-count approval and adjustments gain one check: a negative change that would take on-hand below reserved is refused (adjustment) or marked for review (cycle count) with the message "this would leave less stock than is promised to orders", never applied silently.

## API (internal, through Contracts only; no public HTTP endpoint)

| Method | Surface | Auth | Notes |
|---|---|---|---|
| `IStockReservations.ReserveAsync / ReleaseAsync` | `Warehouse.Contracts` | in-process; the caller is the Orders handler | ids and quantities only |
| `IStockAvailabilityQueries.GetManyAsync` | `Warehouse.Contracts` | in-process | bulk, max 100 ids, constant queries |
| Admin: a "reserved" column on stock-by-variant and a reservation list per variant | `GET /api/stock/variants/{id}/reservations` | existing `warehouse.read` | staff; shows order numbers, not customers |

## Events

| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|
| `StockLevelChangedEvent` v1 (existing; now reflects available = on-hand − reserved, higher version) | Warehouse | Storefront read model, notify-me, alerts | yes (existing) |
| `OrderPlacedEvent`, `OrderCancelledEvent` (`Orders.Contracts`) | Orders | Warehouse (one mapper, inbox) | yes |
| `ReservationReleasedEvent` v1 (variant ids only) | Warehouse | none yet (S14 put-back task) | yes |

## Data (schema `WHSE`, additive migration)

| Table | Notes |
|---|---|
| `STCK_RSRV` | `PLCE_KEY` unique, `RSRV_STS_ID` lookup (`HELD`, `CONF`, `CNVT`, `RLSD`), `HELD_UNTL_DTE`, `ORDR_NO` nullable, `INSR_DTE`; index on status plus `HELD_UNTL_DTE` for the sweeper |
| `STCK_RSRV_LINE` | `RSRV_ID` FK, `VRNT_REF_ID` FK to `VRNT_REF`, `QTY`; unique `(RSRV_ID, VRNT_REF_ID)`; check `QTY > 0` |
| `STCK_RSRV_BAL` | `VRNT_REF_ID` PK and FK, `RSRV_QTY` with check `>= 0`; written only by the reservation port, like `STCK_BAL` is written only by posting (ADR 0012) |
| `RSRV_STS_LKP` | standard lookup shape, seeded |

Audit columns and `xmin` on the header; foreign keys declared and indexed; the placement key is a plain value (no reference to `ORDR`). Reservations are short-lived, so no partitioning; confirmed and released rows are kept 12 months then purged in batches. Words reused: `STCK`, `VRNT`, `REF`, `QTY`, `STS`, `LKP`, `DTE`, `ORDR`, `NO`, `BAL`. New: `RSRV`, `HELD`, `UNTL`, `CNVT`, `RLSD` (checked against the glossary at implementation).

## NFR / security design

- **No overselling** (NFR-08, TC-CHK-17): the conditional statement is the guard, not application code; a stress test runs many concurrent reservations for one unit against PostgreSQL and expects exactly one success.
- **No deadlocks:** lines locked in ascending variant reference order inside one transaction.
- **No personal data:** requests and events carry placement keys, variant ids, quantities and an order number only (SEC-25).
- **Sweeper safety:** a hold is released only if still `HELD` past `HeldUntil` (default 30 minutes from `Warehouse:ReservationHoldMinutes`); confirmed reservations are never swept. If the outbox backlog is older than the hold time the sweeper waits, and a late `OrderPlacedEvent` that finds a released reservation tries to re-reserve and, if stock no longer allows, raises a staff alert instead of failing silently (ADR 0025).
- Configuration `Warehouse:ReservationHoldMinutes` (30) validated at start.

## Frontend

None new for customers (availability already flows through the read model). Admin: the existing stock screens show an extra "Reserved" and "Available" figure (admin keeps its look; read-only).

## Risks

- **What counts as on-hand.** Today it is every bin and grade (LED-5). If quarantined or rejected stock is ever kept in bins as sellable-looking balances, the reservation would promise it. The plan flags this to the owner for S14 and does not change it now.
- **Sweeper versus a long outage** is mitigated as above and has a dedicated test.
- **Integration tests cannot load locally built DLLs** on the developer machine (Application Control): the Linux SDK container and CI run them, and results say so.
