# SCRUM-202: Multi-factor authentication, technical plan

- **Spec:** [spec.md](spec.md) · **Module plan:** [../plan.md](../plan.md)

## Domain model
- MFA state lives on `StaffUser` (secret ciphertext, key id, enrolled date, last accepted step) plus recovery codes (`RecoveryCode`: hash, used date).
- `TotpVerifier` (domain service, pure): given secret, time and last accepted step, returns accepted step or failure; window of one step either side; rejects any step less than or equal to the last accepted one (replay, AC-4).
- `SecretProtector` port: AES-GCM with a configured key and key id; nonce stored with ciphertext.
- Recovery codes: 10 codes of 10 characters from an unambiguous alphabet, shown once, stored as SHA-256 of the code with a per-user pepper from configuration (codes are random, so a fast hash is adequate).

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `StartMfaEnrolmentCommand` | session (scope EnrolMfa or Full without MFA) | otpauth URI and base32 text, held as an unconfirmed secret | AC-1, AC-2 |
| `ConfirmMfaEnrolmentCommand` | code | recovery codes (once); MFA active | AC-2, AC-6 |
| `RegenerateRecoveryCodesCommand` | current code | new codes (once); old invalid | AC-6 |
| `VerifyMfaCommand` (in sessions) | pending token, code or recovery code | session | AC-3, AC-4, AC-5 |
| `ResetStaffMfaCommand` | user id (needs `staff.manage`; not self) | none | AC-8 |
| `ResetOwnerCommand` (host verb `reset-owner`) | flags: password, MFA | none; Owner accounts only | AC-9, staff-accounts AC-13 |
| `GetMfaStatusQuery` | session | enrolled, codes remaining | AC-6 |

## API endpoints
`/api/auth/mfa/enrol/start`, `/enrol/confirm`, `/recovery-codes`, `/api/auth/mfa/verify`; `/api/identity/staff/{id}/reset-mfa` (`staff.manage`).

## Events
None (audited generically).

## Data
Columns on `USR_MAIN` and table `USR_RCVR_CODE` (module plan). An unconfirmed secret is kept in `LGIN_PEND`-like transient storage (a `MFA_ENRL_PEND` row with expiry 15 minutes) so a half-finished enrolment never leaves an active secret behind.

## NFR / security design
- Key management: `Identity:Mfa:EncryptionKey` (32 bytes, base64) and `KeyId` come from the deployment secret store; start-up refuses a missing key outside Development; a second key may be configured for decryption during rotation, and a rotation command re-encrypts all secrets.
- Sign-in with MFA shares the throttle and rate limits; 5 wrong codes destroy the pending sign-in.
- Clock: staging time sync (chrony or systemd-timesyncd) is part of the server bootstrap; readiness notes the check.

## Frontend
`features/identity/mfa/`: enrolment page (QR drawn in the browser from the otpauth URI with the `qrcode` package, secret also as grouped text for manual entry, code field with `autocomplete="one-time-code"` and `inputmode="numeric"`), recovery-code display with a "I saved these" confirmation, challenge page (code or recovery code toggle), a status card in the account menu. Accessible: the QR has a text alternative (the secret), focus management on step changes.

## Risks & alternatives considered
- TOTP replay window with two API nodes: the last-step check is stored in the database, so it holds across nodes.
- Phishable factor: TOTP is accepted per the owner's decision; WebAuthn is a later option and the verifier is behind an interface so it can be added.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | Enrolment needs a session of the right scope; reset needs `staff.manage` and a different target user; operator verb needs server access. |
| Input validation | Codes are exactly 6 digits or 10 recovery characters, checked before any work; bodies under 1 KB. |
| Output encoding | otpauth URI built with URL-encoded label and issuer. |
| Outbound calls | None; QR is drawn locally (no third-party QR service sees the secret). |
| Resource limits | Strict rate limit on verify and enrol; 5-failure cut-off; recovery codes capped at 10. |
| Concurrency | Last-step update is a conditional write so concurrent verifications cannot both succeed with one code. |
| Idempotency | Confirm enrolment and regeneration are safe to repeat (second call fails cleanly); reset is idempotent. |
| Audit | Enrolment, regeneration, resets, recovery-code use are audited and raise security events (names only, no codes). |
| Data protection | Secret encrypted at rest; never returned after enrolment; never logged; recovery codes hashed. |
| Secrets and dangerous config | Encryption key and pepper from secrets; refusal to start in production without them; no "MFA off" option exists. |
| Error responses | Generic "code not valid"; no hint whether it was expired or replayed. |
| New dependencies | `Otp.NET` (MIT) or in-house; `qrcode` (admin app, MIT). |
| Coupling | Inside Identity only. |
| Abuse cases | (1) Attacker with the password guesses codes: 5 tries then throttle, one-step window. (2) Admin account takeover resets someone's MFA: needs `staff.manage` (itself MFA-protected), audited, user is told at next sign-in that MFA was reset. (3) Database leak: secrets are encrypted with a key not in the database. (4) Recovery code theft: single use, hashed, regenerable. |
