# Reverse QC

- **Status:** Implemented 2026-11-10 (approved by delegation, owner away)
- **Jira:** to be created
- **BRD sections:** §5.19 (RTO), §5.20
- **Requirement IDs:** FR-SC-18, FR-SC-19, NFR-SC-05 (audit), SEC-08

## Problem & intent
A parcel that comes back unopened, or opened, must not be put on the shelf, refunded or exchanged on trust. A person checks it, decides line by line what it is worth, and only then does stock or money move.

## Requirements (quoted from the BRD)
> **§5.19 RTO:** RTO (Returned to Origin): parcel comes back to Romp's warehouse unopened by the customer → enters the same reverse-QC gate as a return (Section 4.12), but is restocked immediately if the seal is intact.

> **§5.20:** Parcel received at warehouse against the RMA number, opened, and inspected: item matches what was ordered, unworn/unwashed condition, original tags still attached, no damage. Reverse-QC outcome: Restock (item goes back to sellable ATP inventory, same bin-location logic as Section 5.13), Restock as 'graded/seconds' (minor issue, sell at discount via a separate SKU flag), or Reject (customer's stated reason doesn't match physical condition — routes to a manual resolution case). Refund/credit/exchange is triggered only after reverse-QC completes, not at pickup.

> **FR-SC-18:** System shall route a returned/RTO parcel through reverse-QC before restoring stock, except intact-seal RTO parcels which may restock immediately.

> **FR-SC-19:** System shall trigger refund/credit/exchange only after reverse-QC completion, not at return-pickup.

## User flow
1. Shipping reports RETURNED_TO_ORIGIN for a parcel. A return case is opened with the order lines (awaiting receipt).
2. Staff receive the parcel by its order number, say whether the seal is intact, and name the bin to put stock into.
3. Intact seal: every line is restocked as sellable and the case completes at once.
4. Seal not intact: the case waits for inspection. For each line staff choose Restock, Restock as seconds, or Reject (with a note), for the quantity that came back.
5. When every line is decided, staff complete the case. The completion announces the result; nothing before it does.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| RQC-1 | Given a shipment is returned to origin, then one case is opened for the order with its lines and the status "awaiting receipt"; the same event again opens nothing more | FR-SC-18, NFR-09 |
| RQC-2 | Given a case awaiting receipt, when staff receive it with a bin and "seal intact", then every line is restocked as sellable stock in that bin and the case is completed | §5.19 |
| RQC-3 | Given a case awaiting receipt, when staff receive it with "seal not intact", then no stock changes and the case waits for inspection | FR-SC-18 |
| RQC-4 | Given a case in inspection, when staff decide a line as Restock or Seconds, then that quantity goes to the bin as sellable or seconds (grade) stock, once | §5.20 |
| RQC-5 | Given a line decided as Reject, then a note is required, no stock changes and the line is marked rejected | §5.20 |
| RQC-6 | Given a line, then the quantity decided is at least 1 and at most the quantity on the order, and a line is decided once | (new) |
| RQC-7 | Given some lines are undecided, when staff try to complete, then it is refused and says how many lines are waiting | (new) |
| RQC-8 | Given every line is decided, when staff complete the case, then it is frozen and `ReturnQcCompletedEvent` is published with the order and the result of each line; before that no such event exists | FR-SC-19 |
| RQC-9 | Given a case is received twice or a decided line is decided twice, then the second changes nothing in stock and is refused with a plain message (new) | NFR-09 |
| RQC-10 | Given restocking fails (unknown or inactive bin), then nothing is saved for that step and the case stays as it was | (new) |
| RQC-11 | Given the case list, then it is paged, filterable by status, newest first, with no per-row queries; the detail shows lines, who did what and when | CONVENTIONS, NFR-SC-05 |
| RQC-12 | Given any action, then it needs `shipping.ops` (reading `shipping.view`) and records the staff member from the session | SEC-08 |
| RQC-13 | Given a case, then it holds only ids, quantities, codes and the order number: no name, address or phone | SEC-25 |

## Non-functional constraints
Stock moves through Warehouse's own posting rules, exactly once per line (deterministic idempotency key). The case is saved and stock is restocked in the correct order so that a failure leaves no half-decided line.

