# Tracking

- **Status:** Draft, applied by delegation (owner away, 2026-11-09)
- **Jira:** to be created
- **BRD sections:** §5.19
- **Requirement IDs:** FR-SC-16, SEC-06, SEC-07, OLK-8

## Problem & intent
Once a parcel is with the courier, its progress must reach the customer without anyone typing it in, and a failed attempt must reach them before the parcel is sent back.

## Requirements (quoted from the BRD)
> **§5.19:** Courier webhook pushes (or polling fallback where a courier lacks webhooks) update shipment status: Booked → Picked Up → In Transit → Out for Delivery → Delivered / Failed Attempt / Returned to Origin (RTO). Each status push updates the Order's tracking timeline shown to the customer.

> **§5.19:** Failed Delivery Attempt: ... Romp's system should surface this to the customer proactively (address issue? unavailable?) rather than the customer only discovering RTO after the fact.

> **FR-SC-16:** System shall update the customer-visible order-tracking timeline from courier status webhooks (or polling fallback) in near-real-time.

> **SEC-06:** The public guest order-tracking endpoint ... shall be rate-limited per IP/session and shall require a CAPTCHA after a small number of failed lookups. **SEC-07:** ... error responses shall remain generic.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| TRK-1 | Given a status push with a valid signature, when received, then the shipment moves to that status and the event is stored once | FR-SC-16 |
| TRK-2 | Given a push with a missing or wrong signature, when received, then nothing changes and the answer is generic (the same as for an unknown waybill) | SEC-10, SEC-07 |
| TRK-3 | Given the same courier event id twice, then the second changes nothing and answers success (new) | NFR-09 |
| TRK-4 | Given a push for an earlier status than the shipment already has, then it is stored in the history but does not move the shipment back (new) | §5.19 |
| TRK-5 | Given a status change, then `ShipmentStatusChangedEvent` is published (order number, status code, waybill) and Orders moves the order to Dispatched on pick-up and Delivered on delivery | §5.19 |
| TRK-6 | Given a courier without webhooks, when the polling worker runs, then shipments in transit are asked for their status and updated the same way | §5.19 |
| TRK-7 | Given a Failed Attempt, then the customer is notified at once (kind "delivery attempt failed") with the reason code, and staff see the shipment flagged | §5.19 |
| TRK-8 | Given RTO, then the shipment is "Returned to origin" and the event tells the later returns sprint; nothing is restocked here (new) | §5.19 |
| TRK-9 | Given a tracking number alone, when looked up publicly, then only the courier status timeline is shown, no order data (OLK-8) | owner decision |
| TRK-10 | Given a lookup that matches nothing, then the answer is the same generic message whatever was wrong, after the same rate limit and CAPTCHA rules as order look-up (SEC-06, SEC-07) | SEC-06/07 |
| TRK-11 | Given a pushed body, then it is size-limited, read only after the signature is checked, and nothing personal is logged (new) | SEC-25 |
| TRK-12 | Given a delivered cash on delivery shipment, then the delivered time and the COD amount are kept for the later reconciliation (new) | FR-SC-17 (S16) |
| TRK-13 | Given staff open a shipment, then they see its full timeline and the booking history | §5.19 |

## Out of scope
COD remittance reconciliation, return pickups, rider contact.

## Open questions
See Q-SHP-4.
