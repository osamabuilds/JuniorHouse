# Bins: tasks

Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md). Test-first: write the named test red, then make it green.

- [x] **B1** `Bin` domain: code pattern, derived `BinCode`, deactivate rule. *Proof:* `BinTests` (BIN-1, BIN-3 pattern table, BIN-4 rule).
- [x] **B2** Lookup-free table `BIN_MAIN` configuration (explicit `ToTable`/`HasColumnName`), unique code, zone index, migration. *Proof:* `NamingConventionTests`; `BinSchemaTests` (BIN-2 duplicate refused by the database).
- [x] **B3** `CreateBinCommand` + validator. *Proof:* integration `CreateBinTests` (BIN-1, BIN-2 race of 10 parallel creates leaves one, BIN-3, BIN-7 actor/time).
- [x] **B4** `SetBinActiveCommand` + `IBinStockQuery` port (fake in unit tests). *Proof:* `SetBinActiveTests` (BIN-4 refused with stock, allowed when empty, no-op when unchanged).
- [x] **B5** `IBinReader` (active-by-code, by-ids) and `IBinCountMarker` ports. *Proof:* `BinReaderTests` (BIN-5 inactive bin not returned as active).
- [x] **B6** `SearchBinsQuery` (paged, zone filter, on-hand via port in one grouped call). *Proof:* `SearchBinsTests` (BIN-6 order, clamp 1..100, statement count constant for 5 vs 50 rows).
- [x] **B7** Endpoints in `MapSecuredGroup(Staff)`, rate limit on create. *Proof:* architecture test (every endpoint in a secured group); `BinEndpointsTests` (problem details, 404).
- [x] **B8** Web `warehouse/bins`: NgRx store, container, table, form. *Proof:* unit tests for reducer/effects/component; accessibility asserts (labels, `th scope`); Playwright `e2e/warehouse/bins.e2e.spec.ts`.
- [x] **B9** Card → Implemented; traceability rows for BIN-1..7.
