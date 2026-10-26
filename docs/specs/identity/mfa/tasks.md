# SCRUM-202: Multi-factor authentication, tasks

Module steps are in [../tasks.md](../tasks.md). Depends on sessions steps 1 to 6.

- [x] 1. Package check (licence, vulnerabilities) for `Otp.NET`; if it fails, the in-house TOTP. Pin in `Directory.Packages.props`. **Decision (2026-10-26): in-house `Totp`, `TotpVerifier` and `Base32` in Identity.Domain (about 120 lines, verified against the RFC 6238 vectors); no new NuGet package. Revisit only if a package is wanted for WebAuthn later.** Test: n/a.
- [x] 2. `TotpVerifier`: RFC 6238 vectors, one step of drift, rejects any step not newer than the last accepted. Test: `TotpVerifierTests.MatchesRfcVectors`, `TotpVerifierTests.AllowsOneStepOfDrift`, `TotpVerifierTests.ReusedCode_IsRejected` (AC-3, AC-4).
- [x] 3. `SecretProtector` (AES-GCM, key id, rotation with a second decryption key). Test: `SecretProtectorTests.RoundTrips_AndRejectsTampering`, `SecretProtectorTests.OldKeyId_StillDecrypts` (AC-7).
- [x] 4. Recovery codes: 10 codes, hashed with a pepper, single use, regenerate invalidates. Test: `RecoveryCodeTests.CodesWorkOnce_AndAreStoredHashed`, `RecoveryCodeTests.Regenerate_InvalidatesOldCodes` (AC-6).
- [x] 5. EF configuration and migration `AddMfa` (columns on `USR_MAIN`, `USR_RCVR_CODE`, pending-enrolment row). Test: `NamingConventionTests`, `MfaMigrationTests.Applies_OnACleanDatabase`.
- [x] 6. Enrolment commands: start (unconfirmed secret, 15 minutes), confirm (valid code, then recovery codes shown once). Test: `MfaEnrolmentTests.SecretIsInactiveUntilConfirmed`, `MfaEnrolmentTests.RecoveryCodes_AreReturnedOnce`, `MfaEnrolmentTests.UnenrolledUser_CanReachOnlyEnrolment` (AC-1, AC-2).
- [ ] 7. `VerifyMfaCommand`: TOTP or recovery code, 5 failures destroy the pending sign-in and feed the throttle, last-step update is a conditional write. Test: `MfaVerifyTests.FiveFailures_EndThePendingSignIn`, `MfaVerifyTests.ParallelSameCode_OnlyOneSucceeds` (Testcontainers, AC-4, AC-5).
- [ ] 8. `ResetStaffMfa` by an administrator (not self), clears MFA and sessions. Test: `ResetStaffMfaTests.NeedsPermission_AndCannotTargetSelf`, `ResetStaffMfaTests.ForcesReEnrolment` (AC-8).
- [ ] 9. Owner break-glass MFA reset (covered by `OwnerCommandsTests` in staff-accounts step 10) shows new recovery codes once at the next enrolment. Test: `OwnerCommandsTests.ResetOwner_ClearsPasswordAndMfa_AndAudits` (AC-9).
- [ ] 10. No switch turns MFA off. Test: `MfaPolicyTests.NoConfigurationKey_DisablesMfa` (scans options for such a key, AC-10).
- [ ] 11. Secrets never logged: secret, codes and recovery codes through the logging paths. Test: `MfaSecretLeakTests.NothingSensitive_AppearsInOutput` (AC-7).
- [ ] 12. Endpoints `enrol/start`, `enrol/confirm`, `recovery-codes`, `mfa/verify`, `reset-mfa`. Test: `MfaEndpointsTests.FullEnrolmentAndSignIn`, `MfaEndpointsTests.StrictRateLimitApplies`.
- [ ] 13. Web `features/identity/mfa`: enrolment page (QR drawn in the browser with `qrcode`, secret as text, `autocomplete="one-time-code"`), recovery-code panel, challenge page, status card. Test: `mfa-enrol.component.spec.ts`, `mfa-challenge.component.spec.ts`, `recovery-codes.component.spec.ts`.
- [ ] 14. e2e with real authentication: first sign-in forces password change then enrolment, sign-out, sign-in with TOTP (a test helper computes the code), sign-in with a recovery code, a reused code is refused. Test: `e2e/identity/mfa.e2e.spec.ts`.
- [ ] 15. Update spec.md to **Implemented**, note deviations, tick traceability rows.
