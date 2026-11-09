# 0031: Shipping module, the courier port and tracking

- Status: Accepted by delegation (the owner was away and asked for the next sprint to continue; to be confirmed), 2026-11-09
- Date: 2026-11-09

## Context
Sprint 15 books packed parcels with couriers, prints the label, hands parcels over on a manifest, and tracks them. Couriers differ and their accounts do not exist yet (docs/roadmap/third-party-integrations.md section 6). Order status belongs to Orders (ADR 0024); stock and parcels to their modules (ADR 0002).

## Decision
1. **A new module `Shipping`** (schema `SHPG`) owns shipments, their status history and manifests. It starts on `OrderPackedEvent`, reads Orders through a new contract `IOrderShippingQueries` at the moment of need, and exposes `IShipmentTrackingQueries` for the customer views.
2. **One port, `ICourierGateway`,** per courier code, found by a registry from configuration. The fake courier is the only adapter built; it is registered only in Development and Testing, like the pretend payment gateway (ADR 0027). A real courier is a new class behind the same port.
3. **Booking is a background step,** idempotent on the order, primary then secondary on "unreachable", manual queue on "rejected" or when attempts run out. A waybill is set once.
4. **A status push is trusted only by signature,** checked over the raw body before it is parsed, answered generically, idempotent on the courier's event id, and never moves a shipment back. Polling uses the same command.
5. **Orders moves its own status** from `ShipmentStatusChangedEvent` (Dispatched on pick-up, Delivered on delivery); Shipping never writes order tables.
6. **Permissions:** `shipping.view` and `shipping.ops` are new codes. The existing `shipping.manage` (delivery fee rules from S11) is unrelated and keeps its meaning.
7. **Public tracking by waybill** shows statuses and times only; it shares the look-up rate limit and CAPTCHA rules (SEC-06, SEC-07).

## Alternatives considered
- Orders owning shipments: rejected (mixes courier mechanics into Orders).
- Calling couriers inside the pack request: rejected (a slow courier would slow the packer, and a failure would block packing).
- Trusting the courier's status ordering: rejected (pushes arrive late and out of order).

## Consequences
A second schema, a migration, a hosted worker set, and new permissions. Adding a real courier later is a new gateway class and configuration; adding a rule-based choice is a change in one place (the courier selector).
