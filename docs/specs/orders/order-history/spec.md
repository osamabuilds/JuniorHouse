# Order history and tracking

- **Status:** Draft, applied by delegation (owner away, 2026-11-09)
- **Jira:** to be created
- **BRD sections:** §5.19
- **Requirement IDs:** FR-36, FR-37, SEC-06, SEC-07, OLK-8
- **Built in:** orders, order-lookup (this amends them; the courier data comes from Shipping through its contract)

## Problem & intent
A customer asks "where is my parcel". My Orders and the guest look-up already show the order status; they must now show the courier, the waybill and the timeline, and a parcel must be findable by tracking number alone.

## Requirements (quoted from the BRD)
> **FR-36:** Registered users shall view real-time order status and full order history in their account.

> **FR-37:** Guest users shall track orders via an emailed link or a public tracking page using order number plus a verification value, with generic (non-leaking) error messaging on mismatch.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| OHS-1 | Given a booked order, when its owner opens it in My Orders, then it shows the courier name, the waybill and the status timeline with times | FR-36 |
| OHS-2 | Given the guest look-up (order number and mobile, or the signed link), when it matches, then the same tracking block shows | FR-37 |
| OHS-3 | Given an order with no shipment yet, then the tracking block says the parcel is being prepared (new) | FR-36 |
| OHS-4 | Given a shipment status change, then the order status moves to Dispatched or Delivered and the history records it with the actor "courier" | §5.19 |
| OHS-5 | Given the tracking block, then it shows no other customer's data and no staff names; only courier, waybill, statuses and times (new) | SEC-05, SEC-25 |
| OHS-6 | Given an order that is not the caller's, then My Orders still answers not found, with no hint that the order exists | SEC-05 |
| OHS-7 | Given the dispatch notification, then the email carries the signed track link (new) | §5.19 |

## Out of scope
Live map, rider details.
