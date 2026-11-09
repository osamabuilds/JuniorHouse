# Bookings

- **Status:** Draft, applied by delegation (owner away, 2026-11-09)
- **Jira:** to be created
- **BRD sections:** §5.17, §5.18
- **Requirement IDs:** FR-SC-13, FR-SC-14, NFR-SC-04

## Problem & intent
When a parcel is packed it must get a waybill from a courier before it leaves the building. If the primary courier is down, the secondary takes it; the day's batch is never blocked.

## Requirements (quoted from the BRD)
> **FR-SC-13:** System shall book a shipment via the selected courier's API, capture the returned waybill/tracking number, and attach it to the order before the parcel leaves the building.

> **FR-SC-14:** System shall fall back to a secondary courier if the primary courier's booking API is unreachable, without blocking the day's outbound batch.

> **§5.17:** Practical sequencing: pack → weigh → book shipment with courier (get waybill number + courier barcode) → print combined shipping label ... Edge case: courier booking API times out after packing is done → parcel must not be stuck un-labelled; system falls back to a manual booking queue with a retry.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| BKG-1 | Given an order packed, when `OrderPackedEvent` arrives, then one shipment exists for the order and is booked with the primary courier | FR-SC-13 |
| BKG-2 | Given a successful booking, then the waybill and courier are stored on the shipment and the shipment is "Booked" | FR-SC-13 |
| BKG-3 | Given the primary courier is unreachable, when booking, then the secondary is tried at once and the shipment records which courier booked it | FR-SC-14 |
| BKG-4 | Given both couriers are unreachable, then the shipment is "Pending booking" in the manual queue and the day's other bookings are not delayed | FR-SC-14, NFR-SC-04 |
| BKG-5 | Given the same event delivered twice, then there is still one shipment and one booking (new) | NFR-09 |
| BKG-6 | Given a courier rejects the request, then the secondary is not tried blindly: the shipment goes to the manual queue with the reason (new) | §5.18 |
| BKG-7 | Given the booking needs the recipient and phone and the COD amount, then they are read from Orders at the moment of booking, never from an event (new) | ADR 0026, SEC-25 |
| BKG-8 | Given a cash on delivery order, then the COD amount is sent to the courier; a paid order sends zero (new) | §5.17 |
| BKG-9 | Given a booked shipment, then `ShipmentBookedEvent` is published with ids and codes only (new) | ADR 0030 |
| BKG-10 | Given an order cancelled after booking but before pick-up, then the shipment is cancelled with the courier when the port allows it, otherwise flagged for staff (new) | §5.18 |
| BKG-11 | Given a booking, then it records the staff member or "system" and the time | NFR-SC-05 |

## Non-functional constraints
The booking runs in a background worker, not in the pack request, so a slow courier never slows the packer. Booking is idempotent on the order id: a courier is asked at most once per attempt, and a waybill already stored is never replaced.

## Out of scope
Rule-based courier choice, rates, return pickups.

## Open questions
- [ ] **Q-BKG-1** See Q-SHP-2 (retry numbers).
