# Cart

The shopper's basket (Sprint 11, "Sell: cart"): add, change and remove lines with live stock checks, merge a guest cart into the account cart on sign-in, apply one discount code, and show server-calculated totals and shipping. Nothing here takes an order or a payment (checkout is S12).

**Status:** Draft specs, awaiting owner approval (2026-11-02) · **Schema:** `CART` (proposed) · **Code:** `src/api/src/Modules/Cart/` (planned) · **Web:** `features/cart/` in the storefront app and `features/cart/discount-codes/` in the admin app (planned) · **Jira:** created on approval · **Rules:** [constitution](../../CONSTITUTION.md), [CONVENTIONS](../../CONVENTIONS.md), [CLAUDE.md](../../../CLAUDE.md) · **Design:** `docs/design-system/` (storefront only)

## Features

| Feature | What it does | Requirements | Status |
|---|---|---|---|
| [carts](carts/spec.md) | Guest and account carts, add/update/remove, live stock check, merge on sign-in, empty state | FR-10, FR-22, FR-35, SEC-09, TC-CHK-01, 02, 21 | Draft |
| [discount-codes](discount-codes/spec.md) | Admin creates codes; the shopper applies one code with a specific inline reason on rejection; rate-limited | FR-25, FR-26, FR-57, SEC-14, TC-CHK-07..10 | Draft |
| [totals](totals/spec.md) | Server-side subtotal, discount, shipping and total, to the paisa; the one pricing routine checkout reuses | FR-34, SEC-09, TC-CHK-22 | Draft |

Order of work: carts → totals → discount-codes (totals needs a cart; discounts plug into totals).

## Module decisions to confirm

| ID | Proposal |
|---|---|
| D-1 | **New module `Cart` (schema `CART`)**, separate from `Customers` and `Orders` (owner chose the three-module split, 2026-11-02). Needs an ADR in the module plan. |
| D-2 | **The cart never stores a price.** It stores variant id and quantity; every read prices the lines from the Catalog contract, so a price change is picked up and the client's numbers are never trusted (SEC-09). |
| D-3 | **Cart reads published price from `Catalog.Contracts` and availability from `Warehouse.Contracts`**; it never reads the storefront read model (listings may be seconds stale, the BRD says add-to-cart reads live stock, §12.7). |
| D-4 | **Signed-in customers come from `Customers` through `ICurrentCustomer`-style contract**, never by reading `CUST` tables. The merge hook is called by the customer sign-in feature ("exposes the hook", customers README). |

## Requirement IDs for the module

FR-10, FR-22, FR-25, FR-26, FR-34, FR-35, FR-57, SEC-09, SEC-14, NFR-08 (read side), NFR-17, NFR-19. Test cases TC-CHK-01, 02, 07..10, 21, 22. Criterion IDs are prefixed by feature: `CRT-`, `DSC-`, `TOT-`.

## Out of scope for the sprint

Checkout and payment (S12), stock reservation (S12, see [orders](../orders/README.md)), gift cards and gift wrap (P2), abandoned-cart reminders (FR-51, P2), Child Profile link on a cart line (child profiles are parked), Fit Finder (descoped).
