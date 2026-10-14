# Warehouse: groundwork tasks (do first, in order)

Module design: [plan.md](plan.md). Feature task lists: [bins](bins/tasks.md) · [stock-ledger](stock-ledger/tasks.md) · [putaway](putaway/tasks.md) · [adjustments](adjustments/tasks.md) · [cycle-counts](cycle-counts/tasks.md) · [stock-alerts](stock-alerts/tasks.md). Run order: groundwork → bins → stock-ledger → putaway → adjustments → cycle-counts → stock-alerts → web screens per feature → e2e.

Tests run in the Linux SDK container (Smart App Control blocks local DLLs); say so when reporting.

- [x] **G1** Accept ADR 0012 (status line) once the plans are approved. *Proof:* n/a (doc).
- [x] **G2** Add the glossary words from the module plan to `docs/db/naming.md` ("Added in Sprint 6"). *Proof:* `NamingConventionTests` (existing) passes once tables exist.
- [x] **G3** Create projects `Romp.Modules.Warehouse.{Domain,Application,Contracts,Infrastructure}` and `Romp.Modules.Warehouse.Tests`, add to `Romp.slnx`, `Program.cs` (modules array, MediatR assembly), `ModuleNames` in `ModuleBoundaryTests`. *Proof:* `ModuleBoundaryTests` and `dotnet build Romp.slnx` pass with warnings as errors.
- [x] **G4** `WarehouseModule : IModule` with `WhseDbContext` (schema `WHSE`, `MIG_HIST`), `WarehouseTransactionBehavior`, outbox registration, placeholder handler, `WarehouseOptions` + validator (`LargeAdjustmentUnits` ≥ 1, `DefaultLowStockThreshold` ≥ 0). *Proof:* `WarehouseOptionsValidatorTests` (valid, zero/negative limit, negative threshold).
- [x] **G5** Add `Policies.Inventory` to `Romp.BuildingBlocks.Web` and register it like `Staff` (authenticated now; role in S8). *Proof:* `PoliciesTests` (policy exists, dev handler satisfies it in Testing).
- [x] **G6** Warehouse events in `Warehouse.Contracts` (`StockPutawayConfirmedEvent`, `StockLevelChangedEvent`, `StockAlertRaisedEvent`) with the `LabellingEvent`-style envelope. *Proof:* `WarehouseEventContractTests` (schema version 1, aggregate type/id, serialises without staff names).
- [x] **G7** `WarehouseFeatureDependencyTests` (one-way feature dependencies, Domain purity, no references to `*.Infrastructure` of other modules), and TC-PUT-05 (no module other than Warehouse references `WHSE`). *Proof:* the tests themselves, red until features exist, then green.
- [x] **G8** Initial migration scaffold (empty schema + `INBX` + `OUTB_MSG` + `MIG_HIST`) generated in the SDK container. *Proof:* `WarehouseMigrationTests` applies it to a Testcontainers PostgreSQL.
- [x] **G9** Spec cards flip from Planned to Implemented as each feature lands; traceability file `traceability.md` is filled as tests are tagged. *Proof:* `StructureSyncTests`.
