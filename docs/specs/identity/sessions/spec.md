# SCRUM-202: Sessions and sign-in

- **Status:** Implemented (2026-10-26), except step 2 of sign-in (`POST /api/auth/mfa/verify`), which is a shell until the [mfa](../mfa/spec.md) feature lands; see "As built" at the end
- **Jira:** SCRUM-202; sources SCRUM-124, SCRUM-122 (staff part), SCRUM-128, SCRUM-114
- **BRD sections:** §11.2, §8.4
- **Requirement IDs:** SEC-03, SEC-04, SEC-08, NFR-10, NFR-15, SEC-02 (behaviour reused for staff)

## Problem & intent
Staff must prove who they are, stay signed in safely, and be signed out the moment their access is withdrawn. The browser must never hold a credential that a script on the page can read, and a stolen cookie must be short-lived and detectable.

## Requirements (quoted from the BRD)
> **SEC-03:** Session cookies/tokens shall be HttpOnly, Secure, and SameSite=Strict (or Lax where cross-site flows require it), with short-lived access tokens and rotating refresh tokens.

> **SEC-04:** A new session identifier shall be issued on every successful login (session-fixation prevention).

> **SEC-08:** Admin API endpoints shall re-validate the caller's role on every request server-side; role-based UI hiding alone is not sufficient authorization.

> **NFR-10:** All traffic shall be served over HTTPS/TLS.

> **NFR-15:** Login attempts shall be logged and rate-limited to mitigate brute-force/credential-stuffing.

> **SEC-02:** Failed-login handling shall use progressive throttling and CAPTCHA (e.g. after 3 failed attempts) rather than a hard account lockout, to prevent an attacker from denial-of-service-locking a legitimate customer's account by deliberately failing their login.

## User flow
1. The person opens the admin app and is sent to the sign-in page (any protected route redirects there).
2. **Step 1:** user name and password. On success the server issues a short-lived "MFA pending" cookie that grants nothing else.
3. **Step 2:** the TOTP code (or a recovery code), see [mfa](../mfa/spec.md). On success the server issues the session: an access cookie (short-lived) and a refresh cookie (rotating, single use).
4. While the app is open the browser transparently refreshes the pair before the access cookie expires. An idle or expired session returns the person to sign-in.
5. **Sign out** revokes the session server-side. "Sign out everywhere" revokes all of the person's sessions.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| AC-1 | Given a successful sign-in, then the access and refresh cookies are set `HttpOnly`, `Secure` (HTTP is allowed only in Development/Testing), `SameSite=Strict`, with opaque random values of at least 256 bits; the server stores only their hashes. | SEC-03 |
| AC-2 | Given the access cookie, then it lives 15 minutes (configurable); given a valid refresh cookie, when refresh is called, then a new access and refresh pair is issued and the old refresh value stops working. | SEC-03 |
| AC-3 | Given a refresh value that was already rotated is presented again, then the whole session is revoked and a security event is logged. | SEC-03 (new) |
| AC-4 | Given any successful sign-in, then a new session id is issued; a session cookie sent with the sign-in request is revoked and never reused. | SEC-04 |
| AC-5 | Given a session, then it expires after 30 minutes without refresh (idle) and 12 hours after sign-in (absolute), both configurable. | SEC-03 (new) |
| AC-6 | Given failed attempts for one account/IP pair, then from the 3rd consecutive failure each further attempt is delayed (30 s, doubling, capped at 15 min) and rejected with 429 and `Retry-After` while the delay runs; a success resets the count; the account is never locked. | SEC-02, NFR-15, D-7 |
| AC-7 | Given a wrong user name, a wrong password, an inactive account or an expired temporary password, then the response and its timing are indistinguishable (generic "details do not match"; a dummy hash is computed for unknown names). | NFR-15 (new) |
| AC-8 | Given any sign-in attempt, then one row is recorded: user name as typed (truncated to 40 characters, control characters removed), client IP, outcome code, time. The password is never recorded. | NFR-15, SEC-25 |
| AC-9 | Given a protected request, then the server loads the session and the user on that request and rejects it (401) if the session is missing, revoked, expired or the user is inactive; permissions are evaluated on the same request ([roles](../roles/spec.md) AC-5). | SEC-08 |
| AC-10 | Given an unsafe request (POST, PUT, PATCH, DELETE) without the anti-forgery header, or with a wrong value, then it is rejected with 403. | SEC-03 (new) |
| AC-11 | Given sign-out, then the session is revoked in the database and both cookies are cleared; the old cookie values fail afterwards. | SEC-03 |
| AC-12 | Given the auth endpoints, then they are rate limited per client IP (strict policy) in addition to AC-6. | NFR-15 (new) |
| AC-13 | Given the admin app, then no credential or token is stored in `localStorage`, `sessionStorage` or readable cookies; a 401 sends the person to sign-in; the app shows who is signed in and a sign-out action. | SEC-03 |
| AC-14 | Given a non-development environment, then the development sign-in handler is absent, the cookie scheme is registered, and the host starts (the ADR 0011 gate is satisfied); given the scheme is missing, then the host still refuses to start. | ADR 0011 |
| AC-15 | Given the API runs behind Cloudflare and a reverse proxy, then the client IP used for AC-6 and AC-8 is the real visitor IP, taken from forwarded headers only when they come from the trusted proxy. | NFR-15 (new) |

