# Size charts: tasks

- [ ] **C1** `SizeChart` and `SizeChartLine` rules (min ≤ max, no negatives, one line per size, at least an age range or one measurement). *Proof:* `SizeChartTests` (CHT-4, CHT-5).
- [ ] **C2** Chart lookup rule (category + gender, else category + Unisex, else none). *Proof:* `ChartLookupRuleTests` (CHT-1, CHT-3).
- [ ] **C3** `CHRT_MAIN`, `CHRT_LINE` configuration with check constraints, cascade delete and unique keys. *Proof:* `ChartSchemaTests` (database refuses min > max and duplicates).
- [ ] **C4** `CreateSizeChart`, `UpdateSizeChart` (token), `DeleteSizeChart`, with `SizeChartChangedEvent` in the same transaction. *Proof:* `SizeChartCommandTests` (CHT-2, CHT-4, CHT-5; event written; concurrent edit refused).
- [ ] **C5** `ListSizeCharts` (paged), `GetSizeChart`, `GetChartForProduct` (rows for the product's size run in size order, one query). *Proof:* `SizeChartQueryTests` (CHT-1, CHT-6, CHT-8: constant number of queries).
- [ ] **C6** `ChartGate` (never blocks; reports `NoChart`). *Proof:* `ChartGateTests` (CHT-3).
- [ ] **C7** Endpoints behind `Policies.Catalogue`. *Proof:* `SizeChartEndpointsTests` over HTTP; `EndpointSecurityTests`.
- [ ] **C8** Size rows beyond 7-8Y in `SIZE_LKP` once the labels are confirmed (Q-CHT-1). *Proof:* `ReferenceLookupTests` (new rows in order). Blocked until the owner gives the labels.
- [ ] **C9** Web `catalog/size-charts`: paged list, editor table with one row per size, labelled inputs, inline errors; the product editor's Size chart tab. *Proof:* component and reducer specs (CHT-7: `th scope`, labels, keyboard).
- [ ] **C10** e2e: create a chart, see it on a product, edit a value. *Proof:* `size-charts.e2e.spec.ts`; screens in the layout suite.
- [ ] **C11** Card → Implemented; traceability rows CHT-1..8, FR-08, TC-PDP-01. *Proof:* `StructureSyncTests`.
