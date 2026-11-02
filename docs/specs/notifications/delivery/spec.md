# Notification delivery

- **Status:** Approved (owner approved all recommendations, 2026-11-02; plan awaiting approval)
- **Jira:** to be created on approval (sprint S13)
- **BRD sections:** §8.3, §9.5, §12.3.7
- **Requirement IDs:** FR-61, FR-62, FR-65, NFR-FT-01, NFR-FT-02, NFR-FT-03, NFR-FT-04, NFR-FT-05, NFR-FT-08, TC-NOTIF-01, 02, 03, 04, 07

## Problem & intent
Order confirmations and other critical messages must never be silently lost, whatever crashes or which provider is down, and a failing provider must never stop a customer from ordering.

## Requirements (quoted from the BRD)
> **FR-61:** The system shall guarantee at-least-once delivery of every business-critical notification (order confirmation, COD confirmation outcome, dispatch notice, return/exchange status update, Fit-Finder notify-me alert) via the Transactional Outbox + queue pattern defined in NFR-FT-01/02.

> **FR-62:** The system shall retry failed notification delivery automatically and route permanently-failed messages to a Dead-Letter Queue with an operational alert, per NFR-FT-03/04, instead of discarding them silently.

> **FR-65:** A failure in the notification subsystem shall never cause the underlying business transaction (e.g. order placement) to fail or roll back, per NFR-FT-08.

> **NFR-FT-01:** Every outbound notification (email, SMS, WhatsApp) shall be recorded via the Transactional Outbox pattern — written to the database in the same transaction as the business event that triggers it (e.g. order creation), so the notification intent is guaranteed to persist even if the app crashes immediately after.

> **NFR-FT-02:** A background publisher shall move outbox records onto Azure Service Bus (a persistent, disk-backed queue) for actual delivery, decoupling notification sending from the critical request/response path.

> **NFR-FT-03:** Failed delivery attempts shall retry automatically with exponential backoff (minimum 5 attempts) before being considered failed.

> **NFR-FT-04:** A message that exhausts all retries shall move to a Dead-Letter Queue (DLQ) rather than being discarded, and shall trigger an operational alert (Application Insights) so a human can investigate/resend.

> **NFR-FT-05:** Notification consumers shall be idempotent — re-processing the same message (a normal side-effect of at-least-once delivery) shall not result in a duplicate email/SMS/WhatsApp message reaching the customer.

> **NFR-FT-08:** The notification subsystem's failure shall never block or roll back the underlying business transaction — e.g. if the email service is down, the order itself must still be placed successfully; the notification is retried independently.

*NFR-FT-02 and NFR-FT-04 name Azure services that CLAUDE.md marks stale. See module decision D-2; the owner confirms the replacement.*

## User flow
A business event (order placed, COD cancelled, …) is written to the outbox in the business transaction. Notifications consumes it, decides channels, creates a notification record (Queued), and a worker sends it. Failure → retry with backoff → after the last attempt, dead-letter and alert. Admin can see and resend ([delivery-log](../delivery-log/spec.md)).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| DLV-1 | Given the process crashes right after the order commit, when it restarts, then the confirmation is still sent | TC-NOTIF-01, NFR-FT-01 |
| DLV-2 | Given the provider fails the first 2 attempts, then retry with exponential backoff succeeds later and the customer receives exactly one message | TC-NOTIF-02, NFR-FT-03 |
| DLV-3 | Given all retries fail (at least 5 attempts), then the message moves to the dead-letter store, an alert event fires and the order is unaffected | TC-NOTIF-03, NFR-FT-04 |
| DLV-4 | Given a message is redelivered, then a duplicate is not sent (idempotent consumer keyed by notification id and channel) | TC-NOTIF-04, NFR-FT-05 |
| DLV-5 | Given the whole subsystem is unreachable at order time, then the order completes and is visible in the customer's account | TC-NOTIF-07, FR-65 |
| DLV-6 | Given events carry ids only, then no address, phone, name or message body is in an outbox event (ADR 0011) | SEC-25 |
| DLV-7 | Given the retry schedule, then it is configuration (attempt count, base delay, maximum delay) with jitter and a floor of 5 attempts | NFR-FT-03 |
| DLV-8 | Given a permanent error (invalid address), then it is not retried five times pointlessly: it fails fast and may trigger fallback | FR-62 (new) |
| DLV-9 | Given two workers, then a notification is claimed by one (row lock, skip locked) so it is not sent twice at the same time | NFR-FT-05 |
| DLV-10 | Given a notification older than its usefulness (a COD notice sent 3 days late), then a per-kind expiry stops the send and records "Expired" (new) | FR-61 |
| DLV-11 | Given the queue backs up, then the worker processes in order of creation within priority and a metric/counter of queued age is exposed | NFR-FT-07 |
| DLV-12 | Given a dead-lettered message, then it keeps its payload references and attempt history for the resend | FR-62 |

## Non-functional constraints
Built on the existing outbox and dispatcher (ADR 0004), not a new broker. At-least-once with idempotency, not exactly-once. Message content is rendered at send time from fresh data, so a delayed message is never stale. No personal data in logs (SEC-25). Everything runs with fake senders locally.

## Out of scope
SMS, push, marketing campaigns, a separate message broker.

## Open questions
- [x] **Q-DLV-1 Replacement for Azure Service Bus / Application Insights** (module decision D-2): confirm the outbox + dead-letter table + structured alert event until the platform has alerting. Where should the "operational alert" go while there is no pager (log + admin dashboard counter + email to an ops address)? **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-DLV-2 Retry schedule numbers.** Recommendation: 6 attempts at about 1 min, 5 min, 30 min, 2 h, 6 h, 12 h with jitter. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-DLV-3 Per-kind expiry** (for example: order confirmation never expires within 7 days; COD cancel notice 24 hours). **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-DLV-4 Retention** of notification records and dead letters (they contain no body, only references; recommendation 12 months). **Approved (owner, 2026-11-02: approve all recommendations).**