## Non-functional constraints
- NFR-10: the staging site serves HTTPS only; plain HTTP redirects. `Strict-Transport-Security` is sent from staging.
- Sign-in p95 under 500 ms excluding the intentional hash cost; refresh p95 under 100 ms.
- Failed sign-ins are logged as security events, distinct from routine errors (groundwork for SEC-26 in S18).

## Out of scope
- Customer sign-in (S10) and its lockout policy (FR-39 vs SEC-02, SCRUM-199 stays open).
- Cross-origin clients and bearer tokens (D-3).
- "Remember this device".

## Open questions
- [x] ~~CAPTCHA for staff~~ Decided: none. Staff sign-in uses throttling per account and per IP without hard lockout; the customer lockout vs CAPTCHA decision (SCRUM-199) stays open for the customer-auth sprint.
- [x] ~~Session lifetimes~~ Decided: 30 minutes idle and 12 hours absolute, configurable.

## As built (2026-10-26)

What differs from, or adds to, the text above. Everything else is built as written.

- **AC-2, AC-5:** the three lifetimes are configurable as `Identity:Sessions:AccessMinutes`, `IdleMinutes` and `AbsoluteMinutes` (defaults 15, 30, 720). They are validated at start-up (access at most idle, idle at most absolute, absolute at most one week).
- **AC-3, concurrent refresh:** the previous refresh value is refused with **409** and no new cookies, and the session survives, for 10 seconds after a rotation (`Session.RefreshGrace`). A second request that carried the same cookie therefore retries once with the pair the first one set. It does not receive the same new pair: only hashes are stored, so the raw values cannot be handed out again. From 10 seconds on the previous value revokes the session. The conditional update is the PostgreSQL `xmin` concurrency token on `SESS_MAIN`; the new column is `ROTD_DTE`.
- **AC-8:** the name is also lower-cased before it is stored, so "Admin" and "ADMIN" share one throttle.
- **AC-6, open:** the global per-name ceiling (10 failures from any address in 15 minutes) can delay a real user while someone is guessing their name. This is the plan's accepted trade-off; it is not a lockout. Revisit with the owner if it bites.
- **AC-10:** the anti-forgery header is required on unsafe requests that carry a session. Sign-in, refresh and sign-out have no session value to forge against and are guarded by `SameSite=Strict` plus an `Origin` check instead. The check compares `Origin` with `Site:PublicBaseUrl` (or, when that is unset, the request's own origin); a request with no `Origin` header is allowed, because browsers always send one on a cross-site unsafe request. Only one site origin is accepted, so an admin app served from a different host than `Site:PublicBaseUrl` would be refused (open question for the deployment plan).
- **AC-12:** the auth routes use the existing strict rate-limit policy (`RateLimit:StrictPerMinute`, default 60 a minute per address). A dedicated lower value for `/api/auth` is a small change if wanted.
- **AC-14:** the gate is stricter than written: outside Development/Testing the host starts only when the `RompSession` scheme is registered and is the default scheme (another scheme no longer opens it), and it refuses a missing `Identity:Csrf:Key`, `Identity:AllowInsecureCookies=true`, `Identity:UseRealAuthentication=false` and invalid session lifetimes.
- **AC-15:** `Network:TrustedProxies` lists the proxies whose forwarded address is believed; with none configured forwarding is switched off (the framework would otherwise trust every peer). `Network:ClientIpHeader` is `X-Forwarded-For` (default) or `CF-Connecting-IP` when Cloudflare's header is passed through. Production proxy ranges must be set at deploy time.
- **Security events (observability AC-9):** `SecurityEvents` in `Romp.BuildingBlocks.Application`, category `Romp.Security`: 1001 sign-in failed (outcome `bad_credentials`, `inactive` or `temporary_expired`), 1003 refresh reuse, 1004 sign-in throttled. The typed user name is not logged (people type passwords into that box); the user id and client address are. 1002, 1005, 1006 and 1007 belong to the mfa, roles and observability work.
- **Housekeeping:** `PurgeExpiredSessionsCommand` removes sessions whose absolute limit passed more than 90 days ago, attempts older than 90 days and unfinished sign-ins, once a day from a hosted service.
- **Endpoints added:** `GET /api/auth/me` (also answers a stand-in user under the development handler, so the admin app runs without sign-in in that mode), `POST /api/auth/sign-out-all`. `POST /api/auth/mfa/verify` answers 501 until mfa is built.
- **Limited sessions:** an account that must change its password or enrol MFA gets a session the moment the password is accepted, and the admin app keeps it on `/change-password` or `/setup-required`. Until mfa lands nobody can reach the full scope. The server-side rule that refuses every other endpoint to a limited scope belongs to the roles feature's permission handler (not built yet).
- **Admin app (AC-13):** same-origin API (`proxy.conf.json` for `ng serve`, an nginx `/api` proxy in the container, `API_BASE_URL` empty), `core/auth` (store, interceptor, guard), pages `sign-in`, `change-password` and `setup-required`; the staff header, navigation and footer are not shown to anyone who is not signed in. `permissionGuard` and `*hasPermission` (Sprint 8 G10) wait for the roles feature.

## Traceability

| AC | Proof |
|---|---|
| AC-1 | `SessionTokenTests`, `CookieWriterTests.SetsEveryRequiredFlag`, `AuthEndpointsTests.Cookies_AreSecureByDefault`, `e2e/identity/sessions.e2e.spec.ts` (HttpOnly, SameSite) |
| AC-2 | `SessionTests.Rotate_ReplacesBothTokens`, `RefreshSessionTests.*`, `SessionLifetimeOptionsTests`, `AuthEndpointsTests.FullFlow_SetsRotatesAndClearsCookies`, auth interceptor spec, e2e silent refresh |
| AC-3 | `SessionTests.ReusedRefresh_RevokesTheSession`, `RefreshSessionTests.OldValueAfterGrace_RevokesTheSession`, `RefreshSessionTests.TwoParallelRefreshes_OneWins_NoRevocation`, `SessionSecurityEventTests` |
| AC-4 | `NewSessionOnLoginTests.PresentedSession_IsRevoked_AndNeverReused`, `safe-return-url.spec.ts` |
| AC-5 | `SessionTests.Expires_IdleAndAbsolute`, `SessionAuthenticationHandlerTests.RejectsEveryUnusableSession` |
| AC-6 | `ThrottlePolicyTests`, `SignInHandlerTests.Throttles_PerPairAndPerName`, `AuthEndpointsTests.RepeatedFailures_AreThrottled_WithRetryAfterOnly`, e2e throttling message |
| AC-7 | `SignInHandlerTests.AllFailureKinds_LookIdentical`, `SignInHandlerTests.UnknownUser_StillPaysTheHashCost`, `sign-in.component.spec.ts` |
| AC-8 | `SignInHandlerTests.LogsEveryAttempt_WithoutThePassword`, `SessionSecurityEventTests` |
| AC-9 | `SessionAuthenticationHandlerTests.*`, `SessionRevocationTests.Deactivating_AnAccount_EndsItsSessionsAtOnce`, `authGuard` spec |
| AC-10 | `CsrfFilterTests.*`, `AuthEndpointsTests.UnsafeRequests_FromASession_NeedTheAntiForgeryHeader`, `AuthEndpointsTests.SignIn_FromAnotherOrigin_IsRefused` |
| AC-11 | `SessionRevocationTests.*`, `AuthEndpointsTests.SignOutEverywhere_RevokesAll`, e2e sign-out |
| AC-12 | `AuthRateLimitTests.*` |
| AC-13 | `auth.storage.spec.ts`, `auth.interceptor.spec.ts`, `auth.effects.spec.ts`, e2e (no storage, no readable cookie) |
| AC-14 | `SecurityBaselineTests` gate cases, `SecurityGateTests.*` (real Staging host), `IdentityOptionsValidatorTests` |
| AC-15 | `ClientIpTests.*` |
