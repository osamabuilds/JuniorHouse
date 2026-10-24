# SCRUM-202: Sessions and sign-in

- **Status:** Approved (owner, 2026-10-16)
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
