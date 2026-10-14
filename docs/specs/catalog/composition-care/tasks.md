# Composition and care: tasks

- [ ] **K1** `Composition` value object (no duplicate fibre, 0 < p ≤ 100, one decimal, sum compared in integer tenths). *Proof:* `CompositionTests` (CMP-1, CMP-6, CMP-7: 33.3 + 33.3 + 33.4 = 100; 99.9 incomplete; 100.01 refused).
- [ ] **K2** `CareSet` (ids plus note ≤ 500 characters). *Proof:* `CareSetTests` (CMP-2).
- [ ] **K3** `CompositionGate` (`CompositionMissing`, `CompositionTotal(actual)`, `CareMissing`). *Proof:* `CompositionGateTests` (CMP-1, CMP-2).
- [ ] **K4** `PRDT_CMPS`, `PRDT_CARE_MAP` configuration and constraints. *Proof:* `CompositionSchemaTests` (unique pairs, percentage check constraint).
- [ ] **K5** `SetComposition`, `SetCare` (replace-all in one transaction with the product token) and `ProductChangedEvent` for a published product. *Proof:* `CompositionCommandTests` (CMP-1, CMP-5, concurrent edit refused, fibre and care ids validated through Reference).
- [ ] **K6** `GetCompositionAndCare` and the lookup lists. *Proof:* `CompositionQueryTests` (CMP-3, CMP-4: every care item has a text name; CMP-8: new lookup values appear with no code change).
- [ ] **K7** Endpoints behind `Policies.Catalogue`. *Proof:* `CompositionEndpointsTests` over HTTP; `EndpointSecurityTests`.
- [ ] **K8** Seed the fibre and care lists once the owner confirms them (Q-CMP-3, Q-CMP-4); temporary dev seed meanwhile. *Proof:* `ReferenceLookupTests`.
- [ ] **K9** Web Composition and care tab: rows table with fibre select and percent input, live total announced with `aria-live`, care checkbox group with names, note field. *Proof:* component and reducer specs (CMP-9: labels, announced total, error text).
- [ ] **K10** e2e: enter 95% + 5%, see it publish; enter 95% and see the gate message. *Proof:* `composition-care.e2e.spec.ts`; screen in the layout suite.
- [ ] **K11** Card → Implemented; traceability rows CMP-1..9, FR-09. *Proof:* `StructureSyncTests`.
