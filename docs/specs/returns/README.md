# Returns

Reverse logistics (Sprint 17): a parcel that comes back goes through a reverse-QC gate before its stock is restored, and nothing is refunded, credited or exchanged until that gate is passed.

**Status:** Specs written 2026-11-10 and **applied by delegation** (the owner is away and asked for S17 to be built; the open questions use my recommendations as settings, to be confirmed) · **Schema:** `RTRN` · **Code:** `src/api/src/Modules/Returns/` · **Web:** a Returns section of the admin app · **Jira:** none created · **Rules:** [constitution](../../CONSTITUTION.md), [CONVENTIONS](../../CONVENTIONS.md), [CLAUDE.md](../../../CLAUDE.md)

## Features

| Feature | What it does | Requirements | Status |
|---|---|---|---|
| [reverse-qc](reverse-qc/spec.md) | A returned-to-origin parcel becomes a case; staff receive it, inspect it, restock, restock as seconds or reject each line; completion is the only trigger for what follows | FR-SC-18, FR-SC-19, §5.19 RTO, §5.20 | Implemented |

## Module decisions

| ID | Decision | Why |
|---|---|---|
| D-1 | **New module `Returns` (schema `RTRN`).** It starts from Shipping's `ShipmentStatusChangedEvent` (RETURNED_TO_ORIGIN), reads the order's lines through `Orders.Contracts` and puts stock back through a new `Warehouse.Contracts` port. | Module rule (ADR 0002); ADR 0033. |
| D-2 | **One case per order** and kind; the case holds ids, quantities and codes, never a name, address or phone. | SEC-25, idempotency. |
| D-3 | **Completion is the trigger.** `ReturnQcCompletedEvent` is the only thing refund, credit or exchange may start from. | FR-SC-19. |
| D-4 | **The case has a `Kind`** (today only RTO) so that customer return requests and their RMA number can use the same QC gate later. | BRD §5.20 starts "at the point the physical parcel comes back". |

## Open questions (recommendations applied, owner to confirm)

| ID | Question | Applied recommendation |
|---|---|---|
| Q-RET-1 | Permissions? | Reuse `shipping.view` (read) and `shipping.ops` (act); a separate returns role can follow. |
| Q-RET-2 | What is "seconds" stock? | The existing warehouse grade `CONCESSION`; selling it at a discount through a separate SKU flag is Catalog work, not built here. |
| Q-RET-3 | Which bin does returned stock go to? | The person receiving the parcel scans or types the bin code; Warehouse refuses an unknown or inactive bin. |
| Q-RET-4 | How is an intact seal decided? | The receiver says so when receiving the parcel (a yes/no with an optional note). |
| Q-RET-5 | Who refunds? | Not built: the completed event carries the order, the payment method kind and what happened to each line; the refund itself (S17 refunds) waits for the owner's answer on how a COD order is refunded (bank transfer, store credit, JazzCash/Easypaisa). |

## Out of scope (not built in this sprint)

Customer return and exchange requests with windows, the sale-item rule, defect claims and agent override (the S16 returns half, needs the customer "My Orders" screen), return pickups booked with a courier, refunds and store credit, linked exchange orders (`ParentOrderId`), the contact form and the WhatsApp support log. They plug into the same case: a new `Kind` and a request in front of it.
