# 0025 — Order placement and stock reservation across modules

- Status: Proposed (answers Q-RSV-4; approval comes with the plans)
- Date: 2026-11-02

## Context

Placing an order must (FR-28, FR-29, NFR-08) never oversell, never create two orders for one submit, and leave nothing half done if a step fails. The order lives in `Orders` (`ORDR`), the stock in `Warehouse` (`WHSE`), the discount in `Cart` (`CART`). Each module has its own DbContext and the rules forbid cross-schema access, so one database transaction cannot span them without breaking the boundaries. The owner decided stock is reserved at placement, not at add-to-cart (Q-CRT-1, Q-RSV-1).

## Decision

**A placement key and a hold-then-confirm protocol.**

1. The browser sends an `Idempotency-Key` (a random value created when the checkout page opens). The server derives the **placement key** from it plus the caller (customer id or guest cart), so one person cannot collide with another's key.
2. `Orders` first looks for an order with that placement key (unique index `UQ_ORDR_MAIN_PLCE_KEY`). Found: return it (ORD-1).
3. `Orders` asks `Cart.Contracts.ICheckoutPricing` for the priced cart. Every price, discount, shipping fee and total comes from this call (SEC-09); nothing the client sent is read.
4. **Hold the discount** (if a code is applied): `Cart.Contracts.IDiscountRedemptions.HoldAsync(placementKey, …)`. It locks the code row, checks cap, per-customer limit, expiry and minimum spend, and writes a `Held` redemption in its own transaction. Refused: stop, nothing else exists yet. (Cheapest to fail first.)
5. **Reserve stock**: `Warehouse.Contracts.IStockReservations.ReserveAsync(placementKey, lines)`. All lines or none (RSV-1, RSV-2). It is idempotent on the key (RSV-4). Refused: release the discount hold (best effort), return the short variants.
6. `Orders` writes the order, lines (unit price snapshot), contact and address snapshot, first status-history row, payment row and the `OrderPlacedEvent` to the outbox in **one transaction of its own**. A unique violation on the placement key means a concurrent twin won: load and return its order.
7. **Confirm by event.** `Warehouse` and `Cart` consume `OrderPlacedEvent` (inbox, idempotent) and move their `Held` records to `Confirmed`; `Cart` also empties the cart named in the event. A reservation or redemption that is still `Held` after `HoldMinutes` (default 30) is released by a sweeper in its own module (RSV-6 sibling), because no order ever claimed it.
8. **Release by event.** `OrderCancelledEvent` (staff cancel, COD refused or not confirmed, customer cancel, card payment window expired) makes `Warehouse` release the reservation and `Cart` release the redemption, once each (idempotent on the placement key).
9. **Best-effort compensation**: if step 6 fails for any reason other than the twin case, `Orders` calls `ReleaseAsync(placementKey)` on both. If that call also fails, the sweeper in step 7 clears it within `HoldMinutes`.

**The reservation itself (Warehouse).** `WHSE.STCK_RSRV` (one per placement key: status Held, Confirmed, Released, Converted) and `WHSE.STCK_RSRV_LINE` (variant, quantity), plus `WHSE.STCK_RSRV_BAL` (reserved quantity per variant). Available = on-hand − reserved. Reserving a variant is one conditional statement, never read-modify-write (the pattern of ADR 0012):

`INSERT INTO "WHSE"."STCK_RSRV_BAL" (…) SELECT @v, @q WHERE <on-hand of v> >= @q ON CONFLICT (VRNT_REF_ID) DO UPDATE SET RSRV_QTY = STCK_RSRV_BAL.RSRV_QTY + @q WHERE <on-hand of v> - STCK_RSRV_BAL.RSRV_QTY >= @q RETURNING …`

Lines are processed in ascending variant id inside one transaction, so two orders can never deadlock; if any line returns no row, the transaction rolls back and the short variants are reported. Any ledger or adjustment posting that would take on-hand below reserved is refused or flagged for review (RSV-8). Every reserve, release and conversion emits `StockLevelChangedEvent` with a higher version, so the storefront follows within the usual 5 s (ADR 0012, ADR 0018).

## Alternatives considered

- **One shared transaction across schemas** (one connection, three contexts). Works technically, defeats ADR 0002 and the "module owns its tables" test, and couples the release of three modules.
- **Reserve inside the order transaction by writing `WHSE` tables from `Orders`.** Same objection, worse.
- **Saga with a coordinator table.** More machinery than one hold, one confirm and one sweeper; the hold-then-confirm shape is a two-step saga without the framework.
- **Decrement on-hand at placement.** Mixes "promised" with "physically here"; the ledger stays physical and the reservation is the promise (Q-ORD-5).
- **Hold stock at add-to-cart.** Owner decision against it.

## Consequences

- Between steps 5 and 6 there is a window (milliseconds) where stock is held for an order that does not exist yet. It is harmless: the stock is reserved, not lost, and released by step 9 or the sweeper.
- If the outbox is down for longer than `HoldMinutes`, a real order's hold could be swept before its confirm event is processed. Mitigation: the sweeper skips while the outbox backlog age is above the hold time (a health value it already can read from its own inbox), and the confirm is also applied when the late event arrives to a `Released` reservation by re-reserving if stock allows, otherwise raising an alert for staff (an oversold order is a human problem, not a silent one).
- Everything above is repeatable and is proven by integration tests against PostgreSQL (Testcontainers): the concurrent last unit (ORD-2, RSV-3), the double submit (ORD-1), failure after reserve (ORD-7), and the sweeper.
