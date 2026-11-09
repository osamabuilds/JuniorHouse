# Delivery log and resend (admin)

- **Status:** Approved (owner approved all recommendations, 2026-11-02; plan awaiting approval)
- **Jira:** SCRUM-215 (sprint S13)
- **BRD sections:** §8.3, §7.9
- **Requirement IDs:** FR-63, NFR-FT-07, FR-59, FR-60, SEC-08, TC-NOTIF-06

## Problem & intent
When a customer says "I never got my confirmation", support must see in seconds whether the system failed and, if so, resend.

## Requirements (quoted from the BRD)
> **FR-63:** The system shall support manual resend of any notification from the admin panel, using the tracked delivery status (Queued/Sent/Delivered/Failed) per NFR-FT-07.

> **NFR-FT-07:** Every notification shall have a tracked delivery status (Queued, Sent, Delivered, Failed) visible in the admin panel, so a support agent can confirm whether a customer's 'I never got my confirmation' complaint reflects an actual system failure and manually trigger a resend.

## User flow
Support opens an order (or searches the log by order number, email or mobile) and sees each notification, channel, status, attempts and last error category. They press Resend on a Failed (or any) notification; it is re-queued and delivered; the status updates.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| DLG-1 | Given notifications exist, when support open the log, then they see a paged list with kind, channel, status, attempts, times and related order | NFR-FT-07 |
| DLG-2 | Given a Failed notification, when support press Resend, then it is re-queued, delivered, and the status becomes Sent | TC-NOTIF-06, FR-63 |
| DLG-3 | Given a resend, then it is attributable to the acting user and timestamped (audit trail) | FR-59 |
| DLG-4 | Given a user without the permission, when they call the endpoint, then it is refused server-side | SEC-08 |
| DLG-5 | Given a dead-lettered message, then it appears in a Dead-letter filter with its last error category | FR-62 |
| DLG-6 | Given the log screen, then recipients are masked (a***@mail.com, 03XX-XXX1234) and message bodies are not shown | SEC-25 |
| DLG-7 | Given a resend on a message already Delivered, then a confirmation dialog warns of a duplicate (new) | NFR-FT-05 |
| DLG-8 | Given a resend, then it is idempotent against double-clicks (new) | NFR-FT-05 |
| DLG-9 | Given the order screen in the queue, then it links to that order's notifications | OQ-7 |
| DLG-10 | Given "Delivered", then it is shown only when the channel provider reports delivery (email may stay at Sent) | NFR-FT-07 |

## Non-functional constraints
SEC-08 permissions `notifications.view` and `notifications.resend` in the S8 roles catalogue (support, order-ops). Paged, constant-query. Admin look unchanged; semantic HTML; write buttons hidden by `*hasPermission`.

## Out of scope
Editing templates, bulk resend, customer-facing history.

## Open questions
- [x] **Q-DLG-1 Roles.** Which roles may resend (support and order-ops yes)? Confirm. **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-DLG-2 "Delivered" for email** is rarely knowable without provider webhooks. Accept "Sent" as the final email state for launch? **Approved (owner, 2026-11-02: approve all recommendations).**
- [x] **Q-DLG-3 Bulk resend of dead letters** after an outage: needed at launch? Recommendation: no, one at a time, revisit if an outage shows the need. **Approved (owner, 2026-11-02: approve all recommendations).**

## As built
Built (tasks G1-G4). Differences from the plan: log rows and `{id}` are channel deliveries; masking and exact-match search come from the new `Orders.Contracts.IOrderContactSummaries`; plain services instead of MediatR commands; new column `NTFY_CHNL.RSND_KEY` (unique) for the client resend key; a Sent or Delivered message needs `acknowledgeDuplicate` (email never goes beyond Sent, so both warn); the dead-letter filter shows dead deliveries not yet resent; the resend question is an inline confirmation, not a modal. Resend answers 202, 200 on the same key, or 409 with a code. Not covered: the Playwright spec `e2e/notifications/delivery-log.e2e.spec.ts` was not run, because the secure docker stack needs an owner sign-in with MFA (`E2E_OWNER_PASSWORD`).
