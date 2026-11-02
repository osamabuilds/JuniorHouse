# 0023 — Customer sign-in: sessions, and the four ways to sign in

- Status: Accepted (owner, 2026-11-02, "go with your recommendation"). Scope narrowed by the owner on 2026-11-02: **no Google or Facebook sign-in**; email and password only, with an SMS code to verify the mobile number at sign-up. Decisions 6 and the provider parts of 10 are not built (kept for the record).
- Date: 2026-10-29
- Amends: [0021](0021-customers-module-and-stand-in-customer.md) decisions 2 and 4 (the stand-in stays only in Development/Testing; the pass-through now carries cookies)

## Context

Customers need registration, sign-in, password reset and failed-login protection (FR-38, SEC-02, SEC-03, SEC-04, NFR-15). The owner chose SEC-02 (progressive throttling plus CAPTCHA, never a lock) and Cloudflare Turnstile, and added Google, Facebook and mobile-number-plus-SMS-code sign-in. Staff sessions already exist ([0015](0015-identity-module-sessions-and-permissions.md)). The browser talks only to the storefront SSR server, which forwards to the API (ADR 0018).

## Decision

1. **One customer session, four ways to get it.** Email and password, Google, Facebook, and SMS code all end the same way: a new server-side opaque session (random 256-bit access value, 15 minutes; rotating single-use refresh value; only SHA-256 hashes stored; reuse of a rotated refresh value revokes the session). Idle and absolute lifetimes follow 0015, except "Keep me signed in", which makes the refresh lifetime 30 days instead of 12 hours. Every sign-in issues a new session and revokes any presented one (SEC-04).
2. **Written in `Customers`, not shared with `Identity`.** The mechanics mirror 0015 but the tables, cookies (`__Secure-rc-access`, `__Secure-rc-refresh`) and policy are separate: different lifecycle and privacy class (ADR 0021 decision 1). Duplication is accepted; lifting to a building block is revisited only if a third consumer appears.
3. **Cookies live in the browser on the storefront origin; the SSR server forwards them** to the API through `/customer-api` and passes `Set-Cookie` back (Q-ACT-5, confirmed by the owner 2026-11-02). Cookies are `HttpOnly`, `Secure`, `SameSite=Lax` (Lax, chosen by the owner; every unsafe method still needs the CSRF header). CSRF: header `X-Romp-Csrf` equals `HMAC(serverKey, sessionId)`, as in 0015.
4. **Passwords:** Argon2id, same parameters mechanism as staff. Rule: 8 or more characters, at least one capital, one small letter and one digit (Q-ACT-3); special characters and spaces are allowed, maximum length 128. The same rule list is exposed by the API so the browser shows each rule met or not met while typing and the server returns the failed rules by name.
5. **Failed-login handling: SEC-02.** Per account-and-IP pair and per IP, from the 3rd failure each attempt is delayed (`Retry-After`) and the Turnstile token is required; there is no lock. The delay and the answer are identical for unknown and known emails (ACT-5, timing equalised by hashing a dummy). Turnstile is checked by `ICaptchaVerifier`; tests use a fake. Every attempt, for every method, is one row in a login-attempt table (ACT-10).
6. **(Not built, owner 2026-11-02) Google and Facebook** use the authorization-code flow with PKCE, a `state` value and a nonce bound to a short-lived signed cookie (not built). The server exchanges the code, verifies the Google ID token (signature against Google's keys, audience, issuer, expiry, nonce) or reads the Facebook profile with the server-side access token, and trusts nothing the browser sends as identity. A provider identity is stored as (provider, subject). Same-email rule (Q-ACT-10 A): link to an existing account only when the provider states the email is verified; otherwise the customer must first sign in with the password. A Facebook login without an email asks for one. The Facebook data-deletion callback verifies the signed request and deletes the link.
7. **SMS code: our own verification, delivery through a replaceable sender (owner, 2026-11-02: Twilio is too costly for Pakistan).** The module generates the 6-digit code, stores only its hash, valid 5 minutes, single use, 5 wrong tries (Q-ACT-11), and checks it itself. Only the delivery goes through an `ISmsSender` port. Adapters: a development adapter that writes the code to the log and Mailpit (free, used locally and in tests), and a Pakistani SMS gateway adapter chosen by the owner after quotes (Twilio stays possible as another adapter, not the default). Limits: 3 sends per number and 10 per IP per hour, Turnstile after the second wrong code, never a lock. The response to send code is identical for known and unknown numbers. A phone number is unique per customer and a Pakistani mobile only (Q-ACT-13).
8. **Reset:** single-use, expiring (60 minutes) link; the token is random, only its hash stored, and it is sent through the email port ([0022](0022-email-sender-port.md)). A reset or password change revokes every other session (ACT-13).
9. **The merge hook.** `ICustomerSignedInHook` is called after any successful sign-in or registration with the guest state the browser posted (wishlist ids now, cart in S11). Hook failures are logged and never fail the sign-in.
10. **Providers are switches, not code paths.** Each method has a setting (`Customers:Auth:Google:Enabled`, and so on); a method without credentials is hidden by the API's `GET /api/auth/methods`. Facebook can therefore be off until Meta approves the app. Credentials come from configuration and secrets, never the repo. Outside Development/Testing the host refuses to start if a method is enabled without its secrets or if the stand-in handler is registered.
11. **Rate limiting.** The API applies the `strict` policy to all auth routes and the per-account throttle above. IP limits at Cloudflare and in the SSR layer stay as ADR 0018 decision 5; pulling the SSR limiter forward from `seo` is still an open owner question and the API limits stand alone until then.

## Alternatives considered

- **ASP.NET Core Identity or an external identity service (Auth0, Firebase, Keycloak):** rejected for the same schema, naming and dependency reasons as 0015; an external service also moves customer data (including minors' parents) to a third party the owner has not chosen.
- **JWT access tokens:** rejected; revocation and "sign out everywhere" need a server lookup anyway.
- **SameSite=Strict for customers:** breaks the return from Google and Facebook; Lax plus the CSRF header is the compromise.
- **Treating the provider email as proof of identity without the verified flag:** rejected (account takeover through an unverified address at the provider).
- **Hard lockout (FR-39):** rejected by the owner (SEC-02).

## Consequences

- New dependencies, to be pinned in `Directory.Packages.props` after a licence and vulnerability check: Google ID-token validation (the Google library or JWT validation against Google's keys) and the Twilio SDK or plain HTTP calls; Argon2 already exists. Plain HTTP with `HttpClient` is preferred where it is small.
- Real credentials are needed only for the final check with real providers; everything else is built and tested against fakes.

- The stand-in customer remains for local runs and tests; production never registers it.
