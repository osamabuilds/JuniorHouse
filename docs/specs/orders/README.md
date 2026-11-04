# Orders

Turning a cart into a paid or confirmed order (Sprint 12, "Sell: checkout") and the staff queue that handles it (S13): guest and registered checkout, Cash on Delivery and card payment, idempotent order placement, verified payment webhooks and reconciliation, the COD confirmation workflow, cancellation, and the admin order queue. Picking, packing, courier booking and tracking are S14/S15.

**Status:** Specs approved by the owner with all recommendations (2026-11-02); [plan](plan.md) drafted and awaiting approval (then tasks) · **Schema:** `ORDR` (proposed) · **Code:** `src/api/src/Modules/Orders/` (planned) · **Web:** `features/orders/` in the storefront app (checkout, confirmation) and the admin app (queue) (planned) · **Jira:** created on approval · **Rules:** [constitution](../../CONSTITUTION.md), [CONVENTIONS](../../CONVENTIONS.md), [CLAUDE.md](../../../CLAUDE.md) · **Design:** `docs/design-system/` (storefront only)

## Features

| Feature | What it does | Requirements | Sprint | Status |
|---|---|---|---|---|
| [checkout](checkout/spec.md) | Guest and registered checkout, validated fields, saved-address selection, session expiry | FR-23, FR-24, FR-40 (use), TC-CHK-03..06, 18 | S12 | Draft |
| [orders](orders/spec.md) | Atomic, idempotent placement; unique order number; status history; address snapshot; cancellation | FR-28, FR-29, FR-33, NFR-08, NFR-09, SEC-09, TC-CHK-16, 17, 19 | S12 | Draft |
| [payments](payments/spec.md) | COD, Easypaisa, JazzCash and debit/credit cards through a gateway port; signed webhooks; reconciliation; circuit breaker with COD-only fallback | FR-27, FR-30, SEC-10, SEC-21, TC-CHK-11..13 | S12 | Draft |
| [cod-confirmation](cod-confirmation/spec.md) | Pending-confirmation orders, up to 2 call attempts, auto-cancel, order-value cap, refusal tracking | FR-32, SEC-12, SEC-13, TC-CHK-14, 15 | S12 | Draft (OTP part SEC-11 **parked**) |
| [order-queue](order-queue/spec.md) | Staff queue: confirm COD, cancel, view status (dispatch added in S15) | FR-55, FR-59, FR-60 | S13 | Draft |
| [order-lookup](order-lookup/spec.md) | A guest finds an order by number plus mobile | FR-37 (part), SEC-06 | S13 (tracking-number part with couriers in S15) | Draft |

The reservation of stock lives in the Warehouse module: [warehouse/reservations](../warehouse/reservations/spec.md).

Order of work: orders (placement core) → reservations → payments (COD first, card behind the gateway port) → checkout → cod-confirmation → order-queue → order-lookup.

## Module decisions to confirm

| ID | Proposal |
|---|---|
| D-1 | **New module `Orders` (schema `ORDR`)** holding checkout, orders, payments, COD confirmation. Owner chose the three-module split (2026-11-02). Needs an ADR in the module plan. |
| D-2 | **Order placement is one database transaction in `Orders`** that writes the order, its lines and an outbox event; the stock reservation is a synchronous call through `Warehouse.Contracts` that must be atomic with the order or compensated (design in the plan; this is the hardest point of S12). Needs an ADR. |
| D-3 | **Payment gateway behind `IPaymentGateway`** with a fake adapter first (sprint plan: "each integration is built behind an interface with a fake implementation first"). The real adapter waits for the merchant account and the owner naming the gateway. |
| D-4 | **Notifications are never in the order transaction's path**: the order writes an `OrderPlacedEvent` (ids only) via the outbox; the Notifications module reacts (NFR-FT-08, S13). |
| D-5 | **COD number verification (SEC-11) is parked** (owner, 2026-11-02): no OTP at checkout, no SMS. SEC-12 refusal tracking and SEC-13 value cap stay. |

## Owner decisions carried in (from customers, 2026-11-02)

Guest checkout works with nothing verified: name, email, mobile and address are stored with the order (ACT-30, ACT-32). An unverified-email customer is never blocked and **nothing ever blocks an order** on account state. Mobile numbers are saved but not verified.

## Requirement IDs for the module

FR-23, FR-24, FR-27, FR-28, FR-29, FR-30, FR-32, FR-33, FR-55, FR-59, FR-60, FR-SC-09 (with warehouse), SEC-09, SEC-10, SEC-12, SEC-13, SEC-21, NFR-08, NFR-09, NFR-FT-08 (boundary). Test cases TC-CHK-03..06, 11..19. **Parked:** SEC-11. Criterion IDs are prefixed by feature: `CHK-`, `ORD-`, `PAY-`, `COD-`, `OQ-`, `OLK-`, `RSV-`.

## Out of scope

Picking, packing, courier booking, manifests (S14/S15); order history for signed-in customers and courier tracking (S15, FR-36); returns, exchanges and refunds (S16/S17); gift cards, loyalty, BNPL (post-launch); SEC-11 OTP (parked).
