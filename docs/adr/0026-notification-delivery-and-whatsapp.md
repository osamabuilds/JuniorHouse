# 0026 — Notification delivery on the outbox, and WhatsApp in both directions

- Status: Proposed (owner approved the recommendations 2026-11-02; approval of this ADR comes with the plans)
- Date: 2026-11-02
- Supersedes in part: [0022](0022-email-sender-port.md) (the per-module email handler; the `IEmailSender` port and SMTP adapter stay)

## Context

Customer messages must never be lost and must never stop an order (FR-61, FR-62, FR-65, NFR-FT-01 to 08). The BRD names Azure Service Bus, a dead-letter queue and Application Insights, which CLAUDE.md marks stale (ADR 0003). The owner also wants COD orders confirmed from WhatsApp (replying to a message), which makes WhatsApp a two-way channel for the first time, and WhatsApp business-started messages are billed per message.

## Decision

1. **The queue is the PostgreSQL outbox** (ADR 0004) and the dead-letter queue is a table. No broker. Other modules write events with ids only inside their own transaction (NFR-FT-01). `Notifications` consumes each event through one mapper, creates a **notification** and one **channel delivery** per channel (email always; WhatsApp only if the customer opted in at checkout), in one transaction.
2. **A delivery worker** (a hosted background service) claims due channel deliveries with `SELECT … FOR UPDATE SKIP LOCKED` (so two workers never send the same one, DLV-9), renders the message **at send time** from fresh data read through `Orders.Contracts` (a late message is never stale, OCF-5), sends through the port, and records the attempt. Idempotency key: notification id plus channel (NFR-FT-05); a redelivered event finds the existing row.
3. **Retry** (configuration, floor of 5 attempts, DLV-7): six attempts at about 1 min, 5 min, 30 min, 2 h, 6 h and 12 h, with jitter. A permanent error (invalid address, number not on WhatsApp) fails fast, no retries (DLV-8). Per-kind expiry stops a message that is no longer useful and records `Expired` (DLV-10): order confirmation 7 days, COD cancel notice 24 hours, COD WhatsApp confirm request 12 hours.
4. **Dead letters.** An exhausted delivery gets a row in `NTFY.NTFY_DEAD` (references, attempt history, last error category; never the body) and raises the **operational alert**: a structured log event (`notification_dead_lettered`), a counter shown on the delivery-log screen, and an email to `Notifications:OpsAlertEmail`. (Replaces Application Insights until the platform has paging; Q-DLV-1.) Retention of notification rows and dead letters: 12 months.
5. **Fallback** (FR-64): if WhatsApp fails for an opted-in customer, the email path stands (it is always created; WhatsApp never replaces it). Each channel keeps its own status (CHN-10).
6. **Ports with fakes first:** `IEmailSender` (exists), `IWhatsAppSender` (new). The real WhatsApp adapter is the **Meta WhatsApp Cloud API called directly** (no reseller), behind `Microsoft.Extensions.Http.Resilience` (circuit breaker, timeout, MIT, version pinned at implementation and at least two weeks old). A circuit that opens makes WhatsApp sends fail fast and leaves email running (CHN-4).
7. **Templates are data**: `NTFY.NTFY_TMPL` holds versioned text with placeholders per kind and channel; the renderer HTML-encodes every value (CHN-9). WhatsApp templates must match what Meta approved, by name and version.
8. **Inbound WhatsApp** (the new part). One public endpoint, `/api/notifications/whatsapp/webhook`:
   - `GET` answers Meta's verification challenge using a configured verify token.
   - `POST` events are **verified first**: HMAC-SHA256 of the raw body with the app secret against the `X-Hub-Signature-256` header, constant-time; an invalid or missing signature gets 401, a stored row with `SignatureVerified = false` and a security log event, and nothing else (CHN-11, SEC-10).
   - Each event id is stored once (`NTFY.NTFY_WBHK`, unique on the provider event id), so a replay changes nothing.
   - A **button reply** is matched to the notification that was sent, by the provider message id of the message being replied to, and its sender number must equal the number the message was sent to; then `Orders.Contracts.ICodCustomerResponses.RecordAsync(orderId, Confirm | Cancel, via WhatsApp)` is called. No match: ignored (CHN-12). **STOP** adds the number's hash to `NTFY.NTFY_SUPR`, which every later WhatsApp send checks (CHN-6). Delivery receipts update channel status (CHN-14).
   - The webhook is anonymous but signature-gated, rate-limited and excluded from cookie and CORS assumptions.
9. **Cost control.** Business-started WhatsApp messages are billed. The COD confirm message is sent only to opted-in customers who have not already confirmed after a wait (default 60 minutes), by an `Orders` event, never at placement. A daily count of WhatsApp sends is stored and shown on the delivery-log screen (CHN-15). The rate is the business's to confirm from Meta's current rate card; nothing in code depends on it.
10. **Opt-in lives on the order** (checkout CHK-12: number, flag, timestamp), read through the Orders contract at send time (Q-CHN-2). No contact table is created. `NTFY_SUPR` holds only a salted hash of a number that said STOP.
11. **Customers' own emails** (restock notice, newsletter welcome) keep their ADR 0022 path in this sprint. Moving them onto this subsystem is the last, optional task of the module plan, done only if the earlier tasks are green.

## Alternatives considered

- **Azure Service Bus or another broker:** a new moving part and a paid service for volumes the outbox handles; the ADR 0004 decision stands.
- **A reseller or BSP for WhatsApp:** faster start, a second bill and a second place holding customer numbers. Revisit if Meta's own onboarding stalls (Q-CHN-1).
- **Send WhatsApp at placement:** pays for messages to customers who would have confirmed on the page anyway.
- **Confirm by free-text reply ("YES"):** needs parsing and handling of typos; reply buttons are explicit and cheaper to secure.

## Consequences

- At-least-once with idempotency, not exactly-once. A crash between sending and recording can cause one duplicate; the idempotency key and the provider message id make this rare, and it is accepted for transactional messages.
- A Meta business verification, a phone number and template approval are lead items outside the code; the fake sender lets everything else be built and tested now.
- One more anonymous public endpoint; its protections (signature, event-id uniqueness, rate limit, sender-number match) are in the plan's security table.
