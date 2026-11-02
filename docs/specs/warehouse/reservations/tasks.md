# Stock reservations (Sprint 12): tasks

Ordered, checkable steps; test-first; .NET in the Linux SDK container. Plan: [plan.md](plan.md) (approved 2026-11-02) · Spec: [spec.md](spec.md) · ADR 0025. Built before the Orders placement tasks that call it.

- [x] **R1** `StockReservation` aggregate and `ReservationRequest/Result` (state machine, repeat is a no-op). *Proof:* `StockReservationTests` (RSV-4, 5).
- [x] **R2** Migration: `STCK_RSRV`, `STCK_RSRV_LINE`, `STCK_RSRV_BAL`, `RSRV_STS_LKP` and mappings. *Proof:* `ReservationMigrationTests`, naming and glossary tests.
- [x] **R3** Contract `IStockReservations` and `ReserveStockCommand` with the single conditional statement, lines in ascending order, all-or-nothing, short list on refusal, one stock event per variant. *Proof:* `ReserveStockTests` (RSV-1, 2, 7).
- [x] **R4** Concurrency: many parallel reservations for one unit against PostgreSQL, exactly one succeeds; two orders locking the same variants in opposite order never deadlock. *Proof:* `ReservationConcurrencyTests` (RSV-3, NFR-08).
- [~] **R5** `ReleaseReservationCommand` (done, tested in `ReserveStockTests`); the `OrderCancelledEvent` handler waits for Orders. *Proof:* `ReserveStockTests` (RSV-5).
- [ ] **R6** `ConfirmReservationHandler` on `OrderPlacedEvent`. *Proof:* `ConfirmReservationTests`.
- [ ] **R7** `SweepHeldReservationsJob` (past `HeldUntil`, waits while the outbox backlog is older than the hold, late confirm re-reserves or alerts). *Proof:* `SweepHeldReservationsTests` incl. the late-event case.
- [ ] **R8** Availability follows reservations: `IStockAvailabilityQueries` (single, list, `GetManyAsync`) and the storefront stock events subtract reserved. *Proof:* `StockAvailabilityReservedTests`, read-model version test (RSV-7).
- [ ] **R9** Guard: adjustments and cycle counts cannot take on-hand below reserved (refused or marked for review). *Proof:* `AdjustmentBelowReservedTests`, `CycleCountBelowReservedTests` (RSV-8).
- [ ] **R10** Hook `ConvertReservationCommand` defined with a test double; built in S14. *Proof:* `ConvertReservationContractTests` (RSV-9).
- [ ] **R11** Admin: reserved and available figures and a per-variant reservation list (read-only). *Proof:* `ReservationAdminEndpointsTests`, component specs.
- [ ] **R12** Close the loop: spec "As built", docker rebuild, owner told what to try (place an order in S12 and watch Available drop). *Proof:* e2e in the orders tasks.
