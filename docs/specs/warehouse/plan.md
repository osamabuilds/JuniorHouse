# Warehouse: module design

- **Module README:** [README.md](README.md) (decisions D0-D14, shared BRD text)
- **Features:** [bins](bins/plan.md) · [stock-ledger](stock-ledger/plan.md) · [putaway](putaway/plan.md) · [adjustments](adjustments/plan.md) · [cycle-counts](cycle-counts/plan.md) · [stock-alerts](stock-alerts/plan.md)
- **ADRs:** [0012 stock ledger and balances](../../adr/0012-stock-ledger-and-balances.md) (new, proposed) · [0004 outbox](../../adr/0004-postgres-transactional-outbox.md) · [0006 business numbers](../../adr/0006-business-number-generation.md) · [0011 security and coupling](../../adr/0011-security-and-coupling-baseline.md)

This file holds what is shared by the six features. Each feature plan holds its own domain model, use cases, endpoints and security review.

## Module layout

`Romp.Modules.Warehouse.{Domain,Application,Infrastructure,Contracts}`, tests `Romp.Modules.Warehouse.Tests`. `WarehouseModule : IModule`, DbContext `WhseDbContext` (schema `WHSE`), `AddOutboxModule("WHSE", …)`, inbox `WHSE.INBX`. Add `Warehouse` to `ModuleNames` in `ModuleBoundaryTests`, to the `modules` array and MediatR assembly list in `Program.cs`, to `Romp.slnx`, and to the tests list. Same shape as `Labelling`.

| Feature (same name in every tree) | API folder | Web feature |
|---|---|---|
| bins | `Bins/` | `warehouse/bins` |
| stock-ledger | `StockLedger/` | `warehouse/stock-ledger` |
| putaway | `Putaway/` | `warehouse/putaway` |
| adjustments | `Adjustments/` | `warehouse/adjustments` |
| cycle-counts | `CycleCounts/` | `warehouse/cycle-counts` |
| stock-alerts | `StockAlerts/` | `warehouse/stock-alerts` |

## How the features depend on each other (one way)

```
 QC event ─────────────┐
 Labelling events ──┐  ▼
                    ▼ putaway ──► stock-ledger (IStockPosting, IStockVariantReader)
 bins ◄── putaway   │     └─────► bins         (IBinReader)
 bins ◄── adjustments ──────────► stock-ledger
 cycle-counts ──► adjustments (IAdjustmentPoster), bins, stock-ledger
 stock-alerts ◄── StockLevelChangedEvent (module's own outbox, handler in alerts)
```

