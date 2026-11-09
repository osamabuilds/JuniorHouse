# Manual booking queue

- **Status:** Draft, applied by delegation (owner away, 2026-11-09)
- **Jira:** to be created
- **BRD sections:** §5.17
- **Requirement IDs:** NFR-SC-04, FR-SC-14

## Problem & intent
When no courier will take a booking, the parcel must not be shipped unlabelled or forgotten. It waits, clearly marked, and is retried; staff can also enter a waybill they obtained by phone.

## Requirements (quoted from the BRD)
> **§5.17:** Edge case: courier booking API times out after packing is done → parcel must not be stuck un-labelled; system falls back to a manual booking queue with a retry, and the parcel physically waits in a clearly marked 'pending booking' staging area rather than being shipped unlabelled or left ambiguous.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| MBQ-1 | Given a shipment pending booking, when staff open the queue, then they see it with the order number, the reason, the attempts and the time it has waited, oldest first, paged | §5.17 |
| MBQ-2 | Given a pending shipment, when the retry time comes, then the worker tries again (primary, then secondary) up to the configured attempts | §5.17 |
| MBQ-3 | Given staff press "Try again now", then the booking is tried once at once, and a repeat press while one is running does nothing (new) | §5.17 |
| MBQ-4 | Given staff have a waybill from the courier by phone, when they enter courier and waybill, then the shipment is booked with that waybill and the source is recorded as manual (new) | §5.17 |
| MBQ-5 | Given a waybill that is already used by another shipment, then it is refused (new) | FR-SC-13 |
| MBQ-6 | Given the attempts are used up, then the shipment stays in the queue marked "needs staff" and a count is shown on the screen (new) | NFR-SC-04 |
| MBQ-7 | Given any queue action, then it records the staff member and the time, and needs the shipping permission on every call | NFR-SC-05, SEC-08 |
| MBQ-8 | Given a rejected booking (bad address), then it waits for staff and is not retried automatically (new) | §5.18 |

## Out of scope
Fixing the address (a customer service task through Orders).

## Open questions
See Q-SHP-2.
