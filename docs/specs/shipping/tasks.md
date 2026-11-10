# Shipping: tasks (Sprint 15)

Test-first. Each task names the test that proves it. Tick a task only when its test passes.

## Orders and contracts
- [x] **O1** `IOrderShippingQueries` in `Orders.Contracts`, implemented in Orders. *Proof:* `OrderShippingQueriesTests`.
- [x] **O2** Orders consumes `ShipmentStatusChangedEvent` (Dispatched, Delivered) behind the inbox, history actor "courier". *Proof:* `ShipmentStatusConsumerTests` (OHS-4, TRK-5).
- [x] **O3** Tracking block in My Orders and the guest look-up through `IShipmentTrackingQueries`. *Proof:* `OrderTrackingViewTests` (OHS-1, 2, 3, 5, 6).

## Shipping module
- [x] **S1** Module skeleton, ADR 0031, `ModuleNames`, glossary, `SHPG` schema, lookups, migration, permissions `shipping.view` and `shipping.ops` with the Identity migration. *Proof:* architecture and naming tests; `ShippingMigrationTests`.
- [x] **S2** `Shipment` aggregate rules: status ranks, waybill once, manual queue states. *Proof:* `ShipmentTests` (BKG-2, TRK-4, MBQ-8).
- [x] **S3** Courier port, registry, options and the fake courier. *Proof:* `FakeCourierTests` (CPT-1 to 7).
- [x] **S4** Create from `OrderPackedEvent` and book: primary, secondary, rejected, idempotent. *Proof:* `BookingTests` (BKG-1 to 9, 11).
- [x] **S5** Retry command and manual queue (list, try now, manual waybill). *Proof:* `ManualQueueTests` (MBQ-1 to 8).
- [x] **S6** Label query with the Code 128 barcode. *Proof:* `LabelTests`, `Code128Tests` (LBL-1 to 7).
- [x] **S7** Manifests: generate, count, hand over, close. *Proof:* `ManifestTests` (MNF-1 to 10).
- [x] **S8** Tracking: apply a push, poll, events, failed-attempt flag. *Proof:* `TrackingPushTests`, `PollingTests` (TRK-1 to 8, 11 to 13).
- [x] **S9** Public track by waybill, generic and limited. *Proof:* `PublicTrackingTests` (TRK-9, 10).
- [x] **S10** Cancel from `OrderCancelledEvent`. *Proof:* `CancelShipmentTests` (BKG-10).
- [x] **S11** Endpoints, permissions, matrix rows, anonymous list. *Proof:* `ShippingEndpointsTests`, matrix tests.
- [x] **S12** Workers (retry, polling, manifest) and registration. *Proof:* worker commands tested in S5, S7, S8.
- [x] **N1** Notifications: shipped and delivery-attempt-failed kinds and consumers. *Proof:* `ShipmentNoticeTests` (TRK-7, OHS-7).

## Admin app
- [x] **A1** Shipments list and detail with the timeline. *Proof:* component and reducer specs (TRK-13).
- [x] **A2** Manual booking queue. *Proof:* specs (MBQ-1, 3, 4, 6).
- [x] **A3** Label page. *Proof:* spec (LBL-1, 3, 4, 6).
- [x] **A4** Manifests list, detail, count. *Proof:* specs (MNF-5, 6, 8).
- [x] **A5** Customer tracking block on the storefront order screens and the track-by-number page. *Proof:* storefront specs (OHS-1, 3, TRK-9).
- [ ] **A6** Playwright journey: packed order to delivered, with an outage fallback. *Proof:* `e2e/journeys/ship-track.e2e.spec.ts`.

## Tests beyond unit and integration
- [x] **T1** Security and concurrency: webhook forgery and replay, concurrent pushes, double booking. *Proof:* `ShippingSecurityTests`, `ShippingConcurrencyTests`.
- [ ] **T2** Pen test script. *Proof:* `scripts/security/shipping-pentest.mjs`. *Deferred by the owner (2026-11-10): the pen test and vulnerability tests run once on the whole system.*

## Close-out
- [ ] **Z1** Architecture tests green; build with 0 warnings; tests green; spec "As built"; README "Try Sprint 15"; docker rebuilt; memory note.

---

# COD reconciliation (Sprint 16)

Test-first. Specs in [cod-reconciliation](cod-reconciliation/spec.md).

## Backend
- [x] **C1** `CodJudge` pure outcomes. *Proof:* `CodJudgeTests` (COD-2 to 7).
- [x] **C2** `Shipment` COD state: NotCod / Awaiting at creation, settle, short, over, resolve, overdue flag and clear. *Proof:* `ShipmentCodTests` (COD-1, 11, 12).
- [x] **C3** `CodRemittance` aggregate rules (total equals lines, limits, resolve once). *Proof:* `CodRemittanceDomainTests` (COD-8, 10, 12, 16).
- [x] **C4** Persistence, lookups, migration with back-fill, `ModuleNames` unchanged, glossary words. *Proof:* `ShippingMigrationTests`, naming and architecture tests.
- [x] **C5** `RecordCodRemittanceCommand`. *Proof:* `CodRecordTests` (COD-2 to 9).
- [x] **C6** `ResolveCodLineCommand`. *Proof:* `CodResolveTests` (COD-12).
- [x] **C7** `FlagOverdueCodCommand` and the worker. *Proof:* `CodOverdueTests` (COD-11).
- [x] **C8** Queries: summary, awaiting, exceptions, statements, detail, query counts. *Proof:* `CodQueryTests` (COD-13, 14).
- [x] **C9** Endpoints, permissions, anonymous refused. *Proof:* `ShippingEndpointsTests` additions (COD-15, 16).

## Admin app
- [x] **A1** Models, statement parser, API service, store. *Proof:* `cod.store.spec.ts`, `statement-text.spec.ts`.
- [x] **A2** Pages, route `/dispatch/cod`, nav. *Proof:* `cod-page.container.spec.ts` (COD-13).

## Close-out
- [ ] **Z1** Playwright journeys (ship-track and COD); build 0 warnings; tests green; spec "As built"; README "Try Sprint 16"; docker rebuilt; memory note.
