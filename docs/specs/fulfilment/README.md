# Fulfilment

Turning a confirmed or paid order into a packed parcel (Sprint 14, "Pick and pack"): allocate bin-located stock first-in-first-out, a pick list the picker works by scanning, pick exceptions instead of forced picks, and a pack station that re-checks, weighs, chooses packaging and prints the packing slip. Booking the courier, the waybill, manifests and tracking are S15.

**Status:** Specs written 2026-11-09 and **applied by delegation**: the owner was away and asked for the next sprint to continue, so the open decisions below use my recommendations as configurable settings. The owner can change any of them when back. · **Schema:** `FULF` (proposed) · **Code:** `src/api/src/Modules/Fulfilment/` (planned) · **Web:** a Warehouse section of the admin app · **Jira:** SCRUM-100 (pick lists, allocations), SCRUM-101 (pick exceptions), SCRUM-102 (pack station, weight); nothing created or moved in Jira · **Rules:** [constitution](../../CONSTITUTION.md), [CONVENTIONS](../../CONVENTIONS.md), [CLAUDE.md](../../../CLAUDE.md)

## Features

| Feature | What it does | Requirements | Status |
|---|---|---|---|
| [allocations](allocations/spec.md) | A confirmed or paid order is allocated to specific bins, oldest putaway first; released if the order is cancelled before pick | §5.15, FR-SC-09 (RSV-9), FR-SC-10 | Draft |
| [pick-lists](pick-lists/spec.md) | One pick list per order, in walking order; scan the bin and the GTIN to confirm each pick; confirming a pick turns the reservation into a stock decrement | §5.15, FR-SC-10, NFR-SC-01, NFR-SC-05 | Draft |
| [pick-exceptions](pick-exceptions/spec.md) | A picker flags a stock mismatch instead of force-completing; the line waits for restock and the customer is told | §5.15, FR-SC-11 | Draft |
| [pack-station](pack-station/spec.md) | A second, independent scan check, the packaging rule, weighing against the expected weight | §5.16, FR-SC-10, FR-SC-12 | Draft |
| [packing-slips](packing-slips/spec.md) | The slip inside the parcel: items, prices, the cash to collect, how to return | §5.16, §5.17 | Draft |
| [warehouse-app](warehouse-app/spec.md) | The phone and tablet screens for picking and packing, installable, usable on a weak network | NFR-SC-01, NFR-SC-07 | Draft |

## Module decisions

| ID | Decision | Why |
|---|---|---|
| D-1 | **New module `Fulfilment` (schema `FULF`).** It never reads Warehouse or Orders tables; it talks to them through their contracts and events. | Constitution: a new module needs an ADR, a row here, a glossary entry and an architecture-test entry. |
| D-2 | **Fulfilment starts on `OrderConfirmedEvent` (cash on delivery confirmed) or `OrderPaidEvent` (online order paid),** never on `OrderPlacedEvent`. | BRD §5.15: "Order placed and confirmed (COD confirmation call passed, or card payment captured) → order enters the fulfilment queue." |
| D-3 | **Warehouse does the allocation and the stock decrement,** through a new contract (`IStockAllocator`): it owns the bins and balances. Fulfilment keeps the result (bin and quantity) as its own snapshot. | One owner for stock truth (warehouse §5.14). |
| D-4 | **One order, one pick list.** Batch picking across orders is a later optimisation. | BRD §5.15: "not MVP". |
| D-5 | **Scanner input is keyboard text.** A USB barcode scanner types the code and Enter, so any scanner works. A phone camera scan can be added later behind the same input. | NFR-SC-07 (low-spec, no native install). Q-FUL-4. |
| D-6 | **The screens live in the admin app** (a Warehouse section, large touch layout, installable as a PWA), behind staff sign-in and new permissions `fulfilment.view`, `fulfilment.pick`, `fulfilment.pack`. | Reuses sign-in, roles and audit trail. |

## Open questions (recommendations applied, owner to confirm)

| ID | Question | Applied recommendation |
|---|---|---|
| Q-FUL-1 | When does a parcel go in a poly bag and when in a box? (BRD leaves "X grams") | A poly bag when the order is **one soft item and the expected weight is at most 500 g**; a box when there is more than one item or the expected weight is more than 500 g. Both numbers are settings (`Fulfilment:PolyBagMaxGrams`, `Fulfilment:PolyBagMaxItems`). |
| Q-FUL-2 | How far may the weighed parcel differ from the expected weight before it is flagged? | The larger of **15 percent or 60 grams** (`Fulfilment:WeightTolerancePercent`, `WeightToleranceGrams`). |
| Q-FUL-3 | **Where does a garment's weight come from?** The catalogue has no weight today. | A weight per product category in grams, kept as a setting table in Fulfilment (`FULF_WGHT_CATG`), with a default of 250 g for a garment and 80 g for packaging. Staff can refine per category in the admin app. Weighing is then a safety check, not a billing figure. |
| Q-FUL-4 | Which scanner? | Any USB keyboard-wedge scanner now; phone camera later. |
| Q-FUL-5 | What does the customer hear when a line is delayed? | A "your item is delayed" message through Notifications (event `OrderLineDelayedEvent`); the wording is a template the business edits. |
| Q-FUL-6 | Is the Warehouse a separate installable app or part of the admin? | Part of the admin app (D-6), installable. |

## Requirement IDs for the module

§5.15, §5.16, FR-SC-09 (RSV-9 hook), FR-SC-10, FR-SC-11, FR-SC-12, NFR-SC-01, NFR-SC-05, NFR-SC-07. Criterion IDs are prefixed by feature: `ALC-`, `PCK-`, `PEX-`, `PAK-`, `SLP-`, `APP-`. There are no `TC-*` cases for fulfilment in the BRD, so every criterion marked `(new)` comes from the BRD text and its edge cases.

## Out of scope

Courier booking, the waybill, the shipping label, manifests and tracking (S15). Returns and reverse logistics (S16, S17). Batch picking, zone picking, carton labels (SSCC). Inbound putaway (done in S6).
