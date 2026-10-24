# SCRUM-202: Multi-factor authentication

- **Status:** Approved (owner, 2026-10-16)
- **Jira:** SCRUM-202; source SCRUM-121 (priority Highest)
- **BRD sections:** §11.1, §11.2
- **Requirement IDs:** SEC-01

## Problem & intent
An admin account can see every customer and Child Profile record, so a stolen password must not be enough. Every back-office account therefore needs a second factor, with a way back in when the phone is lost.

## Requirements (quoted from the BRD)
> **SEC-01:** Admin/back-office accounts shall require multi-factor authentication (MFA); this is non-negotiable given admin access exposes full customer and Child Profile data.

## User flow
1. After step 1 of sign-in ([sessions](../sessions/spec.md)), an account **without** MFA is taken to enrolment: the server shows a secret as a QR code and as text; the person adds it to an authenticator app and types the current code to confirm. Ten recovery codes are shown once.
2. An enrolled account enters the current 6-digit code (or one recovery code) at step 2.
3. A person can view how many recovery codes remain, and regenerate them (current code required).
4. An administrator with the staff-management permission can reset another person's MFA; the person re-enrols at next sign-in. The Owner who has lost both device and recovery codes uses the operator step on the server.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| AC-1 | Given an account without confirmed MFA, when it passes step 1, then it receives only a session that can enrol MFA; no other API call succeeds until enrolment is confirmed. | SEC-01 |
| AC-2 | Given enrolment, then the secret is generated server-side (160 bits), becomes active only after a valid code is entered, and is never returned again. | SEC-01 |
| AC-3 | Given an enrolled account, when a valid TOTP code (RFC 6238, SHA-1, 6 digits, 30 s step, one step of clock drift either side) is entered, then the session is issued. | SEC-01 |
| AC-4 | Given a code that was already accepted, when it is entered again within its validity window, then it is rejected (replay protection). | SEC-01 (new) |
| AC-5 | Given 5 wrong codes in a row for one pending sign-in, then the pending state is destroyed and the throttling of [sessions](../sessions/spec.md) AC-6 applies to the account. | NFR-15 (new) |
| AC-6 | Given ten recovery codes, then each works once, is stored only as a hash, and using one is recorded; regenerating codes invalidates all the old ones. | SEC-01 (new) |
| AC-7 | Given the stored secret, then it is encrypted at rest with a key that is not in the database and not in source; the secret, codes and recovery codes are never logged or placed in audit entries. | SEC-25 |
| AC-8 | Given a person with the staff-management permission resets another person's MFA, then that person's MFA, recovery codes and sessions are cleared, the action is audited, and they must re-enrol at next sign-in; nobody can reset their own MFA this way. | SEC-01 (new) |
| AC-9 | Given the operator break-glass step (staff-accounts AC-13) clears the Owner MFA, then sessions are revoked, the Owner re-enrols at next sign-in and receives new recovery codes once, and the action is audited as actor `operator-cli`. Non-Owner accounts are reset in the admin app (AC-8). | Owner decision 2026-10-16 |
| AC-10 | Given any active staff account (including the Owner), then there is no configuration that turns MFA off. | SEC-01 |

## Non-functional constraints
- Code entry shares the sign-in rate limits and throttling; the verify step has its own strict rate limit.
- Clock drift: the server uses NTP-synchronised time on staging (checked in the readiness probe notes of [observability](../../platform/observability/spec.md)).

## Out of scope
- SMS codes (D-2), WebAuthn/passkeys, push approvals, "remember this device", customer MFA.

## Open questions
- [x] ~~Recovery codes~~ Decided: shown once at MFA enrolment (AC-6); the Owner break-glass reset lives in staff-accounts AC-13. The encryption key (AC-7) is a deployment secret; the plan describes rotation.
