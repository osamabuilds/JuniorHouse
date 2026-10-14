# Stock alerts: tasks

Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md). Needs stock-ledger done.

- [x] **S1** `AlertPolicy.Evaluate` pure function. *Proof:* `AlertPolicyTests` (ALR-1 crossing, ALR-2 to zero, ALR-3 restock closes, ALR-4 never stocked, threshold 0).
- [x] **S2** Tables `ALRT_THRS`, `ALRT_MAIN` (open-alert partial unique), `ALRT_LVL_LKP`, `ALRT_STS_LKP` + migration. *Proof:* `NamingConventionTests`; `AlertSchemaTests` (one open alert per variant).
- [x] **S3** `StockLevelChangedHandler` (outbox handler, inbox-guarded, re-reads available from the ledger) + `StockAlertRaisedEvent` + placeholder. *Proof:* `StockLevelChangedHandlerTests` (TC-STK-02 one alert, TC-STK-03 escalation with available 0, TC-STK-08 restock closes, replay creates nothing, ALR-7 event in outbox).
- [x] **S4** Failure isolation. *Proof:* `AlertFailureIsolationTests` (ALR-9: a throwing handler is retried and the stock change stays committed).
- [x] **S5** `SetThresholdCommand` (re-evaluates) and `AcknowledgeAlertCommand`. *Proof:* `ThresholdTests` (ALR-6 range validation, audit, re-evaluation), `AcknowledgeAlertTests` (ALR-5).
- [x] **S6** Queries: alerts (state filter), open count, thresholds with effective value. *Proof:* `AlertQueriesTests` (ALR-8 paging, constant statements, badge count).
- [x] **S7** Endpoints (`Inventory` for writes). *Proof:* architecture test; `AlertEndpointsTests`.
- [x] **S8** Web `warehouse/stock-alerts` with navigation badge. *Proof:* component tests (badge has accessible text, thresholds table labelled inputs); `e2e/warehouse/stock-alerts.e2e.spec.ts`.
- [x] **S9** Card → Implemented; traceability rows ALR-1..9, TC-STK-02, 03, 08.
