# Cycle counts: tasks

Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md). Needs bins, stock-ledger and adjustments done.

- [x] **C1** `CycleCount` domain: states, variance computation, staleness rule, confirm guards. *Proof:* `CycleCountTests` (CC-4 reason required, CC-7 stale lines, CC-8 zero variance, CC-9 cancel).
- [x] **C2** Tables `CNT_MAIN`, `CNT_BIN` (open-bin partial unique), `CNT_LINE`, `CNT_NO_SEQ`, `CNT_STS_LKP` + migration. *Proof:* `NamingConventionTests`; `CountSchemaTests` (CC-3 second open count on a bin refused by the database).
- [x] **C3** `CreateCycleCountCommand` (+ movement marks, business number). *Proof:* `CreateCycleCountTests` (CC-1, CC-3, number sequence).
- [x] **C4** `SubmitCountLinesCommand` and the blind read. *Proof:* `SubmitCountLinesTests` (CC-6 found variant, CC-10 invalid/retired); `BlindCountApiTests` (CC-2: system quantity absent from every response before review).
- [x] **C5** `ReviewCountQuery` + `ConfirmCycleCountCommand` (one adjustment per variance via `IAdjustmentPoster`, bin last-counted, close). *Proof:* `ConfirmCycleCountTests` (CC-5, CC-8, CC-12, large variance carries reconfirm, idempotent confirm).
- [x] **C6** Staleness end to end. *Proof:* `StaleCountTests` (a putaway after open flags the line and blocks confirm until recounted, CC-7).
- [x] **C7** Cancel and listing queries (counts, lines, bin age). *Proof:* `CountQueriesTests` (CC-9, CC-11 paging and constant statements).
- [x] **C8** Endpoints (`Inventory` for create/review/confirm/cancel). *Proof:* architecture test; `CycleCountEndpointsTests`.
- [x] **C9** Web `warehouse/cycle-counts`. *Proof:* component tests (system quantity never rendered before review); `e2e/warehouse/cycle-counts.e2e.spec.ts`.
- [x] **C10** Card → Implemented; traceability rows CC-1..12.
