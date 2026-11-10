# COD reconciliation

- **Status:** Implemented 2026-11-10 (approved by delegation, owner away; recommendations applied as settings, to be confirmed)
- **Jira:** to be created
- **BRD sections:** §5.19 (COD Delivered status)
- **Requirement IDs:** FR-SC-17, NFR-SC-05 (audit), SEC-08

## Problem & intent
With Cash on Delivery the rider collects the money, not Romp. The courier pays it over later in a batch. Trusting the "Delivered" status alone means a missing or short payment goes unseen. Finance needs to see, per courier, which delivered COD parcels have been paid for, which are late, and which payments do not match.

## Requirements (quoted from the BRD)
> **§5.19:** COD Delivered status triggers COD reconciliation: courier remits collected cash on a settlement cycle (typically weekly), and Romp's finance view must reconcile 'orders marked delivered' against 'cash actually remitted by courier' — a real and common gap in COD-heavy markets, worth a dedicated reconciliation report rather than trusting delivered-status alone.

> **FR-SC-17:** System shall reconcile COD-marked-delivered orders against courier cash remittance on a scheduled cycle and flag mismatches.

## User flow
1. A parcel with cash to collect is delivered (tracking, S15). It is now "awaiting cash".
2. The courier sends its settlement statement: a reference, a date, a total and one line per waybill with the amount collected.
3. Finance records the statement. Every line is judged at once against the delivered parcel. Matching lines settle their parcels.
4. A daily job flags delivered parcels that are still unpaid after the courier's cycle (7 days).
5. Finance works the exception list: short, over, duplicate, unknown, not-delivered and not-COD lines. Each is resolved with a note (nothing disappears silently).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| COD-1 | Given a parcel is delivered with cash to collect, then it is "awaiting cash"; a parcel with no cash to collect is "not COD" and never appears in the reconciliation | §5.19, TRK-12 |
| COD-2 | Given a statement line whose waybill is a delivered COD parcel of that courier and whose amount equals the cash to collect, then the line is "matched" and the parcel is "settled" | FR-SC-17 |
| COD-3 | Given a line with a smaller amount, then it is "short" and the parcel is "short"; with a larger amount, "over" and the parcel is "over"; both are flagged | FR-SC-17 |
| COD-4 | Given a line for a parcel already settled (earlier statement or earlier line of this statement), then the line is "duplicate" and flagged; the parcel is unchanged (new) | FR-SC-17 |
| COD-5 | Given a line whose waybill is unknown or belongs to another courier, then it is "unknown" and flagged (new) | FR-SC-17 |
| COD-6 | Given a line for a parcel that is not delivered yet, then it is "not delivered" and flagged; the parcel is unchanged (new) | FR-SC-17 |
| COD-7 | Given a line for a parcel with no cash to collect, then it is "not COD" and flagged (new) | FR-SC-17 |
| COD-8 | Given a statement, then its stated total must equal the sum of its lines, otherwise nothing is saved (new) | FR-SC-17 |
| COD-9 | Given the same reference from the same courier twice, then the second changes nothing and answers with the first (idempotent) (new) | NFR-09 |
| COD-10 | Given a statement is recorded, then it cannot be edited; a correction is a new statement or a resolved line with a note (new) | NFR-SC-05 |
| COD-11 | Given a delivered COD parcel unpaid for more than the cycle (default 7 days), when the daily job runs, then it is flagged overdue once, and the flag clears when it settles | FR-SC-17 |
| COD-12 | Given a flagged line (not matched), when staff resolve it with a note, then it records who, when and why; a resolved short or over parcel becomes "resolved". A line cannot be resolved twice, and a matched line needs no resolution | NFR-SC-05 |
| COD-13 | Given the reconciliation page, then it shows per courier the cash awaiting, the cash overdue, the cash settled and the number of open exceptions | §5.19 |
| COD-14 | Given the lists of awaiting parcels, exceptions and statements, then each is paged with no per-row queries | CONVENTIONS |
| COD-15 | Given a recording or resolving action, then it needs `shipping.ops`; reading needs `shipping.view`; the actor is taken from the session | SEC-08 |
| COD-16 | Given a statement body, then it is size limited, has at most 500 lines, amounts above zero and below 10,000,000, and waybills in the waybill format (new) | SEC-25 |

## Non-functional constraints
- Money is `numeric(12,2)` / `decimal` in PKR; comparisons are exact to the paisa (no tolerance, Q-COD-3).
- Statements are recorded in one transaction: all lines and parcel states, or nothing.
- No customer data is stored or shown beyond the order number (SEC-25).

## Out of scope
Pulling statements from a courier's API (no accounts yet), bank-statement matching, refunds, the courier's service fee deducted from a remittance (Q-COD-5), returns and exchange requests (the other half of the S16 plan, a separate `returns` module).

## Open questions (recommendations applied)

| ID | Question | Applied recommendation |
|---|---|---|
| Q-COD-1 | What is each courier's remittance cycle? | Weekly: a parcel is overdue after 7 days (`Shipping:CodRemitDays`), the same for every courier until real contracts are known. |
| Q-COD-2 | How does the remittance data reach us? | Finance enters or pastes the courier's statement (reference, date, total, waybill and amount per line). No courier API yet. |
| Q-COD-3 | Tolerance for a small difference? | None: any difference is flagged, finance resolves it with a note. |
| Q-COD-4 | Who may record and resolve? | The existing codes: `shipping.ops` writes, `shipping.view` reads. A separate finance permission can follow if the owner wants one. |
| Q-COD-5 | Does the courier deduct its fee from the remittance? | Not modelled: the statement amount per line is the cash collected, the fee is settled outside the system. |

## As built (Sprint 16)
- **Where:** inside Shipping (schema `SHPG`, ADR 0032). Tables `RMTN_MAIN` and `RMTN_LINE`, lookups `COD_STS_LKP` and `RMTN_OTCM_LKP`, and two columns on `SHPG_MAIN` (`COD_STS_ID`, `COD_OVRD_DTE`) back-filled by the migration `AddCodReconciliation`.
- **Judging:** `CodJudge` is a pure function; a statement is recorded in one transaction and a second line for the same parcel in one statement is a duplicate.
- **Worker:** `FlagOverdueCodCommand` runs once per business day inside the existing shipping maintenance worker (`Shipping:CodRemitDays`, default 7).
- **API:** `/api/shipping/cod/*` (summary, awaiting, exceptions, remittances, remittance by id, record, resolve line) with `shipping.view` / `shipping.ops`; rows added to the authorization matrix.
- **Admin:** `/dispatch/cod` (overview, exceptions with inline resolve, awaiting parcels with an overdue filter, statements), `/dispatch/cod/remittances/new` (paste `waybill, amount` rows; the total is checked against the rows), `/dispatch/cod/remittances/:id`.
- **Deviations:** the code is `ShortPaid` in C# (the analyzer reserves `Short`); the database code stays `SHORT`. Two simultaneous statements with the same reference are decided by the unique index (the loser gets an error, the retry answers "already recorded"). The `Not delivered yet` outcome is not re-judged when the parcel is delivered later: staff resolve it with a note.
- **Verification:** `CodDomainTests`, `CodTests` (PostgreSQL), `ShippingEndpointsTests`, the authorization matrix tests, `cod.store.spec.ts`, `statement-text.spec.ts`.
