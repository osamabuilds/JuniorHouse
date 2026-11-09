# Courier port

- **Status:** Draft, applied by delegation (owner away, 2026-11-09)
- **Jira:** to be created
- **BRD sections:** §5.18
- **Requirement IDs:** FR-SC-13, FR-SC-14, NFR-SC-04

## Problem & intent
Romp will use more than one courier, and each has its own API. The rest of the system must not know which. One port hides them, and a fake courier lets everything be built and tested before any account exists.

## Requirements (quoted from the BRD)
> **§5.18:** Shipment booking call to the chosen courier's API returns a tracking/waybill number and (for some couriers) a pre-formatted label PDF Romp can print directly instead of generating its own.

> **§5.19:** Courier webhook pushes (or polling fallback where a courier lacks webhooks) update shipment status: Booked → Picked Up → In Transit → Out for Delivery → Delivered / Failed Attempt / Returned to Origin (RTO).

> **NFR-SC-04:** Courier API integrations must degrade gracefully (secondary courier fallback, manual-booking queue) rather than blocking outbound shipping end-to-end.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| CPT-1 | Given a booking request (order number, recipient, address, phone, COD amount, weight), when a courier books it, then the answer carries a waybill number (and optionally a label) | §5.18 |
| CPT-2 | Given a courier that cannot be reached or times out, when booking, then the answer is "unreachable", never an exception | NFR-SC-04 |
| CPT-3 | Given a courier that refuses the request (bad address), when booking, then the answer is "rejected" with a short reason, and is not retried as if it were an outage (new) | §5.18 |
| CPT-4 | Given a waybill, when asked for its status, then the port answers with the courier status and time (polling fallback) | §5.19 |
| CPT-5 | Given a status push body and signature, when parsed, then the port answers the waybill, the status, the courier's event id and time, or "invalid" when the signature does not verify | §5.19, SEC-10 |
| CPT-6 | Given the fake courier, when an outage is switched on, then every booking answers "unreachable" until it is switched off (a test and demo switch) | NFR-SC-04 |
| CPT-7 | Given a courier code that is unknown or disabled, when a gateway is asked for, then none is returned | D-4 |
| CPT-8 | Given any request or answer, then it carries no more personal data than the courier needs, and nothing is logged beyond the order number and waybill (new) | SEC-25 |

## Non-functional constraints
Each call has a timeout (`Shipping:CourierTimeoutSeconds`, default 10). The port never throws for an ordinary courier problem.

## Out of scope
Real courier adapters (owner's accounts), rate quotes, return pickups (S17).

## Open questions
- [ ] **Q-CPT-1** Which real couriers first? See the module README, Q-SHP-1.
