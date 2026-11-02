# Order look-up

- **Status:** Approved (owner approved all recommendations, 2026-11-02; plan awaiting approval)
- **Jira:** to be created on approval (sprint S13; the tracking-number part waits for S15)
- **BRD sections:** §5.5
- **Requirement IDs:** FR-37 (guest part), SEC-06, SEC-07. The signed-in history (FR-36) is S15.

## Problem & intent
A guest who ordered without an account, or a customer who lost their email, must be able to ask "where is my order?" without signing in, and nobody may use the page to enumerate other people's orders.

## Requirements (quoted from the BRD)
> **FR-37:** Guest users shall track orders via an emailed link or a public tracking page using order number plus a verification value, with generic (non-leaking) error messaging on mismatch.

> **SEC-06:** The public guest order-tracking endpoint (order number + phone/email) shall be rate-limited per IP/session and shall require a CAPTCHA after a small number of failed lookups, to prevent order-number enumeration.

> **SEC-07:** Guest order-tracking error responses shall remain generic ('details do not match') regardless of which field was wrong, per the existing FR-37, now explicitly paired with the rate limit in SEC-06.

## User flow
BRD §5.5: the guest opens the Track Order page, enters order number and mobile number (owner decision: order number + mobile; email link also works), and sees the order status. A later sprint adds the courier status when a tracking number exists. Owner decision (customers): "order number + mobile, or tracking number alone (courier status only)".

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| OLK-1 | Given a correct order number and the mobile on the order, then the status, items summary and totals are shown | FR-37 |
| OLK-2 | Given a wrong order number, a wrong mobile or both, then the same generic "details do not match" response is returned, with the same shape and timing | SEC-07 |
| OLK-3 | Given repeated lookups from one IP or session, then they are rate-limited | SEC-06 |
| OLK-4 | Given a small number of failures, then a CAPTCHA (Turnstile, as in SEC-02) is required before further attempts | SEC-06 |
| OLK-5 | Given the emailed link, then it carries a signed, unguessable token for that order and opens the status page without further input | FR-37 |
| OLK-6 | Given the page output, then it never shows the full address or full phone number (masked) | SEC-25 (new) |
| OLK-7 | Given the mobile is compared, then both sides are normalised to the same format first (03xx…, +92…) | accounts rule (new) |
| OLK-8 | Given a tracking number alone, then only courier status is shown, no order data (S15) | owner decision |
| OLK-9 | Given the page, then it is `noindex`, uses semantic HTML, is keyboard-operable and WCAG 2.2 AA | NFR-19, SEO-05 |
| OLK-10 | Given a signed-in customer, then this page is not needed: their orders list is S15 | FR-36 |

## Non-functional constraints
Comparison of the second factor is constant-time; the response for "no such order" and "wrong mobile" is indistinguishable in status code, body and latency. Rate limit and CAPTCHA reuse the sign-in throttle and `ICaptchaVerifier` from customer accounts (S4).

## Out of scope
Courier tracking events (S15), order history for accounts (S15), editing or cancelling from this page.

## Open questions
- [x] **Q-OLK-1 Timing** (owner, 2026-11-02): order number + mobile look-up ships in S13; the tracking-number part ships in S15.
- [x] **Q-OLK-2 Does the emailed link expire?** Recommendation: no expiry, but it shows only status and masked data, and a new link can be requested. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-OLK-3 Failed lookups before the CAPTCHA and the rate limit numbers** (recommendation: 3 failures, then CAPTCHA; 20 attempts per hour per IP). **Approved (owner, 2026-11-02: approve all recommendations).**
