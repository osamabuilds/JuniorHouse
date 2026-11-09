# 0015 — Identity module: cookie sessions, TOTP, permission-based authorization

- Status: Accepted (approved by the owner 2026-10-16)
- Date: 2026-10-16

## Context

Sprints 1 to 7 declared authorization policies and satisfied them with a development-only handler (ADR 0011). Sprint 8 replaces it. Owner decisions of 2026-10-16: same-origin HttpOnly cookies, TOTP only, the four BRD role names plus an Owner, authorization by capability not by role string, no email sender yet.

## Decision

1. **New module `Identity`** (schema `IDNT`) with Domain, Application, Infrastructure and Contracts projects, per ADR 0002. Other modules never reference it. The cross-module surface is: the authenticated principal (user name claim) read through `ICurrentActor` in BuildingBlocks, and permission names used in endpoint declarations. Customers (S10) are a separate concern and may become their own module reusing the session classes.
2. **Sessions are server-side and opaque.** Sign-in issues two random 256-bit values: an *access* value (15 minutes) and a *refresh* value (single use, rotated on each refresh). Both are sent as cookies (`__Secure-` prefix, `HttpOnly`, `Secure`, `SameSite=Strict`); only SHA-256 hashes are stored in `IDNT`. A rotated refresh value presented again revokes the session family. This meets "short-lived access tokens and rotating refresh tokens" (SEC-03) without a JWT: nothing is trusted from the client, so revocation and role changes apply on the next request (SEC-08).
3. **A custom `AuthenticationHandler`** reads the access cookie, loads the session and user in one query, and builds the principal. No claims about roles or permissions are placed in it.
4. **Authorization by permission.** `PermissionRequirement(code)` and one handler that loads the user's effective permissions for the request (union over roles; the Owner role carries a `ALL_PERM_IND` flag instead of mapping rows). Endpoint groups declare permissions through `MapSecuredGroup(prefix, permission)` and mutating routes add `RequirePermission(write)`. The old `Staff/Admin/Inventory/Catalogue` policy names are removed. `RequireRole` and `IsInRole` are forbidden by an architecture test.
5. **Two-step sign-in.** Password success issues a 5-minute *pending* cookie (no API access) that carries the sign-in state; TOTP or a recovery code completes it and issues the session pair. An account without MFA, or with "must change password", receives a session limited to the enrolment and change-password endpoints (enforced by a `SessionScope` check in the handler, not by hiding UI).
6. **CSRF.** `SameSite=Strict` plus a required anti-forgery header on unsafe methods: a per-session value is returned by the "who am I" endpoint and echoed by the admin app in `X-Romp-Csrf`; the server compares it with a value derived from the session.
7. **Passwords:** Argon2id (parameters in configuration, tuned to about 250 ms on the staging VPS). **TOTP** per RFC 6238 via a small maintained library, or in-house if the licence or maintenance check fails; QR codes are drawn in the browser so the secret never goes to a third party. The TOTP secret is encrypted with AES-GCM using a key from configuration (deployment secret), with a key id stored beside the ciphertext to allow rotation.
8. **Throttling** is per user name plus client IP, stored in `IDNT` (survives restarts, works with more than one API node), with delays that double from the third failure to a 15-minute cap and never lock the account.
9. **Client IP** comes from `X-Forwarded-For` / `CF-Connecting-IP` only when the immediate peer is the trusted reverse proxy (configured network).
10. **Development and Testing keep the existing development handler** (signing in as an Owner), so earlier sprints' integration tests continue to run; real authentication has its own tests (roles AC-11).

## Alternatives considered

- **JWT bearer tokens:** rejected for D-3 (same-origin) and because revocation and role changes would need server state anyway.
- **ASP.NET Core Identity:** rejected: it brings its own schema and naming (violating `docs/db/naming.md`), role-string checks and a larger surface than the few features needed; the password hasher it ships (PBKDF2) is not one of the BRD's named algorithms.
- **An external identity provider (Keycloak, Auth0):** more moving parts and a recurring cost for a handful of staff; reconsider if customer sign-in grows.

## Consequences

- The admin proxy must serve `/api` on the same origin as the app (ADR 0017); CORS is no longer needed outside local development.
- Every protected request costs one session query plus one permission query; acceptable at back-office volume and revisited for the customer storefront.
- New dependencies (to be pinned in `Directory.Packages.props` after a licence and vulnerability check, named in the plan): an Argon2 implementation, optionally a TOTP library.

## As built (2026-10-26)

- Sessions, MFA, roles and staff accounts follow this decision. TOTP is implemented in-house (RFC 6238, tested against its vectors) rather than through a library, so no new package was added for it; Argon2 is the one new dependency. Permissions are data (`PERM_LKP`, `ROLE_LKP`, `ROLE_PERM_MAP`) read once per request; the old role-shaped policies are gone.
- Refresh rotation has a 10-second grace for a concurrent retry: the previous refresh value inside the grace answers 409 without revoking; outside it, reuse revokes the session.
- Write buttons in the admin app are hidden through a `PermissionAccess` check (`*hasPermission`); the server check stays the real control.
