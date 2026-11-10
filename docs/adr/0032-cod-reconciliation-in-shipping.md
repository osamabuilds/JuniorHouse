# 0032: COD reconciliation lives in Shipping, from entered statements

- Status: Accepted by delegation (owner away, 2026-11-10; to be confirmed)
- Date: 2026-11-10

## Context
FR-SC-17 asks to reconcile delivered COD orders with the cash a courier remits. The facts are in Shipping (delivered time, cash to collect, waybill, courier). Orders only knows "Delivered". No courier API accounts exist.

## Decision
1. **Inside the Shipping module (schema `SHPG`)**, not a new module: it uses only shipment data and a courier statement, and adding a module would only add a contract with no second consumer.
2. **A remittance (statement) is an immutable aggregate with lines.** Each line is judged when the statement is recorded, by a pure function (`CodJudge`), into Matched, Short, Over, Duplicate, Unknown, NotDelivered or NotCod. Recorded in one transaction with the parcel's state change.
3. **The parcel keeps a COD state** (NotCod, Awaiting, Settled, Short, Over, Resolved) and an overdue flag set by a daily worker command. Both are lookups, not magic numbers.
4. **Flagged lines are resolved by a staff note**, never deleted or edited.
5. **Idempotent on `(courier, reference)`**; the statement total must equal the sum of lines.
6. **Existing permissions** `shipping.view` and `shipping.ops` (no new code, no Identity migration).
7. **Cycle and limits are settings** (`Shipping:CodRemitDays`, 7).

## Alternatives considered
- A separate `Finance` module: rejected for now (no second consumer; revisit with refunds in S17).
- Matching in the Orders module: rejected (Orders has no courier or waybill data).
- A tolerance for small differences: rejected (the owner can decide later; every difference is visible).

## Consequences
One migration (two tables, three lookups, three columns on the shipment), one more worker command, and a new admin screen. A courier's statement API can later feed the same record command.
