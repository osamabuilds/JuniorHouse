# Fulfilment: tasks (Sprint 14)

Test-first. Each task names the test that proves it. Tick a task only when its test passes. Tests are tagged with the criterion IDs.

## Warehouse (allocation and pick decrement)
- [x] **W1** `IStockAllocator` contract (`Warehouse.Contracts`) and the FIFO query (oldest putaway first, per bin, fixed lock order). *Proof:* `StockAllocatorTests` (ALC-3, 4, 9, 10) against PostgreSQL.
- [x] **W2** Idempotent allocate and release by order key; short lines reported, not thrown (ALC-8). *Proof:* `StockAllocatorTests` (ALC-5, 6, 8).
- [x] **W3** `ConfirmPickAsync`: ledger decrement per bin once, reservation converted. *Proof:* `ConfirmPickTests` (PCK-7, 8, RSV-9).
- [x] **W4** `FlagBinAsync` and "under review" bins skipped by allocation. *Proof:* `StockAllocatorTests` (PEX-6).

## Orders contract
- [x] **O1** `IOrderFulfilmentQueries` (lines, totals, method, recipient name and city) in `Orders.Contracts`, implemented in Orders. *Proof:* `OrderFulfilmentQueriesTests`.

## Fulfilment module
- [x] **F1** Module skeleton, ADR 0030, `ModuleNames`, glossary words, `FULF` schema, domain model and lookups, migration. *Proof:* architecture and naming tests; `FulfilmentMigrationTests`.
- [x] **F2** `PackagingRule` and `WeightCheck` pure functions. *Proof:* `PackagingRuleTests`, `WeightCheckTests` (PAK-4, 6, 7, 8).
- [x] **F3** Create from `OrderConfirmedEvent` / `OrderPaidEvent` behind the inbox, allocate through the contract. *Proof:* `CreateFulfilmentOrderTests` (ALC-1, 2, 5, 8, 11).
- [x] **F4** Cancel: release before pick, put-back task after. *Proof:* `CancelFulfilmentOrderTests` (ALC-6, 7, PCK-12).
- [x] **F5** Queue and pick list queries (paged, walking order, constant queries). *Proof:* `PickListQueryTests` (PCK-1, 11, APP-8).
- [x] **F6** Claim and release the pick. *Proof:* `PickClaimTests` (PCK-9).
- [x] **F7** Scan bin and GTIN with refusals, over-pick guard, under 300 ms. *Proof:* `PickScanTests` (PCK-2 to 6, 10).
- [x] **F8** Complete pick: decrement once, event. *Proof:* `CompletePickTests` (PCK-7, 8).
- [x] **F9** Pick exceptions: raise, hold, resolve, delayed-line event, bin flag. *Proof:* `PickExceptionTests` (PEX-1 to 10).
- [x] **F10** Pack: open, scan, weight, complete, packed event, idempotent. *Proof:* `PackStationTests` (PAK-1 to 13).
- [x] **F11** Packing slip from Orders at print time, print through the printer port. *Proof:* `PackingSlipTests` (SLP-1 to 8).
- [x] **F12** Endpoints, permissions, authorization matrix rows. *Proof:* `FulfilmentEndpointsTests`, matrix test (APP-1, 2).
- [x] **F13** Notifications consumer for `OrderLineDelayedEvent` with a template. *Proof:* `DelayedLineNotificationTests` (PEX-3).
- [x] **F14** Scan log purge job. *Proof:* `ScanLogPurgeTests`.

## Admin app
- [ ] **A1** Pick queue and pick list screens with a focused scan box. *Proof:* component specs (APP-3, 4, 5, 8).
- [ ] **A2** Pack station and slip screens. *Proof:* component specs (PAK, SLP, APP-3).
- [ ] **A3** Exceptions screen. *Proof:* component specs (PEX-4).
- [ ] **A4** Web manifest and icons, `noindex` kept. *Proof:* spec (APP-7, APP-10).
- [ ] **A5** Playwright journey: confirmed order to packed parcel, with a scan refusal and an exception; axe. *Proof:* `e2e/fulfilment/pick-pack.e2e.spec.ts`.

## Tests beyond unit and integration (as for S12b)
- [x] **T1** Security tests: permission on every route, no cross-role access, hostile scan input, oversized bodies. *Proof:* `FulfilmentSecurityTests`.
- [x] **T2** Concurrency: two pickers, two orders for the last unit, repeated completion. *Proof:* `FulfilmentConcurrencyTests`.
- [x] **T3** Performance: scan latency p95 under 300 ms; 200 orders allocated at once. *Proof:* `FulfilmentPerformanceTests`.
- [ ] **T4** Pen test script for the fulfilment routes. *Proof:* `scripts/security/fulfilment-pentest.mjs`.

## Close-out
- [ ] **Z1** Architecture tests green; build with 0 warnings; unit, integration and e2e green; spec "As built" sections; README "Try Sprint 14"; docker rebuilt; memory note.
