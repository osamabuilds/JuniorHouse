# 0008 — Production ↔ Vendor coordination, and file storage as a building block

- Status: Accepted (proposed with SCRUM-183 plan, approved with the spec)
- Date: 2026-09-29

## Context

Sprint 3 adds the Production Tracking module (PROD). Two things it needs cross the module boundary that ADR 0002 draws:

1. **The PO's status and quantities are owned by VNDR, but production and delivery facts are owned by PROD.** PROD decides "bulk cutting started" and "delivered / partially delivered"; the PO must show In Production, Partially Delivered, Delivered. In the other direction, VNDR must refuse an amendment that drops a line below what has already been delivered.
2. **PROD needs the attachment machinery Sprint 2 built** (generated storage keys, content-signature check, safe download headers) for PP-sample photos. That code lives inside the Vendor module (`IFileStorage`, `LocalFileStorage`, `PoFileContent`).

## Decision

1. **Status changes flow by events, guards flow by contracts.**
   - PROD publishes `ProductionStarted` and `DeliveryNoteRecorded` (carrying the outcome PROD computed: partially delivered / delivered) through its outbox. VNDR consumes them through its own inbox and applies the matching forward-only PO transition, which VNDR itself validates and raises its own events for (`PoProductionStarted`, `PoPartiallyDelivered`, `PoDelivered`, `PoClosed`).
   - VNDR owns Close (manual) and Cancel. PROD consumes `PoAcknowledged` (opens the run), `PoRevisionPutInForce` (refreshes its own copy of the latest acceptable date, so the run list can flag "at risk" in one SQL query), `PoCancelled` and `PoClosed`.
   - Anything that must be **consistent at the moment of the decision** (the amendment guard: delivered quantities, "PP sample approved" for the costly flag) is a **read-only query** on a new `Romp.Modules.Production.Contracts`. The reverse guard PROD needs (terms at a dispatch date, PO send date) is a read-only query on `Vendor.Contracts`. Contracts projects never reference each other, so there is no build cycle.
2. **File storage becomes a building block.** `IFileStorage`, `LocalFileStorage`, its options and the content-signature detection move to `Romp.BuildingBlocks.Storage`. Each module keeps its own policy (allowed categories, limits, table). Vendor's public behaviour and configuration keys do not change.

## Consequences

- PO status can lag PROD's decision by the dispatcher's delay (seconds); this is the same eventual consistency the rest of the design accepts (ADR 0004). Money- or quantity-sensitive decisions use the contract, not the event.
- Each transition is a domain event, as §5.9 requires.
- A second real consumer of the shared outbox dispatcher exercises the retry and dead-letter path.
- Two modules now query each other's contracts: acceptable under ADR 0002 (contracts are the sanctioned path) but a signal to keep contracts small and stable.
- Moving storage code is a mechanical refactor covered by Sprint 2's existing file tests.

## Alternatives considered

- **PROD writes the PO status directly (shared table or synchronous command):** breaks ADR 0002 and makes the transition non-atomic across schemas.
- **VNDR keeps its own delivered-quantity counter fed by events for the amendment guard:** cheaper at read time but the guard would be eventually consistent, so an amendment could slip through in the window after a delivery note.
- **Duplicate the storage code in PROD:** rejected by decision D7 (no parallel system).
