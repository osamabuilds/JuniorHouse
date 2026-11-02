# COD confirmation

- **Status:** Approved (owner approved all recommendations, 2026-11-02; plan awaiting approval). Owner decisions 2026-11-02: SEC-11 (phone OTP) parked; SEC-12 and SEC-13 are review flags only, nothing blocks a customer. These deviate from the BRD wording ("COD block", "maximum COD order value") and are recorded here as owner-approved deviations.
- **Jira:** to be created on approval (sprint S12)
- **BRD sections:** §5.4, §8.4, §12.3.4
- **Requirement IDs:** FR-32, SEC-12, SEC-13, FR-55, TC-CHK-14, TC-CHK-15. Parked: SEC-11.

## Problem & intent
Most orders in Pakistan are Cash on Delivery, and fake or refused COD orders are costly. The business confirms an order by phone before spending warehouse effort, cancels non-responders automatically, and limits risk from first-time customers and repeat refusers.

## Requirements (quoted from the BRD)
> **FR-32:** For COD orders, the system shall support a configurable confirmation-call workflow (e.g. up to 2 attempts) with automatic cancellation and stock release on non-response.

> **SEC-12:** The system shall track COD refusal/no-show rate per phone number and flag repeat-offender numbers for manual review or a COD block (directing them to prepaid-only checkout), consistent with the COD-abuse pattern common in Pakistani e-commerce.

> **SEC-13:** First-time or unverified customers shall be subject to a configurable maximum COD order value, raised automatically after a successful delivery history is established.

> **SEC-11 (PARKED):** COD orders shall require phone-number verification (OTP) at checkout for first-time or previously-unconfirmed numbers, to reduce fake/abandoned COD orders.

## User flow
COD order placed as **Pending Confirmation**. The customer can confirm at once with the "Confirm my order" button (page and email) or, if opted in to WhatsApp and still silent after the wait, by tapping a reply button in a WhatsApp message (COD-12 to COD-19). Otherwise → a call is scheduled → staff call the customer from the [order queue](../order-queue/spec.md) and record the outcome (confirmed / no answer / refused) → on the configured number of no-answers (default 2) the order auto-cancels, stock is released and the customer is told. Confirmed orders move on to fulfilment.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| COD-1 | Given a COD order, when placed, then it is Pending Confirmation and a confirmation call is scheduled | TC-CHK-14 |
| COD-2 | Given two unanswered attempts (configured number), then the order is auto-cancelled, the customer is notified and the stock released | TC-CHK-15, FR-32 |
| COD-3 | Given a recorded "confirmed" outcome, then the order becomes Confirmed and is visible to fulfilment | FR-32 |
| COD-4 | Given a "refused" outcome, then the order is cancelled and the number's refusal count increases | SEC-12 |
| COD-5 | Given a number past the configured refusal threshold, when a COD order is placed, then the order is still accepted and is only flagged for staff review in the queue (owner, 2026-11-02: never block a customer) | SEC-12 (revised) |
| COD-6 | Given a first-time customer and a high COD total, when ordering, then COD is still offered; the order is flagged "high value, first order" so staff can prioritise the confirmation call (no cap, owner 2026-11-02) | SEC-13 (revised) |
| COD-7 | Given a delivered order (S15 data), then the customer is no longer shown as first-time in the queue flag | SEC-13 (revised) |
| COD-8 | Given attempts and cancellations, then each attempt records number, outcome, time and the staff member (audited) | FR-59, §12.3.4 |
| COD-9 | Given a no-answer attempt, then the next attempt is scheduled after the configured gap and appears in the queue's "due now" filter | FR-32 (new) |
| COD-10 | Given a flagged number, then staff can clear the flag, audited (new) | SEC-12 |
| COD-11 | Given a COD order, then no OTP or SMS step exists in checkout (parked) and the cap and refusal rules apply to the unverified number as given | SEC-11 parked |
| COD-12 | Given a COD order is placed, then the confirmation page and the confirmation email each carry a "Confirm my order" button that opens a signed, single-order link; pressing it records a customer-confirmed outcome with no staff call | owner request 2026-11-02 (new) |
| COD-13 | Given a COD order whose customer opted in to WhatsApp (checkout CHK-12) and who has not confirmed within the configured wait (default 60 minutes), then a WhatsApp message with the order summary and two reply buttons, "Confirm order" and "Cancel order", is sent through the notifications module | owner request 2026-11-02 (new) |
| COD-14 | Given the customer taps "Confirm order", when the signed inbound WhatsApp event arrives, then the order becomes Confirmed exactly once, the attempt log records outcome "confirmed by customer (WhatsApp)" and no staff call is needed | FR-32, SEC-10 (new) |
| COD-15 | Given the customer taps "Cancel order", then the order is cancelled, the reservation released, the cancellation is recorded as "cancelled by customer (WhatsApp)" and it does not count as a refusal or no-show for SEC-12 | FR-33 (new) |
| COD-16 | Given an inbound WhatsApp event with a missing or invalid signature, a number that does not match the order's mobile, an unknown or already finished order, or a repeated delivery, then it is rejected or ignored, logged as a security event where applicable, and changes nothing twice | SEC-10, NFR-FT-05 (new) |
| COD-17 | Given the customer has not replied or opted in, then the staff call workflow (COD-1 to COD-3, COD-9) runs unchanged; the WhatsApp message, the page button and the email link only add ways to confirm, they never skip or block anything | FR-32 (new) |
| COD-18 | Given WhatsApp is down or the circuit is open, then the order is unaffected, the email and page button remain, and the staff call workflow continues | SEC-21, FR-64 (new) |
| COD-19 | Given a confirmation link or button is used after the order is confirmed, cancelled or past its window, then a neutral "already handled" page is shown and nothing changes | (new) |

## Non-functional constraints
Per-number tracking keys on the normalised mobile (reuse the accounts normaliser); the table holds a hash-free number because staff need to call it, so access is limited to the order-ops and support roles and the number is never logged (SEC-25). The auto-cancel job is idempotent. Thresholds are settings, not code.

## Out of scope
Automated outbound calls or IVR (staff call manually for now), SMS/OTP verification (parked).

## Open questions
- [x] **Q-COD-1 Who calls and when?** Staff using the queue, in what hours, how long between attempts (recommendation 2 hours), and how long before the first call (recommendation 1 hour)? Auto-cancel after attempts 2 plus 24 hours? **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-COD-2 COD cap** (owner, 2026-11-02): no cap and no block at any level. SEC-13 becomes a review flag only; the flag threshold (order value) is a setting the business picks later.
- [x] **Q-COD-3 Refusal block** (owner, 2026-11-02): never blocked; only flagged for staff review. Threshold for the flag (recommendation: 2 refusals) is a setting.
- [x] **Q-COD-6 WhatsApp confirmation (owner, 2026-11-02): add it.** Confirmation by tapping a reply button in WhatsApp, a "Confirm my order" button on the confirmation page and in the email, with the staff call as fallback (COD-12 to COD-19). WhatsApp sends cost money per message (rates for Pakistan reported at about US$0.015 per business-started message from October 2026, unconfirmed: check Meta's current rate card before launch), so the message goes only to opted-in customers who have not already confirmed, after a wait (default 60 minutes). WhatsApp needs a Meta-approved template with quick-reply buttons and the customer's opt-in.
- [x] **Q-COD-4 "Unverified" now means everyone** because mobile numbers are not verified (parked). Confirm SEC-13 therefore applies to every customer without a delivered order, and "delivered" is read from S15. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-COD-5** Closed: COD is offered for every order value.
