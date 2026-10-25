# Identity: module plan (Sprint 8)

- **Specs:** [staff-accounts](staff-accounts/spec.md) · [sessions](sessions/spec.md) · [mfa](mfa/spec.md) · [roles](roles/spec.md) · [audit-trail](audit-trail/spec.md)
- **ADRs:** [0015](../../adr/0015-identity-module-sessions-and-permissions.md) · [0016](../../adr/0016-audit-trail-through-the-outbox.md) · [0011](../../adr/0011-security-and-coupling-baseline.md)
- **Status:** Proposed, awaiting approval. Feature plans hold the per-feature detail and each carries its own security review table.

This file holds what the five features share: the project layout, the data model, the endpoint list, new packages, the host wiring and the order of work.

## Project layout

```
src/api/src/Modules/Identity/
  Romp.Modules.Identity.Domain/          StaffAccounts/ Sessions/ Mfa/ Roles/ AuditTrail/
  Romp.Modules.Identity.Application/     same feature folders (commands, queries, ports)
  Romp.Modules.Identity.Infrastructure/  same feature folders (EF configs, endpoints, hashing, TOTP, cookies)
                                         Migrations/  Persistence/  IdentityModule.cs
  Romp.Modules.Identity.Contracts/       nothing public in Sprint 8 (no other module consumes Identity)
src/api/src/BuildingBlocks/Romp.BuildingBlocks.Web/
  Permissions.cs                         permission codes used in endpoint declarations
  PermissionRequirement.cs               RequirePermission(...) endpoint extension
  HttpContextCurrentActor.cs             ICurrentActor from the authenticated principal
src/api/src/BuildingBlocks/Romp.BuildingBlocks.Application/
  Auditing/AuditRecorded.cs              the integration event of ADR 0016
```

`ICurrentActor` stays in `BuildingBlocks.Persistence`; the web host registers `HttpContextCurrentActor` in place of `SystemCurrentActor` (background work still resolves `system`). The permission **handler** lives in `Identity.Infrastructure` (it reads `IDNT`); other modules see only the `Permissions` codes and the `RequirePermission` extension. Add `Identity` to `ModuleNames` in `ModuleBoundaryTests`.

## Data model (schema `IDNT`; all names follow `docs/db/naming.md`)

Transactional tables carry `ID bigint identity`, the four audit columns and `xmin` as concurrency token. Lookups follow the standard lookup shape and are seeded by migration.

| Table | Purpose | Key columns |
|---|---|---|
| `ROLE_LKP` | Roles: OWNER, CTLG, ORDR_OPS, SUPT, MKTG (codes <= 30 chars) | standard lookup columns + `ALL_PERM_IND` (superset) |
| `PERM_LKP` | Permission codes (see roles spec) | standard lookup columns |
| `ROLE_PERM_MAP` | Role to permission | `ROLE_ID`, `PERM_ID`; PK both |
| `USR_MAIN` | Staff account | `USR_NAME` unique lower-case, `DISP_NAME`, `PWD_HASH`, `PWD_CHNG_DTE`, `MUST_CHNG_PWD_IND`, `TEMP_PWD_EXPY_DTE`, `ACT_IND`, `LAST_LGIN_DTE`, `MFA_ENRL_DTE`, `MFA_SECR_ENC` (bytea), `MFA_KEY_ID`, `MFA_LAST_STEP_NO` |
| `USR_PWD_HIST` | Last password hashes (reuse check) | `USR_ID`, `PWD_HASH` |
| `USR_ROLE_MAP` | User to role | `USR_ID`, `ROLE_ID` |
| `USR_RCVR_CODE` | MFA recovery codes | `USR_ID`, `CODE_HASH`, `USED_DTE` |
| `SESS_MAIN` | Session | `USR_ID`, `ACSS_TOKN_HASH`, `ACSS_EXPY_DTE`, `RFSH_TOKN_HASH`, `PREV_RFSH_TOKN_HASH`, `RFSH_EXPY_DTE`, `ABS_EXPY_DTE`, `RVKD_DTE`, `RVKD_RSN_ID`, `CLNT_IP_ADDR`, `USR_AGNT` (truncated) |
| `SESS_RVKD_RSN_LKP` | SIGN_OUT, SIGN_OUT_ALL, ADMIN, REUSE, PWD_CHNG, DEACT, MFA_RESET, EXPIRED | lookup |
| `LGIN_PEND` | Step-1-passed, awaiting MFA (5 minutes) | `USR_ID`, `TOKN_HASH`, `EXPY_DTE`, `FAIL_CNT` |
| `LGIN_ATMP` | Every sign-in or MFA attempt (NFR-15); throttle state is derived from it | `ATMP_NAME` (as typed, <= 40, sanitised), `CLNT_IP_ADDR`, `OTCM_ID`, `ATMP_DTE`, nullable `USR_ID` |
| `LGIN_OTCM_LKP` | OK, BAD_CRED, INACTIVE, TEMP_EXPD, MFA_BAD, MFA_OK, THROTTLED, REFRESH_REUSE | lookup |
| `AUDT_ENTY` | One audited entity change | `SRC_MSG_ID`, `MODL_NAME`, `ENTY_TYP_NAME`, `ENTY_KEY`, `OPRN_ID`, `ACTR_NAME`, `OCCR_DTE`, `CORR_ID`; partitioned monthly on `OCCR_DTE` |
| `AUDT_OPRN_LKP` | INSERT, UPDATE, DELETE | lookup |
| `AUDT_CHNG` | Changed column names of an entry (one row per column, so no list in a column) | `AUDT_ENTY_ID`, `OCCR_DTE`, `COLM_NAME` |
| `OUTB_MSG`, `INBX` | Standard module outbox and inbox | existing pattern |

