# Reverse QC: tasks (Sprint 17)

Test-first. Each task names the test that proves it. Tick a task only when its test passes.

## Warehouse
- [x] **W1** `IStockReturns` in `Warehouse.Contracts`, implemented over `IAdjustmentPoster` (found-stock reason, deterministic key, grade). *Proof:* `StockReturnsTests` (RQC-4, 9, 10).

## Returns module
- [ ] **R1** Module skeleton, ADR 0033, `ModuleNames`, glossary, `RTRN` schema, lookups, migration. *Proof:* architecture and naming tests.
- [ ] **R2** `ReturnCase` rules. *Proof:* `ReturnCaseDomainTests` (RQC-2, 3, 5 to 9).
- [ ] **R3** Open from `ShipmentStatusChangedEvent` behind the inbox. *Proof:* `OpenReturnCaseTests` (RQC-1, 13).
- [ ] **R4** Receive (intact restocks all and completes; not intact waits). *Proof:* `ReceiveReturnCaseTests` (RQC-2, 3, 9, 10).
- [ ] **R5** Decide a line. *Proof:* `DecideReturnLineTests` (RQC-4 to 6, 9, 10).
- [ ] **R6** Complete and publish `ReturnQcCompletedEvent`. *Proof:* `CompleteReturnCaseTests` (RQC-7, 8).
- [ ] **R7** Queries (paged, status filter, query count). *Proof:* `ReturnCaseQueryTests` (RQC-11).
- [ ] **R8** Endpoints, permissions, matrix rows. *Proof:* `ReturnsEndpointsTests`, matrix tests (RQC-12).

## Admin app
- [ ] **A1** Cases list and case page (receive, decide, complete). *Proof:* store and component specs (RQC-2 to 8, 11).
- [ ] **A2** Playwright journey: an RTO parcel is received and inspected. *Proof:* `e2e/journeys/returns-reverse-qc.e2e.spec.ts`.

## Close-out
- [ ] **Z1** Architecture tests green; build with 0 warnings; tests green; spec "As built"; README "Try Sprint 17"; docker rebuilt; memory note.
