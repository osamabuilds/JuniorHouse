# Notifications (Sprint 13): tasks

Ordered, checkable steps; test-first; .NET in the Linux SDK container. Plan: [plan.md](plan.md) (approved 2026-11-02) · ADR 0026 · Depends on: the `Orders.Contracts` tasks P3 and the cart foundations F1 to F7. After the module: rebuild docker and tell the owner what to try (Mailpit shows the emails; the fake WhatsApp sender shows its messages in the delivery log).

## Skeleton

- [x] **N1** Projects `Romp.Modules.Notifications.{Domain,Application,Infrastructure,Contracts}` and `Romp.Modules.Notifications.Tests`, `NotificationsModule : IModule`, `NtfyDbContext` (schema `NTFY`), outbox and inbox, solution and `Program.cs` entries. *Proof:* `NotificationsMigrationTests`, architecture tests.

## delivery

- [x] **V1** Domain: `Notification`, `ChannelDelivery` state machine, `RetryPolicy` (six steps, jitter, floor of 5), `ChannelPlan`. *Proof:* `ChannelDeliveryTests`, `RetryPolicyTests`, `ChannelPlanTests` (DLV-7, 8; CHN-1, 2, 3).
- [x] **V2** Migration: `NTFY_MAIN`, `NTFY_CHNL`, `NTFY_ATMP`, `NTFY_DEDL`, `NTFY_TMPL`, `NTFY_DAY_CNT`, lookups. *Proof:* `NotificationsMigrationTests`, naming and glossary tests.
- [x] **V3** Event handlers through `OrderEventMapper` create notification and deliveries in one transaction, idempotent on event id. *Proof:* `OrderEventHandlerTests` incl. redelivery (DLV-1, 4, 6).
- [x] **V4** `DeliveryWorker`: `SKIP LOCKED` claim with lease, render, send, record; two workers never send one delivery twice. *Proof:* `DeliveryWorkerTests`, `DeliveryClaimConcurrencyTests` (DLV-2, 9).
- [x] **V5** Exhaustion moves to `NTFY_DEDL` and raises the alert: log event, counter, email to `OpsAlertEmail`. *Proof:* `DeadLetterTests` (DLV-3, 12), `DeadLetterAlertTests`.
- [x] **V6** Per-kind expiry and queue-age health value. *Proof:* `ExpiryTests`, `QueueAgeHealthTests` (DLV-10, 11).
- [x] **V7** The business transaction never fails because of Notifications. *Proof:* `NotificationOutageTests.OrderPlaced_WhenSubsystemDown` against the real outbox (DLV-5, TC-NOTIF-07).
- [x] **V8** Purge job (12 months). *Proof:* `PurgeJobTests`.

## channels

- [x] **H1** `IWhatsAppSender` and `FakeWhatsAppSender` (records sends, configurable failures, can raise inbound events); `IEmailSender` used as is. *Proof:* `FakeWhatsAppSenderTests`; `CHN-8` architecture check.
- [x] **H2** Templates as data (`NTFY_TMPL`), renderer with HTML encoding and plain-text twin; seed order confirmation, order cancelled, COD confirmation request (placeholder copy until the business supplies it). *Proof:* `TemplateRendererTests` incl. encoding of hostile values (CHN-9, OCF-7).
- [x] **H3** Circuit breaker per channel using `Microsoft.Extensions.Http.Resilience` 10.0.0 (Polly strategy; the HTTP adapter uses a plain timeout and relies on this breaker, not the standard handler, to avoid a second retry schedule). *Proof:* `ChannelCircuitTests` (CHN-4).
- [x] **H4** Fallback and opt-in: no opt-in means no WhatsApp; WhatsApp failure leaves email standing; status tracked per channel. *Proof:* `FallbackTests`, `OptInTests` (CHN-1, 2, 3, 5, 10).
- [x] **H5** Inbound webhook: GET verification, POST signature check before parsing, event stored once, 64 KB cap, rate limit. *Proof:* `WhatsAppWebhookTests` (valid, missing or bad signature, replay, oversize: CHN-11).
- [x] **H6** Button reply routing: match by provider message id and sender number, call `ICodCustomerResponses`, ignore unmatched. *Proof:* `CodReplyRoutingTests` (CHN-12, COD-14, 15, 16).
- [x] **H7** STOP adds a salted number hash to `NTFY_BLCK` (the glossary already uses SUPR for "superseded"); every WhatsApp send checks it. *Proof:* `SuppressionTests` (CHN-6).
- [x] **H8** Delivery receipts update channel status; daily send counter. *Proof:* `ReceiptTests`, `DailyCountTests` (CHN-14, 15, DLG-10).
- [x] **H9** Real WhatsApp Cloud API adapter written against Meta's documented request shapes behind the port, disabled by default, needing the secrets; sandbox check once the owner has the number and verification. *Proof:* `WhatsAppCloudAdapterTests` with a stub HTTP handler (request shape, error mapping); the live check is recorded in "As built" when done.

## order-confirmation

- [ ] **M1** Confirmation after placement for COD, after payment for online orders; rendered from the stored order at send time. *Proof:* `OrderConfirmationTests` (OCF-1, 2, 3, 5, 6).
- [ ] **M2** Cancellation notice with the reason category. *Proof:* `OrderCancelledNoticeTests` (OCF-4).
- [ ] **M3** COD confirmation request (WhatsApp with Confirm and Cancel buttons; page link in the email). *Proof:* `CodRequestMessageTests` (COD-13, 18).
- [ ] **M4** No deliverable address or number is recorded and visible to staff. *Proof:* `NoAddressTests` (OCF-8).

## delivery-log

- [ ] **G1** Paged queries with masked recipients, filters, dead-letter view, summary. *Proof:* `DeliveryLogQueryTests` incl. query count and masking (DLG-1, 5, 6).
- [ ] **G2** `ResendNotificationCommand`, audited, idempotent on a client key, duplicate warning data. *Proof:* `ResendTests` (DLG-2, 3, 7, 8).
- [ ] **G3** Endpoints with `notifications.view` / `notifications.resend`; matrix rows. *Proof:* `DeliveryLogEndpointsTests`, authorization matrix (DLG-4).
- [ ] **G4** Admin `features/notifications/delivery-log` (list, filters, detail, resend dialog, summary strip), `*hasPermission`, link from the order screen. *Proof:* component and store specs, `delivery-log.e2e.spec.ts` (DLG-9).

## Optional last task (only if everything above is green)

- [ ] **X1** Move Customers' restock and newsletter-welcome emails onto this subsystem (they keep raising the same events). *Proof:* existing Customers email tests stay green against the new handler.

## Close the loop

- [ ] **Z1** Architecture tests green (only the known `zz-live-demo` failure); full build with 0 warnings; unit and e2e green on the docker secure stack; spec "As built"; Jira issues created and linked.
- [ ] **Z2** Rebuild docker; tell the owner where to see the emails (Mailpit :8025) and the fake WhatsApp messages, and what is still needed from the business (WhatsApp number, Meta verification, template approval, copy).
