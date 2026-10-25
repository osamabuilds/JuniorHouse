# Customer accounts: registration, sign-in, password reset

- **Status:** Draft (2026-10-25). **Scope revised (owner, 2026-10-25): real sign-in and security are built last, together with Sprint 8.** Sprint 10 builds only the stand-in described below.
- **Jira:** SCRUM-59 (FR-38), SCRUM-60 (FR-39), SCRUM-122 and SCRUM-199 (SEC-02) under epic SCRUM-9
- **BRD sections:** §7.5, §11.2, §6.4 (TC-ACC-01..07), §12.3.1
- **Requirement IDs:** FR-38, FR-39, SEC-02, SEC-03, SEC-04, NFR-15, SEC-25, NFR-26

## Problem & intent
A parent needs an account to save addresses, children, wishlist items and later to see orders. Registration and sign-in must be easy, must not reveal whether an email is registered through the sign-in or reset screens, and must resist password guessing without letting an attacker lock a real customer out.

## Scope split (owner decision, 2026-10-25)
**Sprint 10 (now):** the other customer features need to know who the customer is, so this feature delivers only an `ICurrentCustomer` port, a development-only implementation (a seeded test customer, switchable in local runs, refused outside Development exactly like the staff stand-in, ADR 0011), and the rule that no customer endpoint takes a customer id from the request (SEC-05). No registration, sign-in, reset or throttling screens are built. Nothing customer-facing is deployed before real sign-in exists.

**Final auth sprint (with S8):** everything else below (ACT-1..ACT-13, ACT-15, ACT-16 and the Q-ACT-* questions, including FR-39 vs SEC-02) is unchanged and waits for that sprint.

**Sprint 10 acceptance for this feature:** ACT-14 (merge hook as an interface only) and ACT-18. ACT-17 (the start-up gate) is **parked for S8** (owner, 2026-10-25): nothing is deployed before S8, which is already the project rule.

## Requirements (quoted from the BRD)
> **FR-38:** The system shall support account registration, duplicate-email prevention, login, and a self-service, single-use, expiring password-reset link.

> **FR-39:** The system shall temporarily lock an account after repeated failed logins, with a defined unlock path.

> **SEC-02:** Failed-login handling shall use progressive throttling and CAPTCHA (e.g. after 3 failed attempts) rather than a hard account lockout, to prevent an attacker from denial-of-service-locking a legitimate customer's account by deliberately failing their login.

> **SEC-03:** Session cookies/tokens shall be HttpOnly, Secure, and SameSite=Strict (or Lax where cross-site flows require it), with short-lived access tokens and rotating refresh tokens.

> **SEC-04:** A new session identifier shall be issued on every successful login (session-fixation prevention).

> **NFR-15:** Login attempts shall be logged and rate-limited to mitigate brute-force/credential-stuffing.

## User flow
1. **Register** (design `AuthScreens`): first and last name, email, mobile number, password, Terms and Privacy checkbox (required), newsletter checkbox (optional, see [newsletter](../newsletter/spec.md)). On success the customer is signed in.
2. **Sign in:** email, password, "Keep me signed in" `(new, see Q-ACT-4)`. Success returns to the page they came from.
3. **Forgot password:** enter email; the screen always says "If an account exists for that email address, a reset link is on its way". The link opens "Set a new password". The link works once and expires.
4. **Sign out**, and after a password change all other sessions end `(new)`.
5. **Repeated failures:** see AC-8..AC-10; wording depends on Q-ACT-1.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| ACT-1 | Given a valid email and a password that meets the rule, when registering, then the account is created and the customer is signed in. | TC-ACC-01 |
| ACT-2 | Given a password below the rule, then registration is blocked and the rule is explained next to the field. | TC-ACC-02 |
| ACT-3 | Given an email already registered (compared case-insensitively, trimmed), then registration fails with guidance to sign in or reset; no second account exists. | TC-ACC-03, FR-38 |
| ACT-4 | Given valid credentials, when signing in, then the customer is signed in and sent back to where they were. | TC-ACC-04 |
| ACT-5 | Given a wrong email or a wrong password, then the response and its timing are the same ("We could not sign you in"); the message never says which was wrong. | NFR-15 (new) |
| ACT-6 | Given a reset request for any email (known or unknown), then the response is identical and no account existence is revealed; for a known email a link is emailed. | FR-38, design rule |
| ACT-7 | Given the emailed link, when a new password is set, then the old password stops working and the new one works. | TC-ACC-06 |
| ACT-8 | Given a reset link already used, or past its lifetime, then it is rejected as expired or invalid and offers a new request. | TC-ACC-07 |
| ACT-9 | Given repeated failed sign-ins, then the account response follows the answer to Q-ACT-1: **(A, SEC-02)** from the 3rd failure each attempt is delayed and a CAPTCHA is required; the account is never locked; **(B, FR-39 and TC-ACC-05)** after 5 failures the account is temporarily locked and the unlock path is shown. | TC-ACC-05, FR-39, SEC-02 |
| ACT-10 | Given any sign-in attempt, then one row records the email as typed (truncated, control characters removed), the client IP, the outcome and the time; the password is never recorded. | NFR-15, SEC-25 |
| ACT-11 | Given a successful sign-in, then a new session is issued and any session cookie sent with the request is discarded; cookies are `HttpOnly`, `Secure`, `SameSite` as SEC-03 and hold only random values whose hashes the server stores. | SEC-03, SEC-04 |
| ACT-12 | Given a session, then the access cookie is short-lived and the refresh cookie rotates on each use; reuse of a rotated refresh value revokes the session. | SEC-03 |
| ACT-13 | Given a password change or reset, then every other session of the customer is revoked `(new)`. | SEC-03 (new) |
| ACT-14 | Given a guest cart or guest wishlist exists when the customer signs in, then the sign-in result carries the hook the cart (S11) and the wishlist ([wishlist](../wishlist/spec.md)) use to merge; nothing is lost. | FR-35, FR-17 |
| ACT-15 | Given passwords, reset tokens or session values, then none appears in logs, URLs of indexable pages or error bodies; reset tokens are stored hashed. | SEC-25 |
| ACT-17 *(parked for S8)* | Given a non-Development environment, then the host refuses to start while only the stand-in customer exists. | ADR 0011 (new) |
| ACT-18 | Given the stand-in, then every customer endpoint reads the customer from `ICurrentCustomer`, and tests prove customer A cannot reach customer B's data through any customer endpoint. | SEC-05 |
| ACT-16 | Given the account pages, then they send `noindex`, are disallowed in `robots.txt`, and every form control has a visible label with the right `autocomplete` value. | SEO-05, NFR-19 |