- Each callee publishes its own small interface in its `Application/<Feature>/` folder (the Sprint 5 pattern): `IBinReader` (bins), `IStockPosting` and `IStockVariantReader` (stock-ledger), `IAdjustmentPoster` (adjustments).
- Direction: `putaway` → `bins`, `stock-ledger`; `adjustments` → `bins`, `stock-ledger`; `cycle-counts` → `adjustments`, `bins`, `stock-ledger`; `stock-ledger` → `bins` only (it implements `bins`' `IBinStockQuery`, the one port declared by the caller, so `bins` never depends on the ledger); `bins` knows none of the others; `stock-alerts` knows only `stock-ledger`'s read interface and the event. An architecture test (`WarehouseFeatureDependencyTests`, copying the Labelling one) enforces it, and enforces that Domain stays pure.
- **The variant reference.** `stock-ledger` owns `WHSE.VRNT_REF` (variant id, style, colour, size, SKU, GTIN, retired flag), filled from `VariantGtinAssignedEvent` / `GtinVoidedEvent` through one anti-corruption mapper. This is Warehouse's own copy; it never reads `SKU.*`.

## Cross-module reads (all additive, all in `*.Contracts`)

| Need | Source | Change |
|---|---|---|
| Accepted quantities | event `QcBatchAcceptedEvent` (`Quality.Contracts`) | none |
| GTIN, SKU, style/colour/size of a variant; retirement | events `VariantGtinAssignedEvent`, `GtinVoidedEvent` (`Labelling.Contracts`) | none |
| Style id for a PO | `Vendor.Contracts.IPurchaseOrderQueries.GetTermsAtAsync` | none (used by label-work already) |
| Colour/size names for screens | `Reference.Contracts.IApparelCodeQueries` (exists from S5) | none |

The module reads no other module's tables. Consumed events pass through two mappers (`QcBatchAcceptedMapper`, `VariantEventMapper`) that are the only files knowing the shape of those events.

## Events

| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|
| `QcBatchAcceptedEvent` (existing v1) | QC | putaway | yes (existing) |
| `VariantGtinAssignedEvent`, `GtinVoidedEvent` (existing v1) | Labelling | stock-ledger (variant reference) | yes (existing) |
| `StockPutawayConfirmedEvent` v1 | putaway | Catalog read model (S7); placeholder logger now | yes |
| `StockLevelChangedEvent` v1 | stock-ledger | stock-alerts; Catalog read model (S7) | yes |
| `StockAlertRaisedEvent` v1 | stock-alerts | notifications (later); placeholder logger now | yes |

An event with no registered handler stays claimed and retried by the dispatcher, so every published event gets a placeholder logging handler until its real consumer exists (the Sprint 5 pattern). Payloads carry ids, SKU, GTIN and quantities only: no staff names, no notes. Records live in `Warehouse.Contracts` with the same envelope as `LabellingEvent`.

## Data (schema `WHSE`, all names per `docs/db/naming.md`)

| Table | Owner |
|---|---|
| `BIN_MAIN` | bins |
| `VRNT_REF`, `STCK_BAL`, `STCK_MVMT`, `GRD_LKP`, `MVMT_KIND_LKP` | stock-ledger |
| `PUT_ITEM`, `PUT_REC`, `PUT_STS_LKP` | putaway |
| `ADJ_MAIN`, `ADJ_RSN_LKP` | adjustments |
| `CNT_MAIN`, `CNT_BIN`, `CNT_LINE`, `CNT_STS_LKP`, `CNT_NO_SEQ` | cycle-counts |
| `ALRT_THRS`, `ALRT_MAIN`, `ALRT_LVL_LKP`, `ALRT_STS_LKP` | stock-alerts |
| `OUTB_MSG`, `INBX` | module |

Foreign keys across feature ownership are limited to ids resolved through the ports (`STCK_BAL`/`STCK_MVMT` → `BIN_MAIN` and `VRNT_REF`, `PUT_REC`/`ADJ_MAIN`/`CNT_LINE` → `BIN_MAIN` and `VRNT_REF`); they are declared for integrity and indexed, and the code does not navigate them.

New glossary words, added to `docs/db/naming.md` in the same PR (none of these exist yet; `STCK` is stock, `CNT` is count, `CNTD` is counted, `EXPC` is expected, `BIN`, `ITEM`, `KIND`, `QTY`, `REF` exist): adjustment `ADJ`, aisle `AISL`, alert `ALRT`, balance `BAL`, change `CHNG` (exists, Sprint 3), confirmed `CNFM`, cycle `CYC`, grade `GRD`, idempotency `IDEM`, large `LRG`, level `LVL` (exists), movement `MVMT`, open `OPEN`, closed `CLSD`, putaway `PUT`, shelf `SHLF`, system `SYS`, threshold `THRS`, variance `VARN`, zone `ZONE`, scanned `SCAN`, awaiting `AWTG`, pending `PEND`, quality-control quantity `QC_QTY` (uses `QC`). (`ACPT` is "acceptable", so the accepted grade is a lookup row, not a column name.)

## Configuration (module options, validated at start-up)

`Warehouse:LargeAdjustmentUnits` (default 20, D5) and `Warehouse:DefaultLowStockThreshold` (default 5, D13). A negative or non-integer value stops the host.

## Policies

Adds `Policies.Inventory` ("stock actions": adjustments, thresholds, counts), a capability-shaped policy registered like `Staff`/`Admin`: allow-all via the development handler now, a real role requirement in S8 (decision D5). Putaway, bins and reads use `Policies.Staff`. The existing architecture test already requires every mapped endpoint to sit in a secured group.

## Module-wide security & coupling review

| Area | How the module meets it |
|---|---|
| Authorization | all endpoints in `MapSecuredGroup` (`Staff`, or `Inventory` for adjustments, counts and thresholds); enforced for real in S8; no per-user resources (SEC-05 n/a) |
| Input | FluentValidation on every command (lengths, ranges, charset, integer quantities); request records separate from entities; client-supplied totals ignored (LED-10, SEC-09 spirit) |
| Output encoding | all identifiers quoted in raw SQL; no HTML/ZPL produced; bin codes restricted to `[A-Z0-9-]` |
| Outbound calls | none |
| Resource limits | page size 1..100; a count has at most 200 bins and 2,000 lines; notes ≤ 200 chars; rate limit (`strict`) on putaway confirm, adjust and count confirm |
| Concurrency | atomic conditional SQL, unique and partial unique indexes, check constraint `QTY >= 0`, ledger-immutability trigger (ADR 0012) |
| Idempotency | inbox on consumed events; idempotency keys on putaway/adjustment/count confirm |
| Audit | acting person + system actor + time + reason on every change; ledger append-only with before/after |
| Data | no personal data; quantities are integers |
| Secrets/config | none; options validated on start |
| Errors | problem details only |
| Dependencies | none added |
| Coupling | contracts and events only; two anti-corruption mappers; one-way feature dependencies; a placeholder handler for each event with no consumer yet |

## Risks

- **Retired GTIN with unshelved stock (open, Q-PUT-2).** If a GTIN is voided while accepted quantity is still unshelved, putaway of that item is blocked (D3), and labelling only assigns a new GTIN when new accepted quantity arrives (S5 D9), so the item could be stuck. Needs an owner answer; see the putaway plan.
- **Event ordering.** The QC event and the GTIN event can arrive in either order; putaway items wait as `Awaiting GTIN` and are linked when the GTIN event arrives (both orders are tested).
- **NFR-SC-06 depends on the outbox dispatch interval**, not only on commit; the 5 s p95 is measured end to end in an integration test (LED-4) and the dispatcher interval is tuned if it fails.
- **Integration tests cannot load locally built DLLs here** (Application Control); run them in the Linux SDK container or CI and say so.
