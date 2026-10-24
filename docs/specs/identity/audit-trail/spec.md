# SCRUM-202: Audit trail

- **Status:** Approved (owner, 2026-10-16)
- **Jira:** SCRUM-202; source SCRUM-84
- **BRD sections:** §7.8, §12.1, §5.14
- **Requirement IDs:** FR-59, NFR-SC-05, SEC-25, NFR-FT-08

## Problem & intent
Every table already has `INSR_BY` / `UPDT_BY` columns, but they all say "system" because nobody is signed in. Once there are real users, each change must name the person, and the owner must be able to answer "who changed what, and when" across all modules. Per the owner's decision (D-5) this covers every state change in every existing module, not only the entities named in FR-59.

## Requirements (quoted from the BRD)
> **FR-59:** All admin actions on orders, stock, returns, and promotions shall be attributable to the acting user and timestamped.

> **NFR-SC-05:** All inventory-adjusting actions (QC accept/reject, cycle-count adjustment, reverse-QC restock) must be attributable to a user/session and timestamped — this is the supply-chain-side instance of v8.0's AuditLogs requirement (Section 12.1).

> **SEC-25:** Application logs shall explicitly exclude passwords, full card data, and full Child Profile records; a documented logging policy shall define what is safe to log versus what must be redacted/masked.

> **NFR-FT-08:** The notification subsystem's failure shall never block or roll back the underlying business transaction — e.g. if the email service is down, the order itself must still be placed successfully; the notification is retried independently.

## User flow
1. A signed-in person changes something in any module. The change carries their user name in the audit columns and produces an audit entry.
2. Background work (outbox dispatcher, seeders, scheduled jobs) acts as `system`; operator steps act as `operator-cli`.
3. A person with `audit.read` opens the audit page, filters by person, module, entity, record key and date range, and reads the entries.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| AC-1 | Given an authenticated request, when any entity is inserted or updated, then `INSR_BY` / `UPDT_BY` hold the signed-in user name; given background work, then `system`; the value is never empty. | FR-59, NFR-SC-05 |
| AC-2 | Given a save that inserts, updates or deletes entities in any module, then one audit entry per changed entity is recorded: module, entity type, record key, operation (insert, update, delete), names of the changed columns, actor, time, request correlation id. Technical tables (outbox, inbox, audit itself, migrations) are excluded. | D-5 |
| AC-3 | Given the entries, then they are written in the same transaction as the change by the transactional outbox; a rolled-back change leaves no entry, and a stopped dispatcher loses nothing (entries appear when it resumes). | ADR 0004, NFR-FT-08 |
| AC-4 | Given the audit store, then it is append-only: no endpoint updates or deletes an entry, and a database trigger rejects `UPDATE` and `DELETE` on the audit tables. | FR-59 |
| AC-5 | Given an event delivered twice, then exactly one entry exists (inbox-guarded, unique on source message and entity). | ADR 0011 |
| AC-6 | Given a changed column, then only its **name** is recorded, never its value; password hashes, MFA secrets, token hashes and recovery codes are excluded even by name. Business values live in the features' own history tables. | SEC-25 |
| AC-7 | Given sign-in outcomes, MFA changes, role assignments, account creation, deactivation and password resets, then each has an entry or a sign-in-attempt row attributable to the person concerned and the person acting. | FR-59 |
| AC-8 | Given a caller with `audit.read`, then the list endpoint returns pages of 1 to 100 entries, newest first, filterable by actor, module, entity type, record key, operation and date range, using one query per page (no N+1). | CONVENTIONS |
| AC-9 | Given the admin app, then an Audit page shows the list with the filters of AC-8, an entry detail, and a link from entries to the record's screen when one exists. | (new) |
| AC-10 | Given a save of up to 500 changed entities, then it produces at most one audit outbox message per module per save, and the added save time stays under 15% on the existing Warehouse and Catalogue integration scenarios. | (new) |
| AC-11 | Given the actor name written into audit columns, entries and logs, then it comes only from the authenticated user name (charset-restricted) or a fixed system value, so it cannot carry control characters (log injection). | ADR 0011 |
| AC-12 | Given an unauthenticated caller, then the audit endpoints return 401; without `audit.read`, 403. | SEC-08 |
| AC-13 | Given a failure of the audit consumer, then the business change is already committed and unaffected; the failed delivery is retried and, after the retry limit, lands in the dead-letter list visible on the existing outbox admin page. | NFR-FT-08 |

## Non-functional constraints
- Audit tables grow without bound; they are partitioned by month on `OCCR_DTE` from the start (BRD §12.6), and nothing is deleted in Sprint 8.
- Entries reference records by key only, so no personal data or Child Profile content enters the audit store (NFR-27..30 when S10 arrives).

## Out of scope
- Audit of reads (viewing a record), including future Child Profile access: decided in S10.
- Capturing new free-text "reason" fields; where an operation already records a reason (void, reject, adjustment), the feature's history keeps it.
- Tamper-evident hash chaining.

## Open questions
- [ ] Retention: kept forever for now; owner sets a policy before launch (S19).
- [ ] The audit page shows entity type and record key but not business values (AC-6); owner confirms this is enough for "what did they change".
