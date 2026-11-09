# Totals and shipping

- **Status:** Built (2026-11-02; discount hold confirmation waits for Orders)
- **Jira:** SCRUM-202 branch (sprint S11); separate issue to be created when the PR is opened
- **BRD sections:** §5.4, §12.3.3, §12.8
- **Requirement IDs:** FR-34, SEC-09, TC-CHK-22

## Problem & intent
Every number the shopper pays must be calculated by the server from its own data, to the paisa, by one routine used by the cart, by checkout and by the order. Money mistakes here are launch-blocking.

## Requirements (quoted from the BRD)
> **FR-34:** The system shall correctly calculate shipping fees and totals per the configured shipping policy (e.g. flat fee per order) to full currency-unit precision.

> **SEC-09:** The server shall always recalculate order totals (item prices, discounts, shipping) from its own product/pricing data at payment time; client-submitted prices/totals shall never be trusted or persisted directly.

## User flow
The cart and checkout summaries show: subtotal (sum of price × quantity), discount (from the applied code, see [discount-codes](../discount-codes/spec.md)), shipping, total. Each is recomputed on every read and again at order placement and payment.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| TOT-1 | Given several items across categories, when totals are calculated, then the shipping fee applies once per order per the policy, not per item, and the arithmetic is exact | TC-CHK-22 |
| TOT-2 | Given prices with fractional rupees, when totals are calculated, then every amount is `decimal` PKR rounded as the policy says and the sum of displayed parts equals the total | FR-34 |
| TOT-3 | Given a client request with amounts, when totals are calculated, then the amounts are ignored | SEC-09 |
| TOT-4 | Given an empty cart, when totals are read, then everything is zero and shipping is not charged (new) | FR-22 |
| TOT-5 | Given the shipping policy is changed by staff, when the next cart is read, then the new policy applies; existing orders keep the fee they were placed with (new) | §12.3.3 |
| TOT-6 | Given a discount larger than the subtotal, when totals are calculated, then the total is never negative (new) | FR-25 |
| TOT-7 | Given the same cart and policy, when totals are calculated twice, then the result is identical (deterministic) | FR-25, FR-34 |

## Non-functional constraints
Money is `decimal` / `numeric(12,2)` PKR, never `double`. The routine is a pure function in the Domain layer with no I/O, so it is unit-tested exhaustively, and checkout calls the same code (no second implementation).

## Out of scope
Tax calculation engines, multi-currency, per-courier rate tables (the fee is per city, see Q-TOT-1), free-shipping promotions beyond what Open questions settle.

## Open questions
- [x] **Q-TOT-1 (owner, 2026-11-02: rates are picked and edited by staff in the admin, so the business needs no hand-over: a city rate table, a default fee, an optional free-shipping threshold and an optional COD fee, all settings screens; the migration seeds a zero default that a health warning flags until it is set).** Earlier text: The shipping policy (partly answered, owner 2026-11-02): the fee is calculated on the order placement page and differs by city.** Still open: the rate per city (a table the business supplies), a default for cities without a rate, any free-shipping threshold, and whether COD carries an extra fee. Proposal: a staff-editable city rate table in the admin (lookup-style, keyed to the city lookup), so no code change is needed to add a city or change a rate.
- [x] **Q-TOT-2 Tax.** TC-CHK-22 says "shipping fee/tax calculation". Are prices tax-inclusive (GST/sales tax already in the price), so no tax line is shown? Recommendation: inclusive, no tax line. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-TOT-3 Rounding.** FR-34 says "full currency-unit precision" and TC-CHK-22 says "to the paisa/rupee". Do prices carry paisa, or are all amounts whole rupees? Recommendation: store `numeric(12,2)`, round half away from zero to 2 decimals, display whole rupees only if every price is whole. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-TOT-4 Who edits the policy,** and is a history of changes needed beyond the audit trail? Recommendation: audit trail only. **Approved (owner, 2026-11-02: approve all recommendations).**

## As built (2026-11-02)

- One pure routine, `OrderTotals.Calculate`, with `ShippingPolicy.FeeFor`, is used by the bag, `ICheckoutPricing` (for Orders) and the order, so no second copy can drift. Money is rounded half away from zero to 2 decimals.
- Staff set the default fee, the free-delivery amount and the Cash on Delivery fee, and a fee per city, in the admin (`/shipping`, permission `shipping.manage`, `features/cart/totals`). Until a city is known, delivery reads "Calculated at checkout".
- There is no browser e2e for the admin screen (it needs a staff login); the screen is covered by store/reducer specs and the endpoint integration tests.
