# Carts

- **Status:** Built (2026-11-02; discount hold confirmation waits for Orders)
- **Jira:** SCRUM-202 branch (sprint S11); separate issue to be created when the PR is opened
- **BRD sections:** §5.2, §5.4, §12.3.3, §12.7
- **Requirement IDs:** FR-10, FR-22, FR-35, SEC-09, NFR-08, TC-CHK-01, TC-CHK-02, TC-CHK-21

## Problem & intent
A shopper needs a basket that survives navigation and sign-in, always shows the real price and availability, and never loses an item when a guest signs in. Staff and the business need the cart to be tamper-proof: prices come from the server only.

## Requirements (quoted from the BRD)
> **FR-10:** The system shall check live stock at the exact variant level before allowing add-to-cart, and shall re-check at payment (see FR-20).

*BRD erratum (SCRUM-168): "FR-20" means FR-28. Recorded, not resolved here.*

> **FR-22:** The system shall support cart quantity update and removal with live subtotal recalculation, correctly handling the transition to an empty-cart state.

> **FR-35:** When a logged-out user with a guest cart logs in mid-checkout, the system shall merge the guest cart with any existing account cart without item loss or duplication.

> **SEC-09:** The server shall always recalculate order totals (item prices, discounts, shipping) from its own product/pricing data at payment time; client-submitted prices/totals shall never be trusted or persisted directly.

> **NFR-08:** Stock-decrement and order-placement shall be transactional/atomic to prevent overselling under concurrency (see FR-28).

## User flow
BRD §5.2 and §5.4, step by step:
1. On a product page the shopper picks colour and size; the exact variant's stock is checked. In stock → Add to Cart → the mini-cart opens. Out of stock → Notify Me (customers module).
2. The cart page lists lines with quantity steppers, remove, and a live subtotal.
3. Removing the last line shows the empty-cart state.
4. A guest cart is held for a guest session; a signed-in customer has one account cart. On sign-in the two merge (FR-35).
5. Every read re-prices the lines and re-checks stock; a line whose variant is no longer sellable or has less stock than requested is flagged, never silently dropped (new).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| CRT-1 | Given a cart with a line, when the quantity changes, then the subtotal recalculates from server prices | TC-CHK-01 |
| CRT-2 | Given a cart with one line, when it is removed, then the cart is empty and the empty state is shown | TC-CHK-02 |
| CRT-3 | Given a guest cart and an account cart, when the guest signs in, then lines merge: a variant in both is combined into one line, nothing is lost or duplicated | TC-CHK-21, FR-35 |
| CRT-4 | Given a variant with stock N, when a shopper adds more than N in total, then the add is refused with the available count | FR-10 |
| CRT-5 | Given a variant that is not sellable or not published, when it is added, then it is refused | FR-10 (new) |
| CRT-6 | Given a request that carries prices or totals, when it is processed, then they are ignored; only variant id and quantity are read | SEC-09 |
| CRT-7 | Given a line whose stock fell below its quantity after it was added, when the cart is read, then the line is flagged with the available count and the cart still loads | FR-10 (new) |
| CRT-8 | Given a price change in Catalog, when the cart is read, then the new price is shown and the line is marked "price changed" (new) | SEC-09 |
| CRT-9 | Given any cart endpoint, when it is called for another customer's cart or a guest session that is not the caller's, then it behaves as not found | SEC-05 (new) |
| CRT-10 | Given a cart idle past its lifetime, when it is read, then it is empty and a new one starts (new) | §12.3.3 `ExpiresAt` |
| CRT-11 | Given a quantity of zero, negative or above the per-line maximum, when sent, then it is rejected with a specific reason (new) | FR-22 |

## Non-functional constraints
- SEC-09 and SEC-05 as above; a guest session id is an unguessable random value in an HttpOnly cookie, never an incrementing id.
- Read cost: pricing and stock for a whole cart take a constant number of queries (no N+1, CONVENTIONS).
- NFR-17 mobile-first, NFR-19 WCAG 2.2 AA on the cart page and mini-cart; semantic HTML per CLAUDE.md; cart pages are `noindex` and blocked in `robots.txt` (SEO-05).

## Out of scope
Checkout, payment, stock reservation (S12), gift wrap and gift cards (P2), abandoned-cart reminders (FR-51, P2), Child Profile on a line (parked).

## Open questions
- [x] **Q-CRT-1 Reservation timing** (owner, 2026-11-02): reserve at order placement, no hold on add-to-cart. (BRD §12.3.2 `StockReservations` "on add-to-cart" is overridden; FR-SC-09 stands.)
- [x] **Q-CRT-2 Cart lifetime.** How long does a guest cart and an account cart live? Recommendation: guest 30 days, account no expiry. **Approved (owner, 2026-11-02: approve all recommendations).**
- [ ] **Q-CRT-3 Per-line maximum quantity** (stock level only, or a cap such as 10 per line)? *Plan assumes a configurable per-line maximum, default 10, never above available stock; the owner has not answered.*
- [x] **Q-CRT-4 Merge when a variant is in both carts:** add the quantities (capped at stock) or keep the larger? Recommendation: add, capped at available stock, with a notice. **Approved (owner, 2026-11-02: approve all recommendations).** **Revised (owner, 2026-11-02): nothing is removed when a guest signs in; the merge adds quantities and a line asking for more than is available shows as low stock instead of being cut.**
- [x] **Q-CRT-5 Where guest identity lives:** a guest-session cookie on the storefront origin (consistent with Q-ACT-5, SameSite=Lax). Confirm. **Approved (owner, 2026-11-02: approve all recommendations).**

## As built (2026-11-02)

- Public routes `GET /api/cart`, `POST /api/cart/lines`, `PUT` and `DELETE /api/cart/lines/{variantId}`; the owner is the signed-in customer or the `romp-guest` cookie, never an id from the request. Every answer is `no-store`.
- The basket stores only the variant and the quantity; price, stock and the picture are read fresh each time through the Catalog, Warehouse and Storefront contracts (no N+1).
- **Owner decision (replaces the approved "capped at stock with a notice" wording):** when a guest signs in, the guest bag is merged into the account bag and nothing is removed or cut. A line that now asks for more than is in stock is kept and flagged "low stock" in words; the shopper fixes it before checkout.
- The bag shows the real product picture, the header bag button shows the item count, Remove is a trash icon, and the minus button at quantity 1 removes the line.
- Storefront: `features/cart/carts` (NgRx store, bag page, right-hand drawer); e2e `e2e/cart/carts.e2e.spec.ts`.
