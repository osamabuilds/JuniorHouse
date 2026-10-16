# SCRUM-202: Staff accounts

- **Status:** Approved (owner, 2026-10-16)
- **Jira:** SCRUM-202; sources SCRUM-114, SCRUM-85
- **BRD sections:** §8.4, §7.8
- **Requirement IDs:** NFR-12, NFR-13, NFR-14, FR-60 (accounts side), SEC-25

## Problem & intent
Until now every request runs as an anonymous development user. The back office needs real named accounts so that every action can be attributed (FR-59) and access can be limited (FR-60). There is no email or WhatsApp sender in the system yet, so accounts cannot be created by an emailed invite; the first account is created by an operator step on the server and later accounts by an administrator who hands over a temporary password.

## Requirements (quoted from the BRD)
> **NFR-12:** Passwords shall be stored using a salted, industry-standard hash (bcrypt/argon2).

> **NFR-13:** All user input (search, forms, Fit Finder fields) shall be validated and sanitised server-side against injection attacks, independent of client-side validation.

> **NFR-14:** Admin/back-office access shall be role-based, following least privilege, per FR-60.

> **SEC-25:** Application logs shall explicitly exclude passwords, full card data, and full Child Profile records; a documented logging policy shall define what is safe to log versus what must be redacted/masked.

## User flow
1. **Bootstrap (one time):** an operator with server access runs the owner-creation step (login name, display name, password from a secret or prompt, never from a command-line argument). It refuses to run if an Owner already exists. The Owner signs in, must change the password and enrol MFA ([mfa](../mfa/spec.md)). The same operator command offers a **break-glass reset** of the Owner password and MFA (AC-13).
2. **Add staff:** an administrator with the staff-management permission creates an account (user name, display name, roles). The system generates a temporary password and shows it once; the administrator passes it on out-of-band (for example WhatsApp). The account is flagged "must change password".
3. **First sign-in:** the new person signs in with the temporary password and can do nothing except set a new password and enrol MFA.
4. **Day to day:** a person changes their own password (current password required). An administrator can reset a password (a new temporary password, same rules), deactivate an account, reactivate it, and change roles ([roles](../roles/spec.md)).
5. Accounts are never deleted: deactivation keeps the history that refers to them. `(new)`

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| AC-1 | Given no Owner exists, when the operator runs the owner-creation step with valid input, then an active Owner account exists; given an Owner already exists, then the step refuses and changes nothing. | D-1 |
| AC-2 | Given a password shorter than 12 or longer than 128 characters, equal to the user name, or on the embedded common-password list, when it is set, then it is rejected with a problem-details error that says why. | NFR-13 (new) |
| AC-3 | Given any account, then only a salted Argon2id hash is stored; the plain password, the hash, and the temporary password never appear in any response (other than the temporary password once at creation/reset), log line, audit entry or event. | NFR-12, SEC-25 |
| AC-4 | Given an administrator creates an account, then the temporary password is server-generated (at least 16 random characters), returned once, **single-use** (it allows exactly one successful sign-in; the person must then set a new password in that session, and if the session is lost an administrator regenerates a new one), and the account is "must change password" and "must enrol MFA". | D-1, owner 2026-10-16 |
| AC-5 | Given an account is "must change password" or has no MFA, when it signs in, then every API call except change-password, MFA enrolment (forced at that first sign-in, in that order), sign-out and "who am I" returns 403 with a "password change required" or "MFA enrolment required" problem code. | D-1 |
| AC-6 | Given a temporary password is older than 48 hours, when it is used, then sign-in fails with the generic error and an administrator must regenerate it (as many times as needed). | D-1, owner 2026-10-16 |
| AC-7 | Given a user changes their own password, then the current password is required, the new one passes AC-2, it cannot equal the last 3 passwords, and all their other sessions are revoked. | (new) |
| AC-8 | Given an administrator deactivates an account, then every session of that account is revoked immediately, it cannot sign in, and the next request of any open session gets 401. | SEC-08 |
| AC-9 | Given the account is the last active Owner, then deactivating it, or removing its Owner role, is refused. | (new) |
| AC-10 | Given a user name, then it is 3 to 40 characters of `a-z 0-9 . _ -`, unique case-insensitively, and cannot be changed after creation (audit columns refer to it). | NFR-13 (new) |
| AC-11 | Given a caller without the staff-management permission, then every staff-management endpoint returns 403, and an Owner is not required (any role holding the permission works). | FR-60, SEC-08 |
| AC-12 | Given the owner-creation step or any account change, then it is attributed in the audit trail (the operator step as actor `operator-cli`). | FR-59 |
| AC-13 | **Break-glass.** Given server access, when the operator runs the Owner reset step, then it can reset the Owner's password (a new password supplied through the environment or a prompt, flagged "must change password") and/or clear the Owner's MFA (re-enrolment forced at next sign-in, new recovery codes shown once), revokes all the Owner's sessions, refuses any account that is not an Owner, and records the action in the audit trail with actor `operator-cli`. | Owner decision 2026-10-16 |

## Non-functional constraints
- Hash work factor tuned so one hash takes about 250 ms on the staging VPS; configurable.
- Staff-management endpoints use the strict rate-limit policy.
- Password and secret values are never placed in exception messages (SEC-25).

## Out of scope
- Customer accounts, registration, "forgot password" email links (S10; they need the email sender).
- The emailed staff invite (built when a notifications feature exists, then replaces the temporary password flow).
- Single sign-on, password expiry policies, hardware keys.

## Open questions
- [x] ~~Owner bootstrap inputs~~ Decided: login name `osama`, display name "Osama Shafique".
- [x] ~~Temporary password lifetime~~ Decided: 48 hours, single-use, regenerable by an administrator (AC-4, AC-6).
- [ ] Source of the common-password list (a small embedded top-10,000 list is assumed; licence to be checked at implementation).
