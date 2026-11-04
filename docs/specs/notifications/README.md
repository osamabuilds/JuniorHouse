# Notifications

Guaranteed customer messages (Sprint 13, "Confirm"): the order confirmation by email and WhatsApp, delivery that survives crashes and provider outages (retry, dead-letter, fallback channel), idempotent consumers, a delivery status per message, and manual resend from the admin. It replaces the small email port of ADR 0022 with the full subsystem.

**Status:** Specs approved by the owner with all recommendations (2026-11-02); [plan](plan.md) drafted and awaiting approval (then tasks) · **Schema:** `NTFY` (proposed) · **Code:** `src/api/src/Modules/Notifications/` (planned) · **Web:** `features/notifications/` in the admin app (planned) · **Jira:** SCRUM-212 to SCRUM-215 · **Rules:** [constitution](../../CONSTITUTION.md), [CONVENTIONS](../../CONVENTIONS.md), [CLAUDE.md](../../../CLAUDE.md)

## Features

| Feature | What it does | Requirements | Status |
|---|---|---|---|
| [delivery](delivery/spec.md) | Outbox to channel handlers with retry (5+ attempts, backoff), dead-letter and alert, idempotent consumers, never blocks the business transaction | FR-61, FR-62, FR-65, NFR-FT-01..05, NFR-FT-08, TC-NOTIF-01..04, 07 | Draft |
| [channels](channels/spec.md) | Email and WhatsApp behind ports, opt-in, multi-channel fallback | FR-64, NFR-FT-06, FR-31, TC-NOTIF-05 | Draft |
| [order-confirmation](order-confirmation/spec.md) | The order confirmation (and COD-cancelled notice) message content and triggers | FR-31, FR-61, TC-NOTIF-01 | Draft |
| [delivery-log](delivery-log/spec.md) | Admin view of every notification's status (Queued/Sent/Delivered/Failed) and manual resend | FR-63, NFR-FT-07, FR-59, TC-NOTIF-06 | Draft |

Order of work: delivery → channels (email first, WhatsApp behind a fake) → order-confirmation → delivery-log.

## Module decisions to confirm

| ID | Proposal |
|---|---|
| D-1 | **New module `Notifications` (schema `NTFY`)** (owner chose three modules, 2026-11-02). Other modules raise events with ids only (ADR 0011, 0022); Notifications loads what it needs through each module's `*.Contracts` at send time. Needs an ADR. |
| D-2 | **"Azure Service Bus", "Dead-Letter Queue" and "Application Insights" in NFR-FT-02/04 are stale wording** (CLAUDE.md: Azure is replaced). Proposal: the PostgreSQL outbox (ADR 0004) plus a dispatcher is the queue, a `NTFY` dead-letter table is the DLQ, and the "operational alert" is a structured security/ops log event plus an admin counter, until observability (platform ADR 0017) provides alerting. Raised, not silently resolved: owner confirms. |
| D-3 | **ADR 0022 is superseded in part:** the `IEmailSender` port and SMTP adapter stay; the per-module email handler in Customers is migrated to Notifications in this sprint (customers keep raising the same event). |
| D-4 | **WhatsApp behind `IWhatsAppSender` with a fake first** (approval lead time is on the sprint plan's "start now" list). Opt-in is explicit, per number, stored with a timestamp. |

## Requirement IDs for the module

FR-31, FR-59, FR-61, FR-62, FR-63, FR-64, FR-65, NFR-FT-01..08, SEC-25. Test cases TC-NOTIF-01..07. Criterion IDs are prefixed by feature: `DLV-`, `CHN-`, `OCF-`, `DLG-`.

## Out of scope for the sprint

Dispatch, return and exchange notices (later sprints add templates to this subsystem; the framework here is built for them), marketing campaigns and newsletters beyond what Customers already sends (campaign tooling is post-launch), SMS (deferred, owner), push notifications.
