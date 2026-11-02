# Discount codes

- **Status:** Approved (owner approved all recommendations, 2026-11-02; plan awaiting approval)
- **Jira:** to be created on approval (sprint S11)
- **BRD sections:** §5.4, §8.4, §12.3.3
- **Requirement IDs:** FR-25, FR-26, FR-57, SEC-14, FR-59, TC-CHK-07, TC-CHK-08, TC-CHK-09, TC-CHK-10

## Problem & intent
Marketing needs to run promotions with codes; shoppers need to know at once why a code does not work; the business needs codes that cannot be guessed, stacked by accident or reused beyond their limits.

## Requirements (quoted from the BRD)
> **FR-25:** The system shall support a single discount/promo code per order by default, applied only on explicit user action, with deterministic (not silently-overwriting) behaviour if a user attempts to apply a second code.

> **FR-26:** The system shall reject expired, over-limit, or already-used codes with a specific inline reason.

> **FR-57:** Admin users shall create discount codes with expiry, usage cap, minimum spend, and category scope.

> **SEC-14:** Discount-code application and Fit-Finder submission endpoints shall be rate-limited to prevent brute-force code guessing and automated abuse.

> **FR-59:** All admin actions on orders, stock, returns, and promotions shall be attributable to the acting user and timestamped.

## User flow
- **Admin:** create a code (value, type, expiry, usage cap, minimum spend, category scope), list, deactivate. Every action is audited (FR-59).
- **Shopper:** on the cart or payment step, types a code and presses Apply (never auto-applied). The totals update, or a specific inline reason is shown. Remove clears it.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| DSC-1 | Given a valid code, when applied, then the discount is applied and the total is reduced | TC-CHK-07 |
| DSC-2 | Given an expired code, when applied, then an inline "expired" reason is shown and the total is unchanged | TC-CHK-08 |
| DSC-3 | Given a single-use code already redeemed, when applied, then "already used" is shown | TC-CHK-09 |
| DSC-4 | Given a code is active, when a second code is applied, then the outcome is the documented deterministic one (reject the second, keep the first) and the first is not silently replaced | TC-CHK-10, FR-25 |
| DSC-5 | Given a usage cap reached, when applied, then "no longer available" is shown | FR-26 |
| DSC-6 | Given a subtotal below the minimum spend, when applied, then the reason states the minimum | FR-26 (new) |
| DSC-7 | Given a category-scoped code, when applied, then only lines in that scope are discounted; no eligible line gives a specific reason | FR-57 (new) |
| DSC-8 | Given many failed attempts from one caller, when more are made, then they are rate-limited | SEC-14 |
| DSC-9 | Given a code that is unknown, mistyped or deactivated, when applied, then the same generic reason is shown for all three so codes cannot be enumerated | SEC-14 (new) |
| DSC-10 | Given two checkouts using the last remaining redemption, when both place orders, then only one redemption succeeds (the unique index and a row lock enforce it) | §12.3.3, NFR-08 (new) |
| DSC-11 | Given an admin creates, changes or deactivates a code, when it happens, then the audit trail records who and when | FR-59 |
| DSC-12 | Given an admin creates a code, when values are invalid (negative, percent over 100, expiry in the past, empty scope when scoped), then it is refused with a specific message | FR-57 (new) |
| DSC-13 | Given a code applied to a cart, when the cart changes so the rules no longer hold, then the code is dropped with a notice rather than leaving a wrong discount | FR-25 (new) |

## Non-functional constraints
SEC-14 rate limit on apply; SEC-08 role check server-side on every admin endpoint (the marketing role, FR-60); SEC-09 discount computed server-side; codes stored uppercase-normalised; a redemption is recorded only when the order is placed (S12), never on apply.

## Out of scope
Gift cards and the TC-CHK-20 case (P2), loyalty and referral rewards (P2), automatic (code-less) promotions, stacking.

## Open questions
- [x] **Q-DSC-1 Second code behaviour.** TC-CHK-10 allows "enforce one-code-per-order or explicitly support stacking". Recommendation: reject the second with a message telling the shopper to remove the first. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-DSC-2 Types.** Percentage and fixed amount; free shipping as a type? Recommendation: percentage and fixed amount only. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-DSC-3 Per-customer limit.** §12.3.3 has a unique index on (code, user) for one redemption per user. Is that always the rule, or configurable per code? A guest has no user id: limit by mobile or email? Recommendation: configurable per-customer limit, default 1, guests counted by normalised mobile. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-DSC-4 Do codes apply to sale items?** (`CompareAtPrice` set). Recommendation: a per-code flag, default excluded. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-DSC-5 Rate-limit numbers** (attempts per minute and per hour per caller). Recommendation: 10 per 15 minutes per IP and per customer, then a 15-minute wait. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-DSC-6 Admin role.** FR-60 names a marketing role; roles exist from S8. Confirm which permission guards this screen (new `discounts.manage`). **Approved (owner, 2026-11-02: approve all recommendations).**