## Non-functional constraints
- Passwords are hashed with a slow, salted algorithm chosen in the plan; sign-in p95 under 500 ms excluding the intentional hash cost.
- SEC-03 and SEC-04 apply as written. The SSR storefront calls the API server-side, so cookie forwarding is a plan decision (Q-ACT-5).
- Rate limiting by IP is applied at Cloudflare and in the SSR layer, not inside the API (storefront decision P-4); per-account throttling is applied in the API. The SSR rate limiter is currently deferred with the `seo` feature, so Q-ACT-6 asks whether it is pulled forward for sign-in.
- Customer personal data is used only to run the account and fulfil orders (NFR-26).

## Out of scope
Staff sign-in and MFA (S8); social sign-in; MFA for customers; order history; "remember this device"; account deletion and data export of the whole account (only Child Profile data is covered by NFR-29, see [child-profiles](../child-profiles/spec.md)).

## Open questions
- [ ] **Q-ACT-1 (blocks the plan) FR-39 vs SEC-02.** The BRD contradicts itself: FR-39 and TC-ACC-05 say temporary lock after repeated failures (5+); SEC-02 says progressive throttling and CAPTCHA, explicitly not a lock, because a lock lets an attacker lock a customer out. Recommendation: SEC-02, and amend FR-39/TC-ACC-05 to match (the "unlock path" then becomes "wait out the delay or solve the CAPTCHA", and the password reset is always available). Who decides: owner. Until answered, ACT-9 keeps both readings.
- [ ] **Q-ACT-2 Email verification.** The BRD data model has `IsEmailVerified` but FR-38 does not require verification and the design does not draw it. Default: no verification at registration (the reset flow still proves ownership); verification is added when order emails (S13) make a wrong address costly. OK?
- [ ] **Q-ACT-3 Password rule.** TC-ACC-02 says "complexity rule" without stating it. Default: at least 10 characters, not on a common-password list, no composition rules (current NIST advice). Or a stated rule such as 8 characters with a letter and a digit?
- [ ] **Q-ACT-4 "Keep me signed in".** The design draws it. Default: it extends the refresh lifetime (30 days instead of 12 hours); session lifetimes otherwise as the staff defaults (15 minutes access, 30 minutes idle).
- [ ] **Q-ACT-5 Where do cookies live?** Proposal: the browser holds the cookies on the storefront origin and the SSR server forwards them to the API. Alternative: the SSR server holds the session itself. Settled in the plan with an ADR if you have no preference.
- [ ] **Q-ACT-6 CAPTCHA provider and SSR rate limiter.** Proposal: Cloudflare Turnstile (free, no image puzzles, already behind Cloudflare), with a fake verifier in tests. Pull the shared SSR rate limiter forward from `seo` task 12 for the sign-in and reset pages?
- [ ] **Q-ACT-7 Mobile number.** The design asks for a mobile number "used for delivery updates". Required or optional at registration? Format check: Pakistani mobile (`+92 3xx xxxxxxx`)?
- [ ] **Q-ACT-8 Terms and Privacy pages.** The checkbox links to them; do the content pages from `storefront-settings` already cover them, or is text missing from the business?