Indexes: unique `USR_MAIN(USR_NAME)`, `SESS_MAIN(ACSS_TOKN_HASH)`, `SESS_MAIN(RFSH_TOKN_HASH)`, `SESS_MAIN(PREV_RFSH_TOKN_HASH)`; `LGIN_ATMP(ATMP_NAME, CLNT_IP_ADDR, ATMP_DTE)` for the throttle query; `AUDT_ENTY(ACTR_NAME, OCCR_DTE)`, `(MODL_NAME, ENTY_TYP_NAME, ENTY_KEY)`, unique `(SRC_MSG_ID, ENTY_TYP_NAME, ENTY_KEY, OCCR_DTE)`; every FK column indexed. A trigger on `AUDT_ENTY` and `AUDT_CHNG` rejects `UPDATE` and `DELETE`. Session and attempt rows older than 90 days are purged by a daily job (audit rows never).

**New glossary words** (added to `docs/db/naming.md` in the same change as the first migration): access ACSS, address (exists), agent AGNT, all ALL, audit AUDT, change (exists), client CLNT, (column is COLM, Sprint 9), correlation CORR, enrolled ENRL, entity ENTY, failed FAIL, hash HASH, login LGIN, module MODL, multi-factor authentication MFA, occurred OCCR, operation OPRN, password PWD, pending (exists), permission PERM, recovery RCVR, refresh RFSH, revoked RVKD, role ROLE, secret SECR, session SESS, source SRC, step STEP, temporary TEMP, token TOKN, absolute ABS, encrypted ENC, must MUST, user (exists), IP address IP. Words already in the glossary keep their abbreviation (`NAME`, `DTE`, `IND`, `CNT`, `OTCM`, `ATMP`, `EXPY`, `LAST` is new).

## Endpoints (all through `MapSecuredGroup`; anonymous routes are on the explicit list of roles AC-10)

| Method | Route | Needs |
|---|---|---|
| POST | `/api/auth/sign-in` | anonymous, strict rate limit |
| POST | `/api/auth/mfa/verify` | pending cookie, strict |
| POST | `/api/auth/refresh` | refresh cookie, strict |
| POST | `/api/auth/sign-out`, `/api/auth/sign-out-all` | session |
| GET | `/api/auth/me` | session (any scope); returns user, scope, permissions, CSRF value |
| POST | `/api/auth/password` | session (scope allows) |
| POST | `/api/auth/mfa/enrol/start`, `/enrol/confirm`, `/recovery-codes` | session (scope allows) |
| GET, POST | `/api/identity/staff`, `/{id}`, `/{id}/deactivate`, `/reactivate`, `/reset-password`, `/reset-mfa`, PUT `/{id}/roles` | `staff.manage` |
| GET | `/api/identity/roles` | `staff.manage` |
| GET | `/api/identity/audit`, `/{id}` | `audit.read` |
| GET | `/api/admin/health` | `ops.admin` |

Operator verbs on the host executable (handled before the web host starts): `create-owner`, `reset-owner [--password] [--mfa]` (break-glass, Owner accounts only), `migrate`. They read secrets from environment variables or a no-echo prompt, never arguments, and write to the audit trail as `operator-cli`.

## Packages (versions pinned in `Directory.Packages.props`; each checked for licence and known vulnerabilities before adding)

| Package | Use | Licence |
|---|---|---|
| `Konscious.Security.Cryptography.Argon2` (or `Isopoh.Cryptography.Argon2`; the one that passes the check) | Argon2id | MIT |
| `Otp.NET` | RFC 6238 TOTP (replaced by about 40 lines in-house if the check fails) | MIT |
| npm `qrcode` (admin app only) | Draw the enrolment QR in the browser | MIT |

## Admin app and host wiring

- `apiBaseUrl` becomes empty (same origin). `ng serve` gets `proxy.conf.json` forwarding `/api` to `http://localhost:5051`; `Dockerfile.admin`'s nginx forwards `/api` to the API for local compose and staging.
- `Identity:UseRealAuthentication` (default false in Development and Testing, true everywhere else and not changeable there). When true in Development, the real cookie scheme replaces the development handler, so the sign-in flow can be run and tested locally and in Playwright.
- `core/auth`: session store (NgRx), HTTP interceptor (credentials, anti-forgery header, one silent refresh on 401, then redirect), `permissionGuard` for routes, `*hasPermission` for UI hiding.
- The existing e2e suite keeps running against the development handler; new `e2e/identity/` specs run with `UseRealAuthentication=true`.

## Order of work (feeds tasks.md)

1. Lookups, `Permissions` codes, migration, `IdentityModule`, architecture-test registration.
2. staff-accounts (hashing, CLI `create-owner`), then sessions (handler, cookies, throttle, CSRF), then mfa, then roles (handler, endpoint migration, matrix), then audit-trail.
3. Web: sign-in, MFA, change-password, guards, then staff, roles, audit pages.
4. Endpoint migration of Sprints 1-7 groups from `Staff/Admin/Inventory/Catalogue` to permissions (roles AC-9 to AC-12).
5. [Platform](../platform/README.md) in parallel from Wednesday; staging deploy on Friday.

## Risks

- **Endpoint migration touches about 60 route groups across seven modules.** Mitigation: the matrix is generated from the current host first (so behaviour is captured before changing it), then reviewed row by row against the roles spec table, then enforced.
- **Partitioned audit tables are not a native EF feature.** The migration uses raw SQL (all identifiers quoted); EF maps the parent table only. A migration test on a Testcontainers PostgreSQL proves the trigger, partitions and insert path.
- **Integration tests cannot run on this machine** (Windows Application Control); they run in the Linux container or CI, and results are reported as such.
- **Owner locked out** (lost device and codes): the `reset-owner` break-glass verb is the recovery path and is part of the staging runbook.
