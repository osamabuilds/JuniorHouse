# SCRUM-202: Multi-factor authentication

- **Status:** Implemented (2026-10-26); see "As built" at the end
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

## As built (2026-10-26)

What differs from, or adds to, the text above. Everything else is built as written.

- **TOTP:** in-house `Totp`, `TotpVerifier` and `Base32` (about 120 lines, checked against the RFC 6238 test vectors); no `Otp.NET` package. The verifier sits behind plain static functions in the Domain, so a WebAuthn verifier can be added beside it later.
- **Encryption key (AC-7):** `Identity:Mfa:KeyId`, `Identity:Mfa:EncryptionKey` (base64, 32 bytes) and `Identity:Mfa:RecoveryPepper` (base64, at least 16 bytes) are required outside Development and Testing, validated at start-up (a Staging host test refuses to start without them). `Identity:Mfa:PreviousKeys` lists earlier keys by id for decryption during a rotation. In Development and Testing a well-known development key stands in so a developer's database keeps working across restarts. `AesGcmSecretProtector.NeedsRotation` says when a secret was sealed with an old key; the secret is re-sealed when enrolment is confirmed. A bulk "re-encrypt every secret" command from the plan is not built (no secret has ever been sealed with a retired key).
- **AC-2:** the secret is shown only in the response to `enrol/start`, as the `otpauth://` address and as grouped text. It is sealed with the owner's id as associated data, so a copy on another account's row does not open. An unconfirmed enrolment lives 15 minutes in `MFA_ENRL_PEND`, one per account; starting again replaces it.
- **AC-4, AC-6, concurrent use:** the "last accepted step" and each recovery code's "used" date are written before anything else changes in the verify step, guarded by the PostgreSQL `xmin` token, so of several requests with one code exactly one is accepted. The code that confirms enrolment counts as used (a later sign-in cannot replay it).
- **AC-5:** five wrong codes end the pending sign-in; failures are recorded as `MfaBad` attempts, so the sessions throttle applies, and a delay that is running answers 429 without counting as a failure. The pending sign-in lasts 5 minutes.
- **Answers:** a wrong, replayed or malformed code gets one answer (401 "That code is not valid"); an unknown, expired, spent or ended pending sign-in gets 401 "Sign in again" and clears the pending cookie. Enrolment mistakes are 409 with the reason (the person is signed in, so they may be told).
- **AC-1:** `enrol/start` and `enrol/confirm` need scope `EnrolMfa` and the anti-forgery header; `recovery-codes` needs the full scope. An account that must still change its password cannot start enrolment. A general rule that refuses every other endpoint to a limited session belongs to the roles feature.
- **AC-8:** `POST /api/identity/staff/{id}/reset-mfa` needs `staff.manage`, refuses the caller's own account, clears the second factor, the recovery codes and every session of the target (revoke reason `MfaReset`), and is repeatable. It does not yet stop a non-Owner administrator resetting an Owner's second factor; that is a roles decision.
- **AC-9:** the operator `reset-owner --mfa` step uses the same clearing code; the Owner then enrols again and receives new recovery codes once.
- **AC-10:** `MfaPolicyTests` scan every settings class of the host and Identity, `appsettings*.json` and `docker-compose.yml` for anything that reads like an MFA on/off switch.
- **Security events:** 1002 `mfa_bad_code` and `mfa_pending_ended` (typed values only). Recovery-code use is recorded by the code's own `USED_DTE` and by the `MfaOk` attempt row.
- **Not built:** recovery-code use does not raise its own security event; enrolment failures are limited by the strict rate limit only (no five-try cut-off for enrolment).
- **Admin app:** `features/identity/mfa`: enrolment page (the `/setup-required` route; QR drawn in the browser with the `qrcode` package, which is a new admin dependency, MIT), recovery-code panel, the second-step challenge on the sign-in page, and the security page (`/security`, linked from the header) with the status card and new recovery codes. The secret and the codes live in memory only and are dropped once shown.

## Traceability

| AC | Proof |
|---|---|
| AC-1 | `MfaEnrolmentTests.UnenrolledUser_CanReachOnlyEnrolment`, `MfaEndpointsTests.TheScopeRules_KeepEachStepToItsOwnSession`, `MfaEndpointsTests.WithoutASession_EveryEnrolmentRouteReturns401_AndUnsafeOnesNeedTheHeader` |
| AC-2 | `TotpSecretTests`, `MfaEnrolmentTests.SecretIsInactiveUntilConfirmed`, `StartingAgain_ReplacesTheUnconfirmedSecret`, `AnExpiredEnrolment_CannotBeConfirmed`, `MfaMigrationTests` |
| AC-3 | `TotpVerifierTests.MatchesRfcVectors`, `AllowsOneStepOfDrift`, `MfaVerifyTests.AValidCode_IssuesAFullSession_AndTheStepIsRecorded`, `MfaEndpointsTests.FullEnrolmentAndSignIn` |
| AC-4 | `TotpVerifierTests.ReusedCode_IsRejected`, `MfaVerifyTests.ACodeThatWasAlreadyAccepted_IsRejected`, `ParallelSameCode_OnlyOneSucceeds`, `e2e/identity/mfa.e2e.spec.ts` |
| AC-5 | `MfaVerifyTests.FiveFailures_EndThePendingSignIn`, `TheFailures_FeedTheThrottleOfTheSessionsFeature` |
| AC-6 | `RecoveryCodeTests.*`, `MfaEnrolmentTests.RecoveryCodes_AreReturnedOnce`, `Regenerate_InvalidatesOldCodes_AndNeedsACurrentCode`, `MfaVerifyTests.ARecoveryCode_WorksOnce`, `ParallelSameRecoveryCode_OnlyOneSucceeds`, `MfaEndpointsTests.ARecoveryCode_SignsIn_Once` |
| AC-7 | `SecretProtectorTests.*`, `MfaSecretLeakTests.NothingSensitive_AppearsInOutput`, `SecurityGateTests.Staging_RefusesToStart_WithoutTheMfaKeys`, `mfa.effects.spec.ts` (nothing in storage) |
| AC-8 | `ResetStaffMfaTests.*`, `MfaEndpointsTests.ResetMfa_NeedsThePermission_AndForcesReEnrolment` |
| AC-9 | `MfaOwnerResetTests.OwnerBreakGlassReset_ForcesReEnrolment_AndNewRecoveryCodesAreShownOnce`, `OwnerCommandsTests.ResetOwner_ClearsPasswordAndMfa_AndAudits` |
| AC-10 | `MfaPolicyTests.*` |
