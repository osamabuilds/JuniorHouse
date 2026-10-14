# Adjustments: tasks

Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md).

- [x] **A1** `AdjustmentPolicy` (reason × direction × magnitude). *Proof:* `AdjustmentPolicyTests` (ADJ-1, ADJ-3 matrix, ADJ-4b large needs reconfirm incl. `FOUND_STOCK`).
- [x] **A2** Tables `ADJ_MAIN`, `ADJ_RSN_LKP` (4 seeded rows) + migration. *Proof:* `NamingConventionTests`; `AdjustmentSchemaTests` (ADJ-2 seeded codes, non-zero check, unique key).
- [x] **A3** `PostAdjustmentCommand` + validator + `IAdjustmentPoster`. *Proof:* `PostAdjustmentTests` (ADJ-0 −3 with before/after, ADJ-4 below zero refused, ADJ-5 record+movement+event in one transaction, ADJ-7 invalid inputs, ADJ-9 retired GTIN rules).
- [x] **A4** Idempotency and concurrency. *Proof:* `AdjustmentConcurrencyTests` (ADJ-8 resend; ADJ-4a 30 mixed adjustments and putaways on one bin lose no update).
- [x] **A5** History and reasons queries. *Proof:* `AdjustmentQueriesTests` (ADJ-10 filters, paging, constant statements).
- [x] **A6** Endpoints: write in `Inventory` group with strict rate limit; no update/delete route (ADJ-6). *Proof:* architecture test; `AdjustmentEndpointsTests` (problem codes, no PUT/DELETE mapped, ADJ-4c).
- [x] **A7** Web `warehouse/adjustments` with the re-confirm step. *Proof:* component tests (confirm step appears above the limit; reason required); `e2e/warehouse/adjustments.e2e.spec.ts`.
- [x] **A8** Card → Implemented; traceability rows ADJ-0..10, TC-STK-01, 04, 05, 06, 07, 09.
