# Stock ledger: tasks

Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md) · ADR 0012.

- [x] **L1** Domain: `StockPost` (validation: non-zero integer, known grade/kind), grade and kind codes. *Proof:* `StockPostTests`.
- [x] **L2** Tables `GRD_LKP`, `MVMT_KIND_LKP` (seeded), `VRNT_REF`, `STCK_BAL` (check ≥ 0, unique key), `STCK_MVMT` (indexes, immutability trigger), migration. *Proof:* `NamingConventionTests`; `LedgerSchemaTests` (negative balance rejected in SQL; UPDATE and DELETE on a movement raise, LED-1/NFR-SC-05).
- [x] **L3** `IStockPosting` implementation: conditional upsert/update SQL with every identifier quoted, before/after, one event per touched variant. *Proof:* `StockPostingTests` (LED-1 row fields incl. before/after, LED-3 refuse below zero, one event per variant per transaction).
- [x] **L4** Concurrency. *Proof:* `StockPostingConcurrencyTests` (50 parallel −1 postings against 30 units: exactly 30 succeed, balance 0; LED-3).
- [x] **L5** Consistency query. *Proof:* `LedgerConsistencyTests` (random mixed operations, `SUM(movement) == balance` for every key, LED-2).
- [x] **L6** `VariantEventMapper` + handlers for `VariantGtinAssignedEvent` / `GtinVoidedEvent`, inbox-guarded. *Proof:* `VariantEventMapperTests` (rejections), `RecordVariantTests` (assigned, voided, duplicate delivery, void before assign).
- [x] **L7** `IStockVariantReader` (by GTIN/id) and `IBinStockQuery` implementation. *Proof:* `StockVariantReaderTests`.
- [x] **L8** Read queries: stock per variant, bins of a variant, contents of a bin, movements. *Proof:* `StockQueriesTests` (LED-5 totals by grade, LED-6, LED-7, LED-9 order, LED-8 paging + constant statement count).
- [x] **L9** Events and latency: placeholder handler for `StockLevelChangedEvent`; end-to-end commit→handler timing. *Proof:* `StockEventLatencyTests` (LED-4, p95 ≤ 5 s over 50 runs on the test stack).
- [x] **L10** Endpoints (all GET, `Staff`; consistency `Admin`). *Proof:* architecture test; `StockEndpointsTests` (LED-10: no write endpoint, caller totals impossible).
- [x] **L11** Web `warehouse/stock-ledger`. *Proof:* unit + accessibility tests; `e2e/warehouse/stock-ledger.e2e.spec.ts`.
- [x] **L12** Card → Implemented; traceability rows LED-1..10.
