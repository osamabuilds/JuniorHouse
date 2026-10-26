# 0016 — Audit trail written through the transactional outbox

- Status: Accepted (approved by the owner 2026-10-16)
- Date: 2026-10-16

## Context

FR-59 and NFR-SC-05 require every action to be attributable and timestamped; the owner decided the trail covers every state change in every module (D-5). Each module owns its schema and no module may write to another's tables (ADR 0002), yet audit entries must be queryable in one place. Audit must never lose an entry (so no best-effort logging) and must never block or fail a business change (NFR-FT-08).

## Decision

1. The existing `AuditSaveChangesInterceptor` keeps stamping `INSR_*` / `UPDT_*` columns, now from the authenticated user.
2. A second interceptor step collects, during `SaveChanges`, one *audit record* per added, modified or deleted entity (excluding technical entity types: outbox, inbox, audit, migration history, and any type marked `[NotAudited]`). A record holds module name, entity type, key, operation and the **names** of changed columns, never values.
3. All records of one save are serialised into **one `AuditRecorded` integration event** (at most 500 records per event, split beyond that) written to the module's own outbox in the same transaction. The event type lives in `BuildingBlocks.Application`, so no module depends on another.
4. The Identity module consumes `AuditRecorded` from every module's outbox through an inbox-guarded handler and inserts rows into `IDNT.AUDT_ENTY` (and one row per changed column in `IDNT.AUDT_CHNG`). A unique key on (source message, entity type, record key) makes redelivery harmless.
5. `IDNT` audit tables are append-only: a trigger raises an error on `UPDATE` and `DELETE`, and the application database user has no rights to truncate. They are partitioned by month on `OCCR_DTE`.
6. Columns carrying secrets (`PWD_HASH`, `MFA_SECR`, `TOKN_HASH`, recovery-code hashes) are excluded by an attribute, even by name.
7. The actor is the authenticated user name; outside a request it is `system`, and operator steps use `operator-cli`.

## Alternatives considered

- **Database triggers on every table:** captures even raw SQL, but needs a trigger per table in every migration, cannot know the application user, and breaks the "behaviour lives in code" convention. Rejected. Raw SQL writes are rare and reviewed.
- **Writing audit rows directly into each module's schema:** simple and synchronous, but N copies of the table and no single query. Rejected.
- **Storing before/after values:** gives a fuller picture but copies personal and child data into a second store (NFR-27..30) and grows quickly. Rejected; features keep business history in their own history tables.

## Consequences

- The audit view is eventually consistent (seconds, bounded by the dispatcher interval); the record of the change is guaranteed because it commits with the change.
- One outbox message per save per module adds write volume; measured in the audit-trail spec (AC-10).
- A new feature is audited automatically; excluding an entity needs an explicit attribute and a reason.

## As built (2026-10-26)

- The collector is `AuditTrailSaveChangesInterceptor`. A new row gets its generated key during the save, so the message is written after the save, by plain SQL on the same connection and inside the same transaction (the interceptor opens one when the caller has none). The change and its entry still commit or fail together.
- `AuditRecorded` lives in `Romp.BuildingBlocks`, not in the Application project, so Persistence can use it.
- The extra round trips cost roughly 4 to 9 ms on a one-statement save in a local container; the 15 percent target is not met on that micro-benchmark (see the audit-trail spec, "As built").
- The store has no default partition; the handler creates the month it needs, and a daily worker keeps the next months ready.
