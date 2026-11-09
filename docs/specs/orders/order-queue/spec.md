# Order queue (admin)

- **Status:** Approved (owner approved all recommendations, 2026-11-02; plan awaiting approval)
- **Jira:** to be created on approval (sprint S13)
- **BRD sections:** §5.8, §7.8
- **Requirement IDs:** FR-55, FR-59, FR-60, FR-32, FR-33, SEC-08

## Problem & intent
Order-ops staff need one screen that shows every incoming order, tells them which COD orders still need a confirmation call, and lets them confirm or cancel with a trail of who did what.

## Requirements (quoted from the BRD)
> **FR-55:** Admin users shall manage the incoming-orders queue: confirm COD, dispatch with courier details, cancel.

> **FR-59:** All admin actions on orders, stock, returns, and promotions shall be attributable to the acting user and timestamped.

> **FR-60:** Admin access shall be role-based (catalogue admin, order-ops, support, marketing) following least-privilege.

> **SEC-08 (CLAUDE.md rule):** Admin endpoints check the caller's role server-side on every request.

## User flow
BRD §5.8: view the queue → open an order → record a COD call outcome (confirm / no answer / refused) or cancel with a reason → (dispatch with courier and tracking number is added when couriers exist, S15; this sprint shows the action disabled with the reason "couriers arrive in a later release", or hides it, owner's choice in Q-OQ-3).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| OQ-1 | Given orders exist, when staff open the queue, then they see a paged list filterable by status, payment method, date and "COD confirmation due now" | FR-55 |
| OQ-2 | Given a Pending Confirmation COD order, when staff record "confirmed", then it becomes Confirmed | FR-55, FR-32 |
| OQ-3 | Given a no-answer or refused outcome, then it is recorded and the workflow in [cod-confirmation](../cod-confirmation/spec.md) applies | FR-32 |
| OQ-4 | Given a not-dispatched order, when staff cancel with a reason, then reservations are released and the customer is notified | FR-33, FR-55 |
| OQ-5 | Given any staff action, then it is attributable to the signed-in user and timestamped (audit trail, ADR 0016) | FR-59 |
| OQ-6 | Given a user without the order-ops permission, when they call any endpoint, then it is refused server-side | SEC-08 |
| OQ-7 | Given an order, when opened, then staff see lines, totals, contact and address, payment state, status history and notification status (links to [delivery-log](../../notifications/delivery-log/spec.md)) | FR-55 (new) |
| OQ-8 | Given two staff act on the same order, then the second sees a conflict and the latest state (optimistic concurrency) | NFR-08 (new) |
| OQ-9 | Given thousands of orders, then the queue stays paged and constant-query (no N+1) | CONVENTIONS |
| OQ-10 | Given a customer phone number shown on screen, then it is visible only to roles that need it and never written to logs | SEC-25 |

## Non-functional constraints
Admin app keeps its current look (design system does not apply). SEC-08 server-side permission on every request, new permissions `orders.view`, `orders.manage` in the roles catalogue (S8 roles feature). Write buttons hidden by `*hasPermission`. Semantic HTML and WCAG per CLAUDE.md.

## Out of scope
Dispatch with courier details and tracking (S15), pick/pack (S14), returns queue (S16), reports (S18).

## Open questions
- [x] **Q-OQ-1 Permissions.** Which roles get `orders.view` and `orders.manage` (order-ops yes; support view-only; others none)? Confirm. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-OQ-2 Can staff edit an order** (address, items) or only confirm/cancel? Recommendation: confirm/cancel only; an address correction field for ops is a separate small feature if wanted. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-OQ-3 The dispatch action before S15:** hidden or shown-disabled? Recommendation: hidden. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-OQ-4 Notifications on staff cancel:** always notify the customer? Recommendation: yes, with the reason category only. **Approved (owner, 2026-11-02: approve all recommendations).**
