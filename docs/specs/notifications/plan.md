# Notifications: module plan (Sprint 13)

- **Specs:** [delivery](delivery/spec.md) · [channels](channels/spec.md) · [order-confirmation](order-confirmation/spec.md) · [delivery-log](delivery-log/spec.md) · **Module README:** [README.md](README.md)
- **ADRs:** [0026 delivery on the outbox and WhatsApp in both directions](../../adr/0026-notification-delivery-and-whatsapp.md) (the main decision record for this module) · [0024 three modules](../../adr/0024-cart-orders-notifications-modules.md) · [0004 outbox](../../adr/0004-postgres-transactional-outbox.md) · [0022 email port](../../adr/0022-email-sender-port.md) · [0011](../../adr/0011-security-and-coupling-baseline.md) · [0016 audit](../../adr/0016-audit-trail-through-the-outbox.md)
- **Status:** Plan approved by the owner with all recommendations (2026-11-02); tasks written. Owner answers (2026-11-02): all recommendations approved (outbox plus dead-letter table plus log, counter and ops email as the alert; six retries; "Sent" is final for email; no bulk resend); WhatsApp is in scope for launch, in both directions (send, and the COD reply). **Still open and outside the code:** WhatsApp provider assumed to be the Meta Cloud API directly (Q-CHN-1), sender identities (Q-CHN-4), the message copy (Q-OCF-1, Q-CHN-3), and Meta's current per-message rate.

## Module layout

`Romp.Modules.Notifications.{Domain,Application,Infrastructure,Contracts}`, tests `Romp.Modules.Notifications.Tests`. `NotificationsModule : IModule`, DbContext `NtfyDbContext` (schema `NTFY`), `AddOutboxModule("NTFY", …)`, inbox `NTFY.INBX`. Hosted services: `DeliveryWorker`, `DeadLetterAlertWorker`, `PurgeWorker`.

| Feature | API folder | Storefront web | Admin web |
|---|---|---|---|
| delivery | `Delivery/` | n/a | n/a (counters appear in the delivery log) |
| channels | `Channels/` (email, WhatsApp ports and adapters, inbound webhook) | n/a | n/a |
| order-confirmation | `OrderMessages/` (event handlers, templates for the order kinds) | n/a | n/a |
| delivery-log | `DeliveryLog/` | n/a | `features/notifications/delivery-log` |

Build order (README): delivery → channels (email first, WhatsApp behind the fake, then the inbound webhook) → order-confirmation → delivery-log. Notifications reads `Orders.Contracts` only; it is built after the Orders contract exists (S12) and finished in S13.

## Domain model

- **`Notification`**: id, kind (lookup: `ORCF` order confirmation, `ORCN` order cancelled, `CODR` COD confirmation request), related reference (kind `ORDR` and the order id; ids only), created time, per-kind expiry time, priority. One per business event (unique on event id plus kind, so a redelivered event creates nothing new).
- **`ChannelDelivery`**: one per notification and channel (`EMAL`, `WHTS`): status (lookup: `QUED`, `SENT`, `DLVD`, `FAIL`, `DEAD`, `EXPR`, `SKIP`), attempt count, next attempt time, last error category (lookup: `TMPY` temporary, `PERM` permanent, `CIRC` circuit open, `OPTO` opted out or suppressed, `NOAD` no address), provider message id, `FallbackOfId?`. State machine in the domain: `QUED → SENT → DLVD`; `QUED → QUED` (retry, with backoff) `→ DEAD`; `QUED → EXPR`; `QUED → SKIP` (no opt-in, suppressed).
- **`RetryPolicy`** (pure): attempt number to next delay; base schedule 1 min, 5 min, 30 min, 2 h, 6 h, 12 h; jitter ±20 %; at least 5 attempts enforced at start-up (DLV-7); permanent errors skip the schedule (DLV-8).
- **`ChannelPlan`** (pure): given the kind and the order's opt-in and suppression facts, which channels to create. Email always; WhatsApp when opted in and the number is not suppressed (CHN-1, CHN-2, CHN-6). A WhatsApp failure never removes the email (CHN-3, FR-64).
- **`MessageTemplate`**: kind, channel, language (`EN`), version, subject, plain text, HTML, placeholders; renderer HTML-encodes every value (CHN-9, OCF-7). WhatsApp templates also carry the Meta template name and the button definitions.
- **`WhatsAppEvent`** inbound: provider event id, type (`BTNR` button reply, `STOP`, `RCPT` receipt), signature verified flag, body hash, matched notification, processed time.
- **`Suppression`**: salted SHA-256 of a normalised number that said STOP.

## Application layer

| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `OnOrderPlacedHandler` / `OnOrderPaidHandler` / `OnOrderCancelledHandler` / `OnCodConfirmationRequestedHandler` (event, through `OrderEventMapper`) | event | notification and channel deliveries created in one transaction | DLV-1, 4, 6; OCF-1 to 4, 6; COD-13 |
| `DeliveryWorker` (claim, render, send, record) | n/a | status changes, retries, dead letters | DLV-2, 3, 7 to 11, 12; CHN-3, 4, 10 |
| `RenderMessageQuery` | notification, channel | subject, text, HTML, or WhatsApp template parameters, built from fresh order data read through `IOrderNotificationQueries` | OCF-5, 7 |
| `HandleWhatsAppWebhookCommand` | raw body, signature header | verified, stored once, then routed | CHN-11, 12, 14; COD-14 to 16 |
| `VerifyWhatsAppSubscriptionQuery` | challenge, verify token | echo or refusal | CHN-11 |
| `AddSuppressionCommand` | number hash | future WhatsApp skipped | CHN-6 |
| `ListNotificationsQuery` / `GetNotificationQuery` | filters, page | masked rows | DLG-1, 5, 6, 9 |
| `ResendNotificationCommand` | notification or delivery id, acting staff | new `QUED` delivery with a link to the original | DLG-2, 3, 7, 8 |
| `PurgeOldNotificationsJob` | n/a | removes rows past retention (12 months) | Q-DLV-4 |

**Delivery worker contract:** claim up to a batch of due `QUED` deliveries with `SELECT … FOR UPDATE SKIP LOCKED` ordered by priority then created time, in a short transaction that sets a lease; render and send outside the transaction; record the attempt and the new state in a second short transaction. A crash after sending but before recording leaves the lease to expire and the next claim finds the provider message id unchanged, so a duplicate is possible but rare (ADR 0026 consequences). Queue age and counts per status are exposed as health values and shown on the delivery-log screen (DLV-11).

## API endpoints

| Method | Route | Auth / role | Ownership check (SEC-05) |
|---|---|---|---|
| GET | `/api/notifications/whatsapp/webhook` | anonymous (verify token) | n/a |
| POST | `/api/notifications/whatsapp/webhook` | anonymous, **signature-gated**, rate-limited, body at most 64 KB | matched by provider message id and sender number |
| GET | `/api/admin/notifications` | `notifications.view` | staff; recipients masked |
| GET | `/api/admin/notifications/{id}` | `notifications.view` | staff |
| POST | `/api/admin/notifications/{id}/resend` | `notifications.resend` | staff; idempotent on a client key |
| GET | `/api/admin/notifications/summary` | `notifications.view` | counts by status, queue age, dead-letter count, WhatsApp sends today |

## Events

| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|
| `OrderPlacedEvent`, `OrderPaidEvent`, `OrderCancelledEvent`, `CodConfirmationRequestedEvent` (`Orders.Contracts`) | Orders | Notifications (one mapper: `OrderEventMapper`) | yes (Orders' outbox) |
| `NotificationDeadLetteredEvent` v1 (notification id, kind, channel) | Notifications | the alert worker (log, counter, ops email) | yes |
| `NotificationSentEvent` v1 | Notifications | audit | yes |

## Contracts

| Need | Provider | Change |
|---|---|---|
| Contact, lines, totals, masked address, method, WhatsApp opt-in and status of an order, by order id | `Orders.Contracts.IOrderNotificationQueries` | **new** (orders plan) |
| Record the customer's COD reply | `Orders.Contracts.ICodCustomerResponses` | **new** (orders plan) |
| Email sending | `Romp.BuildingBlocks.Email.IEmailSender` | exists (ADR 0022) |
| WhatsApp sending | `IWhatsAppSender` in `Notifications.Application` | **new**, fake first; Cloud API adapter later |
| Resilience | `Microsoft.Extensions.Http.Resilience` | **new dependency**, shared with Orders |

## Data (schema `NTFY`, names per `docs/db/naming.md`)

| Table | Notes |
|---|---|
| `NTFY_MAIN` | `NTFY_KIND_ID`, `REF_KIND_ID` and `REF_ID` (plain reference), `EVNT_ID` unique with the kind, `EXPR_DTE`, `PRIO`; **partitioning key noted: `INSR_DTE` by month (BRD §12.6)**, not applied until volume asks |
| `NTFY_CHNL` | `NTFY_ID` FK, `CHNL_ID` lookup, `CHNL_STS_ID` lookup, `ATMP_CNT`, `NXT_ATMP_DTE`, `LEAS_DTE`, `ERR_CAT_ID` lookup null, `PRVD_MSG_ID` (indexed; used to match replies), `FLBK_OF_ID` null; unique `(NTFY_ID, CHNL_ID)` for the original (idempotency); partial index for due work |
| `NTFY_ATMP` | append-only attempt history: delivery id, attempt number, outcome, error category, time (no body, no address) |
| `NTFY_DEDL` | dead letter: delivery id, kind, channel, reason category, attempt count, time, resent-by and when |
| `NTFY_TMPL` | versioned templates (kind, channel, language, version, subject, text, HTML, Meta template name, active); unique on kind, channel, language and version |
| `NTFY_WBHK` | inbound events: provider event id unique, type, `SGNT_VRFD_IND`, body hash, matched delivery id, received and processed times |
| `NTFY_SUPR` | salted number hash, time; unique |
| `NTFY_DAY_CNT` | per day and channel send counts (cost watch, CHN-15) |
| Lookups (standard shape, seeded): `NTFY_KIND_LKP`, `CHNL_LKP`, `CHNL_STS_LKP`, `ERR_CAT_LKP`, `WBHK_TYP_LKP` | |
| `OUTB_MSG`, `INBX` | module |

No message body and no address or phone is stored in any `NTFY` table (recipients are re-read from Orders at send time; the log screen shows masked values read the same way). Audit columns and `xmin` on every transactional table; foreign keys declared and indexed inside the schema; no cross-schema foreign keys.

**New glossary words** (added with the migration): `NTFY`, `CHNL`, `ATMP`, `DEAD`, `TMPL`, `WBHK`, `SUPR`, `LEAS`, `PRVD`, `FLBK`, `PRIO`, `ERR`, `CAT`, `CNT`, `NXT`, `KIND`, `TYP`, `EVNT`. Reused: `ID`, `STS`, `LKP`, `DTE`, `IND`, `MSG`.

## Policies, configuration and the web edge

- Options `Notifications:*`, validated at start: retry schedule (list, floor of 5 attempts), `Jitter` 0.2, `ClaimBatchSize` 20, `LeaseSeconds` 120, per-kind expiry (orders 7 days, COD cancel 24 hours, COD WhatsApp 12 hours), `RetentionMonths` 12, `OpsAlertEmail`, `WhatsApp:Enabled`, `WhatsApp:PhoneNumberId`, `WhatsApp:AccessToken` (secret), `WhatsApp:AppSecret` (secret, signature), `WhatsApp:VerifyToken` (secret), `Email:FromAddress` and name (Q-CHN-4). A missing secret while `WhatsApp:Enabled` is true stops the host outside Development; the fake sender is refused outside Development and Testing.
- Admin app: one lazy route `notifications/delivery-log` (paged list, filters for status, channel, kind and dead letters; detail with attempts; Resend with a duplicate warning when already delivered; summary strip with queue age, dead letters and WhatsApp sends today). Admin keeps its look; write buttons behind `*hasPermission`.
- The webhook route is on the API host (the provider calls it directly); it is not proxied through the storefront.

## Security & coupling review

| Area | How this module meets it |
|---|---|
| Authorization | Admin endpoints in `MapSecuredGroup` with `notifications.view` / `notifications.resend`, checked server-side on every request (SEC-08, DLG-4); the webhook is anonymous but every POST is signature-verified before any parsing or state change; matrix test rows |
| Input validation | webhook: size cap, signature first, JSON schema check, unknown fields ignored, string lengths capped; admin filters validated; resend body is an id plus a client key |
| Output encoding | every template value HTML-encoded; plain-text twin; WhatsApp parameters length-capped and stripped of control characters; headers built by the email library, never by string concatenation (no header injection) |
| Outbound calls | SMTP host and WhatsApp Graph base URL from configuration only, allow-listed host, timeouts, circuit breaker per channel (SEC-21); nothing derived from user input |
| Resource limits | webhook body 64 KB and rate limit; claim batch size; admin lists paged 1..100; retry caps; purge job in batches |
| Concurrency | `SKIP LOCKED` claims with a lease; unique `(notification, channel)`; unique provider event id; `xmin` on rows staff can resend |
| Idempotency | inbox on every consumed event; notification unique on event id and kind; webhook events stored once; resend keyed by client key (DLG-8) |
| Audit | every resend and every dead letter recorded with who and when through the audit trail (FR-59, ADR 0016); inbound events kept as hashes and ids; security events for bad signatures |
| Data protection | no body, address or phone in the tables, events or logs (SEC-25); numbers appear only masked on screen; STOP stored as a salted hash; opt-in is read from the order, with its timestamp |
| Secrets and dangerous configuration | access token, app secret and verify token from secrets, never committed or logged; the host refuses insecure combinations outside Development |
| Error responses | problem details only; webhook answers 200 quickly to valid events and 401 to invalid signatures, never echoing input |
| New dependencies | `Microsoft.Extensions.Http.Resilience` (MIT), shared with Orders; MailKit already present |
| Coupling | reads `Orders.Contracts` only; consumes Orders events through one mapper; Orders never references Notifications; the COD reply goes back through the Orders contract call, not a table |
| Abuse cases | (1) forged WhatsApp POST to confirm or cancel someone's order: signature, then sender number must equal the order's number, then message id must match what we sent; (2) replay of a real event: event id stored once; (3) flooding the webhook: size cap and rate limit before work; (4) enumerating notifications in the admin: permission plus paging; (5) a customer texting STOP then ordering again: suppression is checked on every WhatsApp send |

## Risks

- **WhatsApp lead time and cost.** Business verification, a number and template approval take days and are outside the code; the fake sender and fake inbound events let every path be tested meanwhile. The per-message rate is unconfirmed (reports suggest about US$0.015 in Pakistan from October 2026): the daily count on the delivery-log screen makes the cost visible from day one.
- **At-least-once means a rare duplicate** after a crash between send and record; accepted for transactional messages (ADR 0026).
- **Delivered status for email** is not knowable without provider webhooks; "Sent" is final for email (Q-DLG-2).
- **Integration tests cannot load locally built DLLs** on the developer machine (Application Control): the Linux SDK container and CI run them, and results say so.