## Out of scope
See the module README: customer requests, return pickups, refunds, exchange orders.

## Open questions
See Q-RET-1 to Q-RET-5 in the module README.


## As built (Sprint 17)

### What was built
- **Module `Returns`** (schema `RTRN`, ADR 0033): `src/api/src/Modules/Returns/`, feature folder `ReverseQc`. Tables `RTRN_MAIN`, `RTRN_LINE`, lookups `RTRN_KIND_LKP`, `RTRN_STS_LKP`, `RTRN_DCSN_LKP`, plus the module's outbox and inbox. One case per order and kind, enforced by a unique index.
- **Opening (RQC-1, RQC-13):** `ShipmentReturnedConsumer` handles `ShipmentStatusChangedEvent` with status `RETURNED_TO_ORIGIN` behind the inbox and sends `OpenReturnCaseCommand`, which reads the order lines once from `IOrderFulfilmentQueries` and keeps variant id, SKU, product name, colour, size and quantity. The order number is kept; no name, city, address or phone is.
- **Receiving (RQC-2, RQC-3, RQC-9, RQC-10):** the receiver gives the bin and whether the seal is intact. Intact: each line is restocked through Warehouse and decided as Restock, then the case completes in the same unit of work. Not intact: no stock changes and the case waits for inspection. A bin is required in both cases (it is where later restocks go).
- **Deciding (RQC-4 to RQC-6):** the rules run on the case first (quantity 1 to the ordered quantity, a note for Reject, a line decided once), then Warehouse is asked for Restock or Seconds, then the decision is saved. Seconds uses the existing `CONCESSION` grade (Q-RET-2).
- **Stock (RQC-4, RQC-9, RQC-10):** Warehouse gained `IStockReturns.RestockAsync` (Warehouse.Contracts). It posts a found-stock adjustment through `IAdjustmentPoster` with an idempotency key derived from `return:{case}:line:{line}`, so a repeat or a crash never restocks twice. An unknown or inactive bin comes back as a plain refusal; the Returns handler raises it as a conflict and nothing is saved. The Warehouse call runs in Warehouse's own transaction, so units already restocked in an earlier line of an intact receive stay restocked after a later failure; the retry finds them by key and adds nothing.
- **Completing (RQC-7, RQC-8):** completion needs every line decided and publishes `ReturnQcCompletedEvent` (order id, order number, per line the variant, SKU, decision code and quantity) from the aggregate, in the same transaction. Nothing else publishes a result.
- **Reading (RQC-11):** `GET /api/returns/cases` (paged, `status` filter, newest first, one query for rows and counts) and `GET /api/returns/cases/{id}` (lines and who did what and when).
- **Access (RQC-12):** reads need `shipping.view`; receive, decide and complete need `shipping.ops` (Q-RET-1). The staff member is taken from the session. Authorization-matrix rows are added.
- **Admin app:** `features/returns/reverse-qc` (NgRx store, `/returns` list with status filter and paging, `/returns/:id` case page with receive form, a decision form per line and a complete button), a "Returned parcels" menu entry for `shipping.view`.

### Differences from the plan
- Domain rule violations (a repeat receive, a line decided twice, a missing note) answer with the platform's conflict status (409), like every other module, not 400.
- The Warehouse port lives in the existing `Adjustments` feature folder of Warehouse, not a new `Returns` folder, so the structure-sync rule needs no extra spec card.
- Refusals from Warehouse reach the case page as the plain Warehouse message (for example "Bin X does not exist.").

### Tests
- Warehouse: `StockReturnsTests` (RQC-4, 9, 10). Returns: `ReturnCaseDomainTests` and `ReverseQcFlowTests` against PostgreSQL (RQC-1 to RQC-13, including the constant query count of the list). API: `ReturnsEndpointsTests` and the authorization matrix. Web: `reverse-qc.store.spec.ts`. End to end: `e2e/journeys/returns-reverse-qc.e2e.spec.ts` is written but has not been run (it needs a second Owner on the stack, as for the S15 and S16 journeys).

### Still open
Q-RET-1 to Q-RET-5 keep the recommendations applied by delegation. Refunds, credits and exchanges (they start from `ReturnQcCompletedEvent`), customer return requests, defect claims, and selling seconds at a discount remain unbuilt.
