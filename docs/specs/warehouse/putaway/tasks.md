# Putaway: tasks

Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md).

- [x] **P1** `PutawayItem` domain: `Record`, `LinkVariant`, pending math, status transitions, grade from outcome. *Proof:* `PutawayItemTests` (PUT-1 mapping, PUT-5 quantity rules, PUT-9 never above QC quantity, PUT-20 grade).
- [x] **P2** Tables `PUT_ITEM`, `PUT_REC`, `PUT_STS_LKP` + migration. *Proof:* `NamingConventionTests`; `PutawaySchemaTests` (check constraint `PUT_QTY ≤ QC_QTY`, unique idempotency key, unique item key).
- [x] **P3** `QcBatchAcceptedMapper` + `RecordAcceptedBatchCommand` handler (inbox, style via `IPurchaseOrderQueries`). *Proof:* `QcBatchAcceptedMapperTests` (PUT-12 only released lines, malformed → rejected), `RecordAcceptedBatchTests` (PUT-1, PUT-2 duplicate delivery, both event orders with the variant event).
- [x] **P4** Link items when the variant reference arrives (and mark back to Awaiting GTIN on void, Q-PUT-2 default). *Proof:* `LinkVariantTests`.
- [x] **P5** `ScanLookupQuery` . *Proof:* `ScanLookupTests` (PUT-6 unknown GTIN, GTIN with no pending, PUT-7 retired); `ScanLatencyTests` (PUT-17, 1,000 lookups p95 < 300 ms).
- [x] **P6** `ConfirmPutawayCommand` (lock, validate, post, record, event). *Proof:* `ConfirmPutawayTests` (PUT-3, PUT-4 balance/ledger/record/event together, PUT-5, PUT-6 inactive bin, PUT-19 list selection without GTIN, PUT-21 over-quantity message, PUT-10 rollback leaves nothing).
- [x] **P7** Idempotency and concurrency. *Proof:* `ConfirmPutawayConcurrencyTests` (PUT-8 same key twice applies once; PUT-9 20 parallel confirms never exceed the QC quantity).
- [x] **P8** `StockPutawayConfirmedEvent` + placeholder handler . *Proof:* `PutawayEventTests` (PUT-10, PUT-11 within 5 s p95).
- [x] **P9** Queue and history queries. *Proof:* `PutawayQueriesTests` (PUT-14 paging, order, constant statement count).
- [x] **P10** Endpoints  (`Staff`, strict rate limit on confirm), problem-detail codes. *Proof:* `PutawayEndpointsTests`; architecture TC-PUT-05 (PUT-16).
- [x] **P11** Audit. *Proof:* `PutawayAuditTests` (PUT-15 actor/time/scanned flag, records immutable).
- [x] **P12** Web `warehouse/putaway` (focused scan input, bin input, list-selection mode, history). *Proof:* component tests for keyboard-wedge entry (Enter submits), list-selection mode, messages naming the failed check; `e2e/warehouse/putaway.e2e.spec.ts` including a slow-network run (PUT-18).
- [x] **P13** Card → Implemented; traceability rows PUT-1..21 and TC-PUT-01..05.
