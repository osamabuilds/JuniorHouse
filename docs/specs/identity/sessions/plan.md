# SCRUM-202: Sessions and sign-in, technical plan

- **Spec:** [spec.md](spec.md) · **Module plan:** [../plan.md](../plan.md) · **ADR:** [0015](../../../adr/0015-identity-module-sessions-and-permissions.md)

## Domain model
- `Session` aggregate: user id, access and refresh token hashes, previous refresh hash, three expiries (access 15 min, refresh idle 30 min, absolute 12 h), revoked date and reason. Behaviour: `Rotate(now)` (new pair; keeps the old refresh hash as `Previous`), `Revoke(reason)`, `IsUsable(now)`.
- `PendingSignIn` (password passed, MFA not yet): token hash, user id, 5-minute expiry, failure count (5 destroys it).
- `ThrottlePolicy` (pure): from the recent attempts of a name+IP pair returns the delay: none for 2 consecutive failures, then 30 s, 60 s, 120 s ... capped at 900 s since the last attempt.
- Tokens: 32 random bytes, base64url; hashed with SHA-256 before storage (high-entropy values need no slow hash). Lookup by hash through a unique index; comparison constant-time.

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `SignInCommand` | user name, password, client IP | pending cookie, or a limited session when MFA enrolment or password change is still needed, or throttled (429) | AC-6, AC-7, AC-8, AC-12 |
| `VerifyMfaCommand` | pending token, code | session cookies | AC-1, AC-4 (new session; revokes any session presented) |
| `RefreshSessionCommand` | refresh token | new cookie pair | AC-2, AC-3 |
| `SignOutCommand`, `SignOutEverywhereCommand` | session | cleared cookies | AC-11 |
| `RevokeUserSessionsCommand` | user id, reason | count | staff-accounts AC-8 |
| `AuthenticateSessionQuery` | access token | principal + scope or failure | AC-9 |
| `PurgeExpiredSessionsJob` | none | rows removed | housekeeping |

Scope of a session (computed on each request, never stored): `Full`, `EnrolMfa`, `ChangePassword`. The authentication handler marks the principal; `RequirePermission` handlers refuse everything but the enrolment and password endpoints when the scope is limited.

## API endpoints
Module plan table, `/api/auth/*`. Cookies: `__Secure-romp-acs` (path `/api`), `__Secure-romp-rfs` (path `/api/auth/refresh`), `__Secure-romp-pnd` (path `/api/auth/mfa`); all `HttpOnly; Secure; SameSite=Strict`. In Development over HTTP the `__Secure-` prefix and `Secure` flag are dropped by a single option, and a start-up check rejects that option outside Development/Testing.

## Events
`SecurityEventRaised` log events only (see [observability](../../platform/observability/spec.md) AC-9); no integration events.

## Data
`SESS_MAIN`, `SESS_RVK_RSN_LKP`, `LGIN_PEND`, `LGIN_ATMP`, `LGIN_OTCM_LKP` (module plan). Throttle state is read from `LGIN_ATMP` with one indexed query. Daily purge of rows older than 90 days.

## NFR / security design
- **Session fixation (SEC-04):** sign-in never reuses an id; a presented session is revoked first.
- **Reuse detection:** refresh matches `RFSH_TOKN_HASH` (current) or `PREV_RFSH_TOKN_HASH` (reuse, revoke and raise a security event).
- **Timing:** unknown user runs a dummy Argon2 verification so response time matches (AC-7).
- **CSRF:** header `X-Romp-Csrf` equals `HMAC(serverKey, sessionId)`, returned by `GET /api/auth/me`; validated in an endpoint filter on unsafe methods; sign-in itself is protected by `SameSite=Strict` plus an `Origin` header check.
- **Forwarded headers:** `UseForwardedHeaders` with `KnownProxies/KnownNetworks` from configuration so the visitor IP is trusted only via the reverse proxy; Cloudflare's `CF-Connecting-IP` is read only when the peer is in the configured ranges.
- **Cache:** auth responses send `Cache-Control: no-store`.
- Handler cost per request: one query joining session and user; measured against the 100 ms refresh and 5 ms budget (roles spec).

## Frontend
`features/identity/sessions/`: sign-in page (user name, password; generic error text; shows retry delay from `Retry-After`), MFA challenge page, change-password page; `core/auth` store, interceptor, guards. Pages are `noindex`. Focus is moved to the error summary on failure; all controls labelled; no credential stored in browser storage (AC-13).

## Risks & alternatives considered
- Server-side sessions add a query per request: accepted (ADR 0015).
- Throttle by name+IP lets an attacker rotate IPs to try many passwords against one name: the per-name delay is also applied after 10 failures across all IPs in 15 minutes (a slower global ceiling), without locking the account; MFA still blocks a guessed password.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | Anonymous only for the four routes on the explicit list; everything else needs a valid session and the right scope. |
| Input validation | Name and password length-limited before hashing (max 128) so huge inputs cannot burn CPU; JSON body max 4 KB. |
| Output encoding | Cookie values are base64url; no user data in headers except `Retry-After`. |
| Outbound calls | None. |
| Resource limits | Strict rate limit per IP on auth routes; pending sign-ins limited; sessions per user capped at 10 (oldest revoked). |
| Concurrency | Refresh rotation is a conditional update (`WHERE RFSH_TOKN_HASH = @old`); two simultaneous refreshes yield one winner and the loser counts as reuse only after a 10-second grace (clients retry once). |
| Idempotency | Sign-out is idempotent; refresh is single use by design. |
| Audit | Every attempt in `LGIN_ATMP`; session creation and revocation audited via the generic mechanism. |
| Data protection | Names typed at sign-in are sanitised and truncated before storage and logs; no password or token in any log (redaction tests). |
| Secrets and dangerous config | Anti-forgery HMAC key is a deployment secret; the insecure-cookie option is refused outside Development/Testing at start-up; the ADR 0011 gate stays. |
| Error responses | One generic sign-in failure message; 429 carries only `Retry-After`. |
| New dependencies | None beyond the module plan. |
| Coupling | Provides `ICurrentActor` through the principal; no module reads Identity tables. |
| Abuse cases | (1) Credential stuffing against many names: per-IP strict limit, per-pair throttle, global per-name ceiling, MFA. (2) Stolen refresh cookie replayed: rotation plus reuse detection revokes the session. (3) CSRF from another site: SameSite=Strict, Origin check, anti-forgery header. (4) Forged `X-Forwarded-For` to dodge throttling: ignored unless the peer is the trusted proxy. |
