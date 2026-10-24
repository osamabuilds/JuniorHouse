# SCRUM-202: Staff accounts, technical plan

- **Spec:** [spec.md](spec.md) · **Module plan:** [../plan.md](../plan.md)
- **Module:** Identity (`IDNT`)

## Domain model
- `StaffUser` aggregate: user name (value object, validated `[a-z0-9._-]{3,40}`), display name, password hash, flags (`Active`, `MustChangePassword`), temporary-password expiry, roles (set of role ids). Invariants: user name immutable; at least one active Owner always exists (checked inside the same transaction with a conditional update, not check-then-act); a password change records the old hash in `USR_PWD_HIST` (last 3 kept).
- `PasswordPolicy` (domain service, pure): length 12 to 128, not equal to user name, not on the common list.
- Ports: `IPasswordHasher` (Argon2id, parameters from options), `ITemporaryPasswordGenerator` (cryptographic RNG, at least 16 characters, no look-alike characters), `ICommonPasswords`.

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `CreateOwnerCommand` (operator verb only) | user name, display name, password (from env or prompt) | account | AC-1, AC-12 |
| `ResetOwnerCommand` (operator verb `reset-owner`) | password and/or MFA flags | none; refuses non-Owner accounts; revokes sessions | AC-13 |
| `CreateStaffCommand` | user name, display name, role ids | account + temporary password (once) | AC-4, AC-10, AC-11 |
| `ChangeOwnPasswordCommand` | current, new | none | AC-5, AC-7, AC-2 |
| `ResetPasswordCommand` | user id | temporary password (once) | AC-4, AC-8 |
| `DeactivateStaffCommand`, `ReactivateStaffCommand` | user id | none | AC-8, AC-9 |
| `ListStaffQuery` (paged 1..100), `GetStaffQuery` | filters | page / detail DTO (no hash, no secret) | AC-3, AC-11 |

Deactivate and reset call the session feature's `RevokeUserSessionsCommand` through an application-level port inside Identity (same module).

## API endpoints
See the module plan table (`/api/identity/staff...`, `/api/auth/password`). Auth: `staff.manage`; change-own-password needs a session whose scope allows it.

## Events
None published to other modules. All changes are audited by the generic mechanism ([audit-trail](../audit-trail/plan.md)).

## Data
`USR_MAIN`, `USR_PWD_HIST`, `USR_ROLE_MAP` (module plan). Migration seeds nothing here; the owner comes from the operator verb. A partial unique expression index keeps user names unique case-insensitively (names are stored lower-case and a check constraint enforces it).

## NFR / security design
- Hash parameters in options with a start-up validator (rejects weak production values); a timing test asserts about 250 ms on the CI runner within a wide band.
- Passwords are `SecretString` values (a type with redacting `ToString`, no serialisation) so logging them by mistake prints nothing.
- Temporary password shown once in the create/reset response over HTTPS with `Cache-Control: no-store`.

## Frontend
`features/identity/staff-accounts/`: containers (list, detail/edit, create dialog), components (roles picker, one-time password display with copy button and warning), NgRx store, service. Semantic HTML, labelled forms, keyboard operable, WCAG 2.1 AA. Page title and a single `h1` per route.

## Risks & alternatives considered
- The operator verb takes the password from the environment, which anyone who can read the process environment on the server could see. Mitigation: the runbook sets it for one command only and recommends a long random value. The Owner account is also flagged "must change password" (a `(new)` addition to AC-4's rule), so the Owner replaces it at the first sign-in.
- Alternative of seeding the Owner in a migration: rejected, a migration cannot carry a secret safely and would run in every environment including tests.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | Staff endpoints need `staff.manage`; own-password endpoint needs a session; operator verbs need server access. Checked per request. |
| Input validation | Length, charset and policy checks in domain/FluentValidation style validators; request DTOs separate from entities; no role ids accepted that do not exist. |
| Output encoding | Names are rendered by Angular's escaping; no raw SQL (EF only). |
| Outbound calls | None. |
| Resource limits | Page size 1..100; request bodies under 8 KB; strict rate limit on create/reset. |
| Concurrency | `xmin` token on `USR_MAIN`; last-Owner rule enforced by a conditional update in one transaction; unique index on user name. |
| Idempotency | Create fails with 409 on duplicate; deactivate/reactivate are idempotent; reset always yields a new password (not retried silently by the client). |
| Audit | All changes audited generically; operator verbs record `operator-cli`; actor names are charset-restricted. |
| Data protection | No password, hash or temporary password in logs, events or audit; staff names are not child data. |
| Secrets and dangerous config | Owner password via env/prompt only; hash parameters validated at start; no default accounts in any environment. |
| Error responses | Problem details; duplicates and policy failures give specific messages only to `staff.manage` callers. |
| New dependencies | Argon2 package (MIT), common-password list data file (public list, licence checked). |
| Coupling | No other module touches Identity; session revocation is an in-module port. |
| Abuse cases | (1) A phished administrator creates accounts: creation is audited and MFA gates the admin session. (2) Someone guesses a temporary password: it is random, expires in 48 hours and is single-use, sign-in is throttled. (3) The last Owner is deactivated by mistake or malice: refused by the invariant. (4) Enumeration of user names through create/duplicate responses: only `staff.manage` holders see them. |
