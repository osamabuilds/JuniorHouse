# Checkout

- **Status:** Approved (owner approved all recommendations, 2026-11-02; plan awaiting approval)
- **Jira:** to be created on approval (sprint S12)
- **BRD sections:** §5.4, §12.3.3
- **Requirement IDs:** FR-23, FR-24, FR-40 (selecting a saved address), SEC-09, TC-CHK-03, TC-CHK-04, TC-CHK-05, TC-CHK-06, TC-CHK-18

## Problem & intent
A shopper, signed in or not, must get from cart to a placed order in as few steps as possible, with clear field validation, and with nothing about their account state ever standing in the way of buying.

## Requirements (quoted from the BRD)
> **FR-23:** The system shall support both guest and registered checkout.

> **FR-24:** The system shall validate all required checkout fields before allowing progression to payment.

> **FR-40 (use at checkout):** Registered users shall maintain multiple saved addresses selectable at checkout.

## User flow
BRD §5.4: Cart review → Checkout → Guest or sign in → shipping details (or choose a saved address) → validation → payment step (discount code, then COD or Card) → place order → confirmation page. Signing in mid-checkout merges the guest cart ([carts](../../cart/carts/spec.md) CRT-3).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| CHK-1 | Given a guest cart, when the guest enters valid details, chooses COD and places the order, then an order is created and the confirmation shown | TC-CHK-03 |
| CHK-2 | Given a blank required field, when continuing, then an inline message blocks progression | TC-CHK-04 |
| CHK-3 | Given letters in the phone field, when continuing, then an inline format error shows (Pakistani mobile rule shared with accounts) | TC-CHK-05 |
| CHK-4 | Given a signed-in customer with saved addresses, when checking out, then the default address is pre-selected and manual entry is skipped | TC-CHK-06 |
| CHK-5 | Given a session idle past its timeout, when the shopper submits, then they are asked to refresh or sign in again and nothing is placed or charged twice | TC-CHK-18 |
| CHK-6 | Given any customer state (unverified email, unverified mobile, no account), when placing an order, then it is never blocked on that state | owner decision ACT-31 |
| CHK-7 | Given a guest, then email and mobile are required for the order record and confirmation; no account is created without consent | ACT-30 |
| CHK-8 | Given a city or province not in the lookups, when submitted, then it is rejected; the form offers only valid values | FR-24 (new) |
| CHK-9 | Given the cart changed or went empty in another tab, when the summary loads, then it shows the current cart and a notice (new) | FR-22 |
| CHK-10 | Given the user presses Back after placing an order and submits again, then no second order is created | TC-CHK-19 |
| CHK-11 | Given the page, then it is semantic, keyboard-operable, WCAG 2.2 AA, `noindex` and blocked in `robots.txt`; errors are announced to assistive tech | NFR-19, SEO-05 |
| CHK-12 | Given a guest opts in to WhatsApp updates, then the opt-in and number are stored with the order for [notifications](../../notifications/README.md) | FR-31 (new) |

## Non-functional constraints
SEC-09 (server totals), SEC-05 (saved addresses belong to the caller), SEC-25 (no card data or full address in logs). Card details are never entered on a Romp page; the gateway hosts them (PCI scope stays minimal). Checkout is a server-rendered page with progressive enhancement; it works with the fake gateway locally.

## Out of scope
Gift cards, gift wrap, order notes beyond one optional delivery instruction (Q-CHK-3), saving a new address from checkout into the address book unless the customer ticks a box (included, small).

## Open questions
- [x] **Q-CHK-1 Required guest fields:** name, email, mobile, address line, city, province, postal code? Is postal code mandatory in Pakistan? Recommendation: postal code optional. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-CHK-2 Single page or multi-step?** The design system's checkout page shape decides; check `docs/design-system/Pages.md`. If it has no checkout page, raise it with the owner (do not improvise). **Approved (owner, 2026-11-02: approve all recommendations).**
- [ ] **Q-CHK-3 Delivery instruction field** wanted? *Plan assumes one optional field of up to 200 characters; the owner has not answered.*
- [x] **Q-CHK-4 Serviceable areas.** Does Romp deliver to every city in the city lookup? Recommendation: all, until couriers (S15) say otherwise. **Approved (owner, 2026-11-02: approve all recommendations).**
- [ ] **Q-CHK-5 Terms and Privacy** consent text does not exist (business must supply). Checkout shows a placeholder link until then.
