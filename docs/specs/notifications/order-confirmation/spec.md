# Order confirmation

- **Status:** Draft
- **Jira:** to be created on approval (sprint S13)
- **BRD sections:** §5.4, §8.3
- **Requirement IDs:** FR-31, FR-61, TC-NOTIF-01, TC-CHK-03

## Problem & intent
The shopper must get proof of their order immediately, and a second message when a COD order is cancelled for lack of an answer.

## Requirements (quoted from the BRD)
> **FR-31:** The system shall generate a unique order number and send order confirmation via email and, where the customer has opted in, WhatsApp.

> **FR-61 (the messages in this sprint):** […] every business-critical notification (order confirmation, COD confirmation outcome, […]).

## User flow
`OrderPlacedEvent` → confirmation (email, WhatsApp if opted in) with order number, lines, totals, delivery address summary (masked), payment method and what happens next (COD: "we will call you to confirm"). `OrderCancelledEvent` with reason "COD not confirmed" or staff cancel → a cancellation notice. The email also carries the guest tracking link ([order-lookup](../../orders/order-lookup/spec.md)).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| OCF-1 | Given a placed order, then a confirmation is created in the same transaction path as the event and delivered | TC-CHK-03, TC-NOTIF-01 |
| OCF-2 | Given a COD order, then the message says it is pending confirmation and when the call will come | FR-32 (new) |
| OCF-3 | Given a card order, then the message is sent only after payment is confirmed, not at placement | PAY-1 (new) |
| OCF-4 | Given a COD auto-cancel or a staff cancel, then the customer receives a cancellation notice with the reason category | TC-CHK-15 |
| OCF-5 | Given the message, then it is rendered from the order as stored at send time (prices, items, address snapshot), never from the cart | SEC-09 |
| OCF-6 | Given the same event is processed twice, then only one confirmation reaches the customer | NFR-FT-05 |
| OCF-7 | Given the HTML email, then it is accessible (semantic, alt text, readable without images) and has a plain-text twin | NFR-19 (new) |
| OCF-8 | Given no deliverable address or number, then the failure is recorded and visible to staff rather than lost | NFR-FT-07 |

## Non-functional constraints
Content from stored templates ([channels](../channels/spec.md) CHN-9); no card data; masked contact data; transactional mail has no marketing content and no unsubscribe header.

## Out of scope
Dispatch, delivery, return and exchange notices (later sprints reuse this framework).

## Open questions
- [ ] **Q-OCF-1 Copy and branding** for the email and WhatsApp template: the business supplies the text; placeholders are used until then.
- [ ] **Q-OCF-2 Should the account holder's confirmation use the account email or the checkout email** if they differ? Recommendation: the checkout email on the order.
