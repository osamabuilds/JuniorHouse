# SCRUM-202: Audit trail, technical plan

- **Status:** Approved (owner, 2026-10-16, with the Sprint 8 plans and ADRs 0015-0017; recorded 2026-10-26)
- **Spec:** [spec.md](spec.md) · **Module plan:** [../plan.md](../plan.md) · **ADR:** [0016](../../../adr/0016-audit-trail-through-the-outbox.md)

## Domain model
- Audit entries are immutable records: source message id, module, entity type, record key, operation, actor, occurred date, correlation id, changed column names. No behaviour besides construction and validation (actor and column names charset-checked).
- `AuditRecorded` event (in `BuildingBlocks.Application`): a list of up to 500 records plus module name, actor and correlation id.

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `AuditCollector` (in `BuildingBlocks.Persistence`, used by the save interceptor) | change tracker | `AuditRecorded` appended to the module's outbox | AC-2, AC-3, AC-6, AC-10 |
| `RecordAuditHandler` (Identity, inbox-guarded) | `AuditRecorded` | rows in `AUDT_ENTY` / `AUDT_CHNG` | AC-3, AC-5, AC-13 |
| `ListAuditQuery` | actor, module, entity type, key, operation, date range, page | page of entries with changed columns | AC-8 |
| `GetAuditEntryQuery` | id | entry detail | AC-8 |

`ICurrentActor` gets `HttpContextCurrentActor` (user name from the authenticated principal; `system` outside a request; `operator-cli` set explicitly by operator verbs through an `ActorScope`). `AuditSaveChangesInterceptor` is unchanged apart from where the name comes from.

## API endpoints
`GET /api/identity/audit`, `GET /api/identity/audit/{id}`: `audit.read`; no write routes exist.

## Events
| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|
| `AuditRecorded` | every module's save interceptor | Identity `RecordAuditHandler` | yes, same transaction as the change |

## Data
`AUDT_ENTY` (monthly range partitions on `OCCR_DTE`; a job creates the next three months), `AUDT_CHNG`, `AUDT_OPRN_LKP`, immutability triggers (module plan). Migration uses raw SQL with every identifier quoted. Idempotency key: unique `(SRC_MSG_ID, ENTY_TYP_NAME, ENTY_KEY, OCCR_DTE)`. Composite record keys are joined with `|` into `ENTY_KEY` (a single identifier string, not a list of values).

## NFR / security design
- **Overhead (AC-10):** collection reads only `ChangeTracker.Entries` already loaded; one JSON payload per save; payload capped at 500 records, splitting beyond. Measured by a benchmark-style test on the Warehouse putaway and Catalog publish scenarios (limit 15%).
- **Exclusions:** entity types implementing `INotAudited`, outbox/inbox/audit/migration tables, and columns marked `[AuditExcluded]` (hashes, secrets).
- **Immutability:** trigger `BEFORE UPDATE OR DELETE` raising an exception; the application database role has no `TRUNCATE`.
- **Failure isolation (AC-13):** handler errors use the standard retry and dead-letter path; the business change is already committed.
- **Ordering:** entries carry the original change time (`OCCR_DTE` from the stamping time), not delivery time.

## Frontend
`features/identity/audit-trail/`: filter form (actor, module, entity type, key, date range), paged table (`<table>` with `<th scope>`), detail panel listing changed columns, link to the record's screen through a small route-resolver map per module. Dates shown in the viewer's locale with the UTC value in a tooltip.

## Risks & alternatives considered
- **Eventual consistency:** an entry appears seconds after the change; documented on the page.
- **Partitioning in EF:** only the parent table is mapped; tested with Testcontainers; if partition maintenance proves fragile, fall back to an unpartitioned table with an index and revisit at 10 million rows.
- **Volume:** every save across all modules produces an event; the per-save cap and no-values rule keep rows small. The existing outbox purge keeps outbox tables bounded.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | Read endpoints need `audit.read`; no write endpoints; the handler is internal (no HTTP). |
| Input validation | Filters typed and bounded; page size 1..100; date range at most 366 days per query. |
| Output encoding | Entry text rendered with Angular escaping; actor names restricted to `[a-z0-9._:-]`, so no control characters reach logs or UI. |
| Outbound calls | None. |
| Resource limits | Page size cap, range cap, 500-record event cap, index-backed filters only (a filter combination without an indexed column requires a date range). |
| Concurrency | Append-only inserts; redelivery absorbed by the unique key and inbox. |
| Idempotency | Inbox-guarded handler plus unique key. |
| Audit | This is the audit store; its own changes (none, append-only) are not re-audited, the trigger rejects edits. |
| Data protection | Column names only, never values; secrets excluded even by name; keys only, so no child or personal data content (NFR-27..30, SEC-25). |
| Secrets and dangerous config | None; trigger cannot be disabled by the application role. |
| Error responses | Problem details; no SQL text. |
| New dependencies | None. |
| Coupling | Modules know only `AuditRecorded` in BuildingBlocks; Identity is the sole consumer; the architecture test forbids modules referencing Identity. |
| Abuse cases | (1) A privileged user tries to erase their trail: no API, trigger blocks SQL edits, the application role cannot truncate. (2) A hostile name containing newlines or log-forging text: charset restriction at account creation and at the actor boundary. (3) Flooding with large saves to bloat storage: per-save cap, rate limits on write endpoints, monthly partitions that can be archived. (4) Missing entries when a dispatcher stops: entries are in the outbox with the change and drain when it resumes. |
