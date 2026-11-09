# SCRUM-202: Audit trail

- **Status:** Implemented (2026-10-26); see "As built" at the end
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

## As built
- **Collector (AC-2, AC-3, AC-6, AC-10):** `AuditTrailSaveChangesInterceptor` in `BuildingBlocks.Persistence`, registered by every module next to the outbox interceptor (`AuditArchitectureTests` fails a module that forgets). `SavingChanges` notes one record per added, modified or deleted entity; `SavedChanges` (when generated keys exist) writes `AuditRecorded` into the module's own `OUTB_MSG` by plain SQL on the same connection and transaction. If the caller has no transaction the interceptor opens one around the save and commits it itself, so a change and its entry commit or fail together. At most 500 records per message.
- **Where the contract lives:** `AuditRecorded` and the attributes `[NotAudited]` / `[AuditExcluded]` are in `Romp.BuildingBlocks` (namespace `Auditing`), not `BuildingBlocks.Application` as the plan said, because Persistence cannot reference Application. `AddOutboxModule` adds `AuditRecorded` to every module's registration, and the placeholder logging handler skips it.
- **What is left out:** technical tables carry `[NotAudited("reason")]` (outbox, inbox, sessions, pending sign-ins, pending MFA enrolments, password history); the stamping columns and concurrency tokens are never named; a column whose name has a word HASH, SECR, TOKN, PEPR or CIPH, or that carries `[AuditExcluded]`, is never named. Sign-in attempts (`LGIN_ATMP`), account rows, role mappings and recovery codes are audited.
- **Store (AC-4, AC-5):** `AddAuditTrail` creates `AUDT_OPRN_LKP` and two range-partitioned tables, `AUDT_ENTY` and `AUDT_CHNG`, with raw SQL. Triggers reject UPDATE, DELETE and TRUNCATE (including on each partition); `AUDT_PART_ENSR` creates monthly UTC partitions under an advisory lock. The unique key is (module, source message, table, record key, time), because outbox ids repeat across modules. There is no INSR/UPDT/concurrency column on these tables (they are append-only and carry the time and the person).
- **Partitions:** no default partition, so nothing can be hiding in a catch-all that blocks a later month. The handler creates the month it needs before inserting, and `AuditPartitionWorker` keeps the next months in place daily.
- **Module as text:** `MODL_NAME` holds the producing schema code (for example `CTLG`), an identifier and not an enumerated status, so a new module needs no lookup row.
- **Actor (AC-1, AC-11):** the actor is the authenticated user name, `system` outside a request, `operator-cli` for operator verbs; names outside the charset become `unknown` in the store.
- **AC-7:** account creation, role changes (mapping rows), MFA reset, deactivation, password resets, break-glass resets and every sign-in attempt are entries. For a role change or MFA reset the entry names the table and the person's key; the actor is who did it. This closes roles AC-6 and staff-accounts AC-12.
- **AC-8, AC-12:** `GET /api/identity/audit` and `GET /api/identity/audit/{id}` behind `audit.read`; no write route (`NoWriteRoutesExist`). With no dates the list covers the last 30 days; a range over 366 days is a 400. Authorization per role is in the matrix tests.
- **AC-9:** Audit page in the admin app (`features/identity/audit-trail`), menu entry for `audit.read`, record link for accounts and styles.
- **AC-10, deviation (owner to decide):** writing the audit row in the same transaction adds round trips (BEGIN, INSERT, COMMIT) to a one-statement save. On a pure-insert micro-benchmark of 30 rows against a local container that is roughly 4 to 9 ms per save (about 50 percent of a very fast save); the 15 percent target was not measured on the Warehouse putaway and Catalog publish scenarios and is not met on the micro-benchmark. `AuditTrailOverheadTests` is a loose relative tripwire (audited under 3 times plain plus 500 ms) against a gross regression. A cheaper design (put the message in the same batch for updates and deletes, whose keys are known) is possible if the owner wants it.
- **Retention and values (open questions):** unchanged; nothing is deleted, and the page shows no values by design.

## Traceability

| AC | Proof |
|---|---|
| AC-1 | `HttpContextCurrentActorTests`, `ActorScopeTests`, `IdentityAuditTests.OperatorBreakGlass_IsAuditedAsOperatorCli`, `AuditEndpointsTests.AChangeInAnotherModule_ReachesTheTrail_ThroughItsOutbox` |
| AC-2 | `AuditTrailCollectorTests.RecordsOneEntryPerEntity_WithItsGeneratedKey`, `.Deletes_AreRecordedWithTheirKey`, `.TechnicalTables_AreExcluded`, `AuditArchitectureTests.EveryModule_RegistersTheAuditCollector_NextToTheOutbox` |
| AC-3 | `AuditTrailCollectorTests.RolledBackChange_LeavesNoEntry`, `.CommittedChange_AlwaysHasItsOutboxRow_WithoutACallerTransaction`, `.FailedSave_LeavesNeitherTheChangeNorAnEntry` |
| AC-4 | `AuditStoreTests.UpdateAndDelete_AreRejectedByTheTrigger`, `.TruncatingAPartitionDirectly_IsRejectedToo`, `.InsertsLandInTheRightPartition`, `AuditEndpointsTests.NoWriteRoutesExist`, `AuditArchitectureTests.NothingInTheCode_UpdatesOrDeletesAuditRows` |
| AC-5 | `AuditStoreTests.TheSameMessageAndRecord_CannotBeStoredTwice`, `IdentityAuditTests.DeliveredTwice_StoresOnce` |
| AC-6 | `AuditTrailCollectorTests.StoresColumnNamesNotValues`, `.SecretColumns_AreExcludedEvenByName`, `IdentityAuditTests.NoSecretValueOrSecretColumnName_ReachesTheTrail`, `AuditStoreTests.AnEntry_NeverCarriesAForgedActorOrColumn` |
| AC-7 | `IdentityAuditTests.AccountRoleAndMfaChanges_AreAudited_WithWhoDidThem`, `.SignInFailures_AreAudited_AsAttemptRows`, `.OperatorBreakGlass_IsAuditedAsOperatorCli` |
| AC-8 | `AuditQueryTests.*`, `AuditEndpointsTests.ARangeOver366Days_IsABadRequest` |
| AC-9 | `audit-list.component.spec.ts`, `audit-detail.component.spec.ts`, `audit-filters.component.spec.ts`, `audit.effects.spec.ts`, `app.spec.ts`, `e2e/identity/audit-trail.e2e.spec.ts` |
| AC-10 | `AuditTrailCollectorTests.SplitsAtFiveHundred`, `.OneMessagePerSave_ForUpToFiveHundredEntities`, `AuditTrailOverheadTests` (tripwire only; see deviation) |
| AC-11 | `HttpContextCurrentActorTests`, `AuditStoreTests.AnEntry_NeverCarriesAForgedActorOrColumn` |
| AC-12 | `AuthorizationMatrixTests`, `RoleBehaviourTests` (audit.read is Owner-only), `AuditEndpointsTests` |
| AC-13 | `IdentityAuditTests.AFailingHandler_StoresNothing_AndLeavesTheCommittedChangeAlone`, the existing dispatcher retry and dead-letter tests |
