# Shipping

Ship and track (Sprint 15): a packed parcel is booked with a courier, gets its waybill and label, goes out on a reconciled end-of-day manifest, and is tracked to delivery. The customer sees the timeline; a failed attempt is surfaced proactively.

**Status:** Specs written 2026-11-09 and **applied by delegation** (the owner is away and asked for S15 to be built): the open decisions use my recommendations as settings, to be confirmed. · **Schema:** `SHPG` · **Code:** `src/api/src/Modules/Shipping/` · **Web:** a Shipping section of the admin app · **Jira:** none created · **Rules:** [constitution](../../CONSTITUTION.md), [CONVENTIONS](../../CONVENTIONS.md), [CLAUDE.md](../../../CLAUDE.md)

## Features

| Feature | What it does | Requirements | Status |
|---|---|---|---|
| [courier-port](courier-port/spec.md) | `ICourierGateway`: book, status, parse a push; a fake courier with outages on demand | FR-SC-13, FR-SC-14, NFR-SC-04 | Draft |
| [bookings](bookings/spec.md) | Book on `OrderPackedEvent`, primary then secondary, waybill attached | FR-SC-13, FR-SC-14, §5.18 | Draft |
| [labels](labels/spec.md) | The combined shipping label with the waybill barcode | §5.17 | Draft |
| [manual-booking-queue](manual-booking-queue/spec.md) | Failed bookings wait, retry, or take a hand-entered waybill | §5.17, NFR-SC-04 | Draft |
| [manifests](manifests/spec.md) | End-of-day manifest per courier, reconciled with the parcel count | FR-SC-15, §5.18 | Draft |
| [tracking](tracking/spec.md) | Status pushes and polling, timeline, failed-attempt warning, public tracking number look-up | FR-SC-16, §5.19, SEC-06, SEC-07 | Draft |
| [orders/order-history](../orders/order-history/spec.md) | Courier timeline in My Orders and the guest look-up | FR-36, FR-37, OLK-8 | Draft |

## Module decisions

| ID | Decision | Why |
|---|---|---|
| D-1 | **New module `Shipping` (schema `SHPG`).** It reads Orders only through `Orders.Contracts` and talks to couriers only through `ICourierGateway`. | Constitution: new module, ADR, glossary, architecture-test entry. |
| D-2 | **Booking starts on `OrderPackedEvent`** (S14), never before: the weight is known. | BRD §5.17 sequencing: pack, weigh, book, label. |
| D-3 | **A fixed default courier at MVP** (primary), a secondary for fallback; rule-based choice later. | BRD §5.18. |
| D-4 | **Couriers are configured, not coded:** each has a code, a name, a webhook secret and an enabled flag in settings. Real courier adapters wait for the owner's accounts; the fake courier stands in. | third-party-integrations.md section 6. |
| D-5 | **A status push is verified by signature, idempotent, and never moves a shipment backwards.** The answer to a bad push is generic. | SEC-10 pattern; NFR-09. |
| D-6 | **Orders owns the order status; Shipping owns the shipment status.** Shipping publishes `ShipmentStatusChangedEvent`; Orders moves to Dispatched or Delivered from it. | Module rule; one owner per truth. |

## Open questions (recommendations applied, owner to confirm)

| ID | Question | Applied recommendation |
|---|---|---|
| Q-SHP-1 | Which primary and secondary courier? | Two fake couriers `PRIMARY` and `SECONDARY` until accounts exist; the port is the same. |
| Q-SHP-2 | Which numbers retry a failed booking, and for how long? | Retry every 5 minutes, up to 12 attempts (an hour), then the order stays in the manual queue for staff (`Shipping:RetryMinutes`, `MaxAttempts`). |
| Q-SHP-3 | Courier's own label PDF or ours? | Ours: an HTML label with a Code 128 barcode of the waybill; a courier PDF can be stored later. |
| Q-SHP-4 | Polling fallback interval for a courier without webhooks? | Every 30 minutes for shipments in transit (`Shipping:PollMinutes`). |
| Q-SHP-5 | COD cash reconciliation (FR-SC-17)? | Out of S15 (it needs the courier's remittance data); the shipment keeps the COD amount and the delivered time so S16 can build it. |

## Out of scope

Real courier adapters (need accounts), COD remittance reconciliation (FR-SC-17), return pickups (S17), rule-based courier choice, multi-parcel orders, courier rate cards.
