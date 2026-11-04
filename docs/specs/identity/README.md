# Identity

Staff accounts, sign-in, multi-factor authentication, roles and the audit trail for the back office (Sprint 8). It replaces the development-only sign-in with real authentication, so the host's deployment gate ([ADR 0011](../../adr/0011-security-and-coupling-baseline.md)) lets a non-development environment start.

**Schema:** `IDNT` · **Code:** `src/api/src/Modules/Identity/` · **Web:** `features/identity/` · **Jira:** SCRUM-202; sources SCRUM-84 (FR-59), SCRUM-85 (FR-60), SCRUM-114 (NFR-10..16), SCRUM-121 (SEC-01), SCRUM-122 (SEC-02, staff part), SCRUM-124 (SEC-04), SCRUM-128 (SEC-08) · **Rules:** [constitution](../../CONSTITUTION.md) · **Module plan:** [plan.md](plan.md)

Customer accounts (registration, password reset, addresses, Child Profiles) are **not** this module's Sprint 8 scope; they arrive in S10 and may live in a separate `customers` module (`CUST` schema) sharing the session mechanics built here. Whether they do is decided then.

## Features

| Feature | What it does | Status |
|---|---|---|
| [staff-accounts](staff-accounts/spec.md) | Staff users, password hashing, owner bootstrap, admin-created accounts with a temporary password and forced change | Implemented |
| [sessions](sessions/spec.md) | Sign-in, short-lived access cookie, rotating refresh cookie, new session on every login, throttling, sign-out | Implemented |
| [mfa](mfa/spec.md) | TOTP enrolment, recovery codes, mandatory for every staff account | Implemented |
| [roles](roles/spec.md) | Permissions, the Owner role plus the four BRD roles, the policy-to-permission mapping, the endpoint authorization matrix | Implemented |
| [audit-trail](audit-trail/spec.md) | Real actor on every change; append-only audit log of every state change in every module | Implemented |

## Module decisions (owner, 2026-10-16)

| ID | Decision |
|---|---|
| D-1 | **No email or WhatsApp sender exists yet**, so there is no emailed invite. The first account (Osama, role Owner) is created by a one-time CLI/migration step, never by self-registration. Further staff get an admin-generated temporary password shared out-of-band, with a forced change at first sign-in. The emailed invite is built when a notifications feature exists. |
| D-2 | **TOTP authenticator app only.** No SMS codes. |
| D-3 | **Same-origin cookies** (HttpOnly, Secure, SameSite). The warehouse PWA (S14) lives in the same Angular/.NET system and stays same-origin; revisit only if a genuinely cross-origin client appears. |
| D-4 | **Roles use the four BRD names** (Catalogue, Order-ops, Support, Marketing) plus a superset **Owner**. Authorization stays capability/policy-based: roles and permissions are lookup data, and code checks permissions, never role strings. |
| D-5 | **Audit every state change in every existing module**, not only FR-59's named list (NFR-SC-05 has applied uniformly since Sprint 1). |
| D-6 | Platform work (deployment, observability) is specified under [`docs/specs/platform/`](../platform/README.md), not folded into this module and not a constitution amendment. |
| D-7 (new) | **Staff sign-in uses progressive throttling, not hard lockout** (SEC-02 behaviour; NFR-15). This does not settle FR-39 vs SEC-02 for customers (SCRUM-199 stays open); it is the safe choice for staff because MFA already blocks a guessed password. No CAPTCHA for staff (see the sessions spec, open question). |
