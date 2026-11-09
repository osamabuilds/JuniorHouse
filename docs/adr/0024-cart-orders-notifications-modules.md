# 0024 — Cart, Orders and Notifications modules, and what they share

- Status: Proposed (owner chose the three-module split on 2026-11-02; approval of this ADR comes with the plans)
- Date: 2026-11-02

## Context

Sprints 11 to 13 add the basket, checkout and orders, and customer notifications. The owner chose three modules rather than one "commerce" module (2026-11-02). The modular monolith rules (ADR 0002, ADR 0011) say modules never reach into each other's internals, each owns one schema, and they talk through `*.Contracts` or outbox events. Several things the three need already exist, but inside other modules' internals: who the signed-in customer is (`ICurrentCustomer`), the CAPTCHA check (`ICaptchaVerifier`), the origin check for cookie-authenticated writes (`CustomerOriginCheckFilter`) and the sign-in hook (`ICustomerSignedInHook`).

## Decision

1. **Three new modules**, each with `Domain`, `Application`, `Infrastructure` and `Contracts` projects and a `Tests` project, registered like the others:

   | Module | Schema | Owns |
   |---|---|---|
   | `Cart` | `CART` | carts and lines, guest identity, discount codes and their redemptions, shipping rates, the one pricing routine |
   | `Orders` | `ORDR` | checkout, orders and their lines and history, payments, COD confirmation, the staff order queue, order look-up |
   | `Notifications` | `NTFY` | notification records, per-channel delivery, templates, dead letters, WhatsApp inbound, the delivery log |

   Reservations of stock live in `Warehouse` (`WHSE`), not in `Orders` (ADR 0025).

2. **Dependencies are one way, through Contracts.**
   - `Cart` reads `Catalog.Contracts` (variant price and sellability), `Warehouse.Contracts` (availability), `Reference.Contracts` (cities), `Customers.Contracts` (sign-in hook).
   - `Orders` reads `Cart.Contracts` (priced cart, discount holds), `Warehouse.Contracts` (reserve), `Customers.Contracts` (saved addresses), `Reference.Contracts` (cities, provinces).
   - `Notifications` reads `Orders.Contracts` (what to say about an order; the customer's reply to a COD message) and nothing else. `Orders` never references `Notifications`: it raises events.
   - `Warehouse` and `Cart` consume `Orders.Contracts` events (`OrderPlacedEvent`, `OrderCancelledEvent`) to confirm or release holds.
   - No cycle at project level. Architecture tests get the three names added to `ModuleNames`.

3. **Events carry ids only** (ADR 0011): never a name, address, phone, email or message body. The consumer loads what it needs through the owning module's contract at the time it acts. Each consumer reads the foreign event through one mapper file, so a change to the event has one place to fix.

4. **Shared pieces move to building blocks**, so that no module references another module's `Application` or `Infrastructure` project:
   - `ICaptchaVerifier` (in `Romp.BuildingBlocks.Application`) and the Turnstile and development adapters (in `Romp.BuildingBlocks.Web`), moved out of `Customers` (Customers keeps its behaviour; its tests move with the code).
   - `Romp.BuildingBlocks.Web`: `OriginCheckFilter` (the rule in today's `CustomerOriginCheckFilter`, parameterised by the site address) and `SignedLink` (purpose-bound HMAC-SHA256 token with constant-time comparison, used for order links and the COD confirm link).
   - `Romp.BuildingBlocks.Domain` gains `PhoneNumber` (the Pakistani mobile normaliser, moved out of `Customers.Domain`), so checkout, COD tracking and look-up compare numbers in exactly the format accounts store.
   - `Customers.Contracts` gains `ICustomerSignedInHook` (moved from `Customers.Application`) so `Cart` can implement it, and `ICurrentCustomerAccessor` (the signed-in customer id from the session principal, nothing else).
   Moves are refactors with no behaviour change; the existing tests are the proof.

5. **Permissions** are rows in the Identity permission lookup (seeded by an additive Identity migration, as `warehouse.read` was): `orders.view`, `orders.manage`, `discounts.manage`, `shipping.manage`, `notifications.view`, `notifications.resend`. Roles are assigned in the existing roles screen.

6. **Naming and growth.** Schema names `CART`, `ORDR`, `NTFY` go in `docs/db/naming.md` and the constitution table. `ORDR.ORDR_MAIN`, `ORDR.ORDR_LINE` and `NTFY.NTFY_MAIN` are the tables expected to grow; their partitioning key is the placement date by month (BRD §12.6), not applied until volume asks for it (the primary keys do not block it).

## Alternatives considered

- **One `Commerce` module.** Fewer contracts, but one schema and one set of tests growing without a boundary, and Notifications would sit inside the module whose failures it must survive (NFR-FT-08). Rejected by the owner.
- **Orders owns the reservation.** Puts stock arithmetic in a module that has no ledger and breaks "WMS is the single source of truth" (BRD §5.14). Rejected (ADR 0025).
- **Notifications reads order tables.** Forbidden by ADR 0002.

## Consequences

- Three more modules to build, but each is small and each boundary is tested.
- The extractions touch Customers and Identity once, behind green tests.
- Every cross-module read is a contract method with a bulk form (max 100 ids), so no N+1 across modules (CONVENTIONS).
