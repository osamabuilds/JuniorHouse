# Notification channels

- **Status:** Approved (owner approved all recommendations, 2026-11-02; plan awaiting approval)
- **Jira:** to be created on approval (sprint S13)
- **BRD sections:** §5.4, §8.3
- **Requirement IDs:** FR-31, FR-64, NFR-FT-06, SEC-21, TC-NOTIF-05

## Problem & intent
Customers should hear about their order on the channel they prefer, and a critical message must still arrive when that channel fails.

## Requirements (quoted from the BRD)
> **FR-31 (channel part):** The system shall generate a unique order number and send order confirmation via email and, where the customer has opted in, WhatsApp.

> **FR-64:** For critical notifications, the system shall automatically fall back to an alternate channel (e.g. email if WhatsApp fails) per NFR-FT-06, rather than depending on a single channel succeeding.

> **NFR-FT-06:** Critical customer-facing notifications (order confirmation, COD confirmation, dispatch notice, return/exchange status) shall support multi-channel fallback — e.g. if WhatsApp delivery fails, the system shall fall back to email rather than depending on a single channel.

> **SEC-21 (channel part):** Calls to third-party dependencies (payment gateway, WhatsApp API, courier API) shall be wrapped in a circuit-breaker pattern; if a channel is down, the system shall degrade gracefully […] rather than blocking the entire checkout flow.

## User flow
For each notification the channel plan is: email always; WhatsApp too if the customer opted in. If WhatsApp fails (unreachable number, provider down, circuit open) the email is the guaranteed path.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| CHN-1 | Given an opted-in customer, when an order is placed, then both email and WhatsApp are sent | FR-31 |
| CHN-2 | Given no opt-in, then no WhatsApp message is ever sent | FR-31 |
| CHN-3 | Given WhatsApp fails for an opted-in number, then the email is sent (or was already sent) and the record shows the fallback | TC-NOTIF-05, FR-64 |
| CHN-4 | Given the WhatsApp provider is down, then its circuit opens and sends stop quickly without blocking the worker; email continues | SEC-21 |
| CHN-5 | Given a guest, then the opt-in comes from the checkbox at checkout and is stored with a timestamp and the number it applies to | FR-31, checkout CHK-12 |
| CHN-6 | Given an opt-out (reply STOP or account setting), then WhatsApp stops at once | FR-31 (new) |
| CHN-7 | Given email is the only channel with an address, then a failed email is retried and dead-lettered per [delivery](../delivery/spec.md) (no second fallback) | NFR-FT-06 |
| CHN-8 | Given channel senders, then each sits behind a port with a fake for tests: `IEmailSender` (exists), `IWhatsAppSender` (new) | sprint plan |
| CHN-9 | Given message templates, then they are stored as versioned text with placeholders (not hard-coded in handlers) and HTML-encode every value | ADR 0022 note |
| CHN-10 | Given the same notification, then each channel is tracked separately (status per channel) | NFR-FT-07 |
| CHN-11 | Given an inbound WhatsApp event (button reply, STOP, delivery status), when it arrives at the webhook, then its signature is verified with the app secret before anything is read; an invalid or unsigned event is rejected, stored with `SignatureVerified = false` and logged as a security event | SEC-10 (new) |
| CHN-12 | Given a button reply to a COD confirmation message, then it is matched to the notification that was sent (provider message id), and the COD outcome command is called through the Orders contract; a reply that matches no sent message is ignored | COD-14 (new) |
| CHN-13 | Given the first business-started WhatsApp message to a number, then it uses an approved template and the number has an opt-in on record (CHN-5); no free-text business-started messages are ever sent | WhatsApp policy (new) |
| CHN-14 | Given provider delivery receipts (sent, delivered, read, failed), then the per-channel status is updated from them, so WhatsApp can show Delivered (email stays at Sent) | NFR-FT-07, DLG-10 |
| CHN-15 | Given a WhatsApp message is billed per message, then the number sent per day is counted and exposed with the delivery log so the business can watch cost (new) | owner request 2026-11-02 |

## Non-functional constraints
Provider credentials come from secrets. The WhatsApp Business API needs approved templates for business-initiated messages; the template list is part of the approval and must match the stored templates. Sender ports are mocked at the boundary; no real provider is called in tests.

## Out of scope
SMS (deferred, owner), a channel preference screen beyond the opt-in, marketing WhatsApp.

## Open questions
- [ ] **Q-CHN-1 WhatsApp provider** (Meta Cloud API directly or a reseller)? WhatsApp is IN scope for launch (owner, 2026-11-02). The plan assumes the Meta WhatsApp Cloud API directly (no reseller mark-up), behind `IWhatsAppSender` with a fake first; the business needs Meta business verification, a dedicated number and template approval, and must confirm the per-message rate on Meta's current rate card. The approval is an external lead item; the fake adapter is built regardless.
- [x] **Q-CHN-2 Where is the opt-in recorded for signed-in customers** (account setting in `Customers`, read through a contract) and does guest opt-in create a contact record or live on the order only? Recommendation: on the order only. **Approved (owner, 2026-11-02: approve all recommendations).**
- [ ] **Q-CHN-3 Language and tone** of templates (English only for launch?). The business supplies the copy.
- [ ] **Q-CHN-4 Sender identity:** from address, display name, WhatsApp business number.
