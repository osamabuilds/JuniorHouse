# Warehouse

Shelve (BRD §5.13-5.14): bins, putaway by scan, the stock ledger, reason-coded adjustments, cycle counts and low-stock alerts. Stock becomes available only after putaway.

**Schema:** `WHSE` · **Code:** `src/api/src/Modules/Warehouse/` (planned) · **Web:** `features/warehouse/` (planned) · **Jira:** epic SCRUM-17 (WMS) with SCRUM-98 (FR-SC-08, putaway); SCRUM-79 (FR-54, under epic SCRUM-12) for adjustments, ledger and alerts; SCRUM-120 for the NFR-SC checks; sprint ticket SCRUM-200 · **Rules:** [constitution](../../CONSTITUTION.md)

## Features

| Feature | What it does | Status |
|---|---|---|
| [bins](bins/README.md) · [spec](bins/spec.md) | Define zone-aisle-shelf bin locations | Implemented |
| [putaway](putaway/README.md) · [spec](putaway/spec.md) | Receiving queue, scan GTIN + bin, confirm; stock turns available | Implemented |
| [stock-ledger](stock-ledger/README.md) · [spec](stock-ledger/spec.md) | Append-only movements, stock by bin, available quantity, stock events | Implemented |
| [adjustments](adjustments/README.md) · [spec](adjustments/spec.md) | Reason-coded stock adjustments | Implemented |
| [cycle-counts](cycle-counts/README.md) · [spec](cycle-counts/spec.md) | Partial recounts that raise adjustments | Implemented |
| [stock-alerts](stock-alerts/README.md) · [spec](stock-alerts/spec.md) | Low-stock and stock-out alerts per variant | Implemented |

## Requirement IDs for the module

FR-SC-08, FR-54, NFR-SC-01 (putaway scan latency, Q10), NFR-SC-05, NFR-SC-06, NFR-SC-07, NFR-08, NFR-FT-08, NFR-17, NFR-19, SEC-08. The BRD has no `TC-*` cases for this area, but the Jira tickets carry them as comments: **TC-PUT-01..05** (SCRUM-98) and **TC-STK-01..09** (SCRUM-79). Those are used as sources; every other criterion is derived from the BRD text and marked `(new)`. The cycle-count feature has no ticket test cases. Criterion IDs are prefixed by feature: `BIN-`, `PUT-`, `LED-`, `ADJ-`, `CC-`, `ALR-`.

## BRD text (shared by all six features)

> **§5.13 Warehouse Inbound & Putaway.** Accepted, tagged stock is assigned a bin/shelf location (simple zone-aisle-shelf scheme is sufficient at MVP scale; a full slotting algorithm is a later optimisation).
> Putaway is recorded by scanning the GTIN + scanning/selecting the bin location — this is the system's source of truth for 'where physically is this unit', separate from 'how many do we have' (both matter: pick staff need location, not just count).
> Stock becomes Available-to-Promise (ATP) on the storefront only after putaway is confirmed — not the moment QC accepts it — so the storefront never shows stock that is still sitting in a receiving area, un-shelved and unfindable.

> **§5.14 Inventory Management & Stock Sync.** WMS holds the single source of truth for physical StockQuantity per SKU+bin; the storefront's cached/read-model stock (v8.0 Section 12.7) is refreshed from WMS via the same domain-event mechanism already specified for Products/ProductVariants.
> Reserved vs Available split: when a customer places an order, stock is reserved (not yet decremented) until pick is confirmed, so a cancelled-before-pick order releases the reservation rather than requiring a manual stock correction.
> Cycle counts (partial physical recounts on a rolling schedule) reconcile system stock against physical stock; a full annual stocktake is the fallback, cycle counts catch drift continuously instead.
> Edge case: cycle count finds a discrepancy → logged as a StockAdjustment with a mandatory reason code (damage, miscount, theft, found-stock) — never a silent overwrite of the quantity, for the same auditability reason as v8.0's return-window override (Section 5.6).

> **FR-SC-08:** System shall not mark a variant 'available' on the storefront until putaway is confirmed in the warehouse, not merely on QC acceptance.

> **FR-54:** Admin users shall manage stock per variant with low-stock/stock-out alerting.

> **NFR-SC-05:** All inventory-adjusting actions (QC accept/reject, cycle-count adjustment, reverse-QC restock) must be attributable to a user/session and timestamped — this is the supply-chain-side instance of v8.0's AuditLogs requirement (Section 12.1).

> **NFR-SC-06:** Stock-visible-on-storefront must reflect a putaway-confirmed WMS state within the same eventual-consistency bound already set in v8.0 Section 12.7 (a few seconds), not longer.

> **NFR-SC-07:** The warehouse/pack-station system must remain usable on a low-spec local network (a solo-founder or small-team operation, not an enterprise LAN) — this favours a lightweight web app or PWA over a heavy native install for pick/pack scanning.

> **§9.10.1 / §9.10.2 / §9.10.3:** WMS — "Bin/location mapping, putaway, stock counts, cycle counts, pick lists". Orders "must read WMS's live available-to-promise quantity". Events: `SKU.Generated / Tag.PrintRequested` published by SKU-GEN, consumed by WMS (awaits putaway) and Catalog; `Stock.PutawayConfirmed` published by WMS, consumed by the Catalog read model (ATP becomes visible on storefront).

> **§12.11:** Warehouse (WMS) core tables: WarehouseBins, StockByBin, PutawayRecords, StockReservations, CycleCounts, StockAdjustments.

*(Scope notes. (1) Reservations, allocation, pick and pack are Sprints 11-14 (FR-SC-09..12); this sprint builds the on-hand side and keeps the ledger open for a reserved split later, but creates no reservation table. (2) The storefront `ProductVariants` and read model arrive in Sprint 7 (Publish); this sprint publishes the events that read model will consume. (3) The BRD's Azure Service Bus wording is stale; events travel through the outbox per ADR 0004.)*

## Builds on Sprints 1-5 as delivered

- QC publishes `QcBatchAcceptedEvent` v1 (`Quality.Contracts`): batch id, sub-batch id, GRN id, PO id/no, outcome (`Accepted` | `Concession`) and lines (size id, colour id, qty). It carries no style id (the PO has exactly one style, D11 of labelling).
- Labelling publishes `VariantGtinAssignedEvent` v1 (variant id, style id, colour id, size id, SKU, GTIN) and `GtinVoidedEvent` v1, and `TagJobPrintedEvent` v1. `Labelling.Contracts` carries no read interface for GTINs yet.
- Outbox, per-module inbox idempotency, business numbers (ADR 0006), the acting-person name (no authentication until S8), `MapSecuredGroup` and the security baseline (ADR 0011) work as delivered in S5.

## Decisions (owner answers, 2026-10-14; the specs are Approved on this basis)

- **D0: Ticket and branch.** Sprint ticket **SCRUM-200** "Sprint 6 — Shelve" under epic SCRUM-17 covers SCRUM-98 (putaway) and SCRUM-79 (FR-54). Branch `SCRUM-200-warehouse`.
- **D1: Putaway-able.** Pending = QC-accepted minus already put away. Tag printing never blocks putaway. When a variant has no printed tag yet, the putaway screen lets staff **select the item from a list** instead of scanning, so staff are never blocked with nothing to scan.
- **D2: Grade.** The stock grade (`Accepted` | `Concession`) is a first-class dimension through putaway items, bin balances and the ledger, not a note. It is the point of Sprint 5's concession mark.
- **D3: Retired GTIN.** Putaway refuses a scan of a retired GTIN. Stock already tagged under a GTIN voided after putaway is corrected by an adjustment. (Closes Q14 carried from Sprint 5.)
- **D4: Bin-to-bin transfer** is deferred. If ever needed before a dedicated feature exists, do it by hand as two adjustments.
- **D5: Adjustments.** No approval gate; audited only. Large adjustments (above N = 20 units, configuration) need a **same-person re-confirm**, not a second approver; revisit when there is more than one warehouse staff member. Role checks are declared now (capability-shaped policy) and enforced when authentication arrives in Sprint 8.
- **D6: Alerts.** Dashboard badge, in-app list and an outbox event now. Email/WhatsApp waits for a notifications/sender feature.
- **D7: Scanners** behave as keyboard-wedge devices into a focused input. No camera scanning this sprint.
- **D8: "A few seconds"** = 5 s p95 from commit to stock event delivery (reuses SCRUM-120's standard).
- **D9: Cycle counts.** No automatic schedule; staff create counts manually; counts are blind.
- **D10: Scan latency.** The 300 ms p95 target applies to putaway scans as well as pick and pack.
- **D11: Over-receipt at putaway.** Refuse the excess scan and route it through a `FOUND_STOCK` adjustment. It is governed by the same adjustment policy as D5, including the large-adjustment re-confirm (not exempt).
- **D12: Reason codes.** Only `DAMAGE`, `MISCOUNT`, `THEFT`, `FOUND_STOCK`, as lookup rows; more can be added later without a deployment.
- **D13: Thresholds.** Per-variant overrides only this sprint, default 5 units. Per-category overrides wait for Sprint 7 (category structure is Catalog's Sprint 7 decision).
- **D14: Accepted limits.** The storefront side of stock-out and availability, and testing of order-decrement concurrency, are acknowledged as out of reach until Sprints 7-12.
- **From the BRD:** a single warehouse location (§10.4.1), so bins carry no warehouse column.

## Still open (defaults adopted; change only if you disagree)

- **Q-BIN-1: Bin code format.** Not answered. Default: zone 1-3 letters/digits, aisle 1-3 digits, shelf 1-2 letters/digits, upper-case, joined by hyphens (`A-03-2`). This is what gets printed on shelves.
- **Q-PUT-2 (new, from the putaway plan):** a GTIN voided while accepted stock is still unshelved leaves that item stuck (putaway refuses retired GTINs, D3, and Labelling only assigns a new GTIN on new accepted quantity). Default: the item returns to Awaiting GTIN and shows as stuck; a Labelling follow-up to re-assign on request needs your decision (options in [putaway/plan.md](putaway/plan.md)).
- **Q-LED-1:** no reserved column until the reservation feature. **Q-CC-1:** counting does not freeze a bin (CC-7 handles movement). **Q-ALR-2:** alerts are per variant across bins and grades.

Plans: [module design](plan.md) and ADR [0012](../../adr/0012-stock-ledger-and-balances.md) (proposed). Back to the [spec index](../README.md).
