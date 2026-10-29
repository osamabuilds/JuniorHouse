# Customer accounts: technical plan (Sprint 10 slice)

- **Spec:** [spec.md](spec.md) (this plan covers ACT-14 and ACT-18 only; ACT-1..13, 15, 16 wait for the auth sprint and ACT-17, the start-up gate, is parked for S8)
- **Module:** Customers · **ADR:** [0021](../../../adr/0021-customers-module-and-stand-in-customer.md), [0022](../../../adr/0022-email-sender-port.md) · **Module plan:** [../plan.md](../plan.md)

## Domain model
`Customer` (`Id`, `Email` normalised lower-case/trimmed, `FirstName`, `LastName`, `Phone`). Invariant: email unique. No password, no credentials yet. The stand-in is one seeded `Customer` row in Development/Testing (`dev.customer@example.test`). `CustomerId` is a value type (`long`) so a raw id cannot be passed where a scoped id is expected.

## Application layer
| Type | Purpose | Covers |
|---|---|---|
| `ICurrentCustomer` (`CustomerId?`, `IsSignedIn`, `Require()`) | the only way features learn who the customer is | ACT-18 |
| `ICustomerSignedInHook` (`OnSignedInAsync(customerId, guestState)`) | merge point for guest wishlist now, cart in S11 | ACT-14 |
| `GetCurrentCustomerQuery` | account shell: name and which sections exist | ACT-18 |
| `ICustomerEmailQueue` | writes `CustomerEmailRequestedEvent` in the caller's transaction (used by newsletter and notify-me) | ADR 0022 |

## API endpoints
| Method | Route | Policy | Ownership |
|---|---|---|---|
| GET | `/api/customers/me` | `Customer` | the caller only; no id parameter |
| GET | `/dev/customer` (Development/Testing only; absent otherwise) | none, dev-only | sets the stand-in on/off for the browser session |

## Events
None of its own. Defines `CustomerEmailRequestedEvent` (module plan).

## Data
`CUST.CUST_MAIN` (`ID`, `EML` varchar(254) unique on lower-case, `FRST_NAME`, `LAST_NAME`, `PHON` varchar(20) null, audit, `xmin`). Created by the first migration with the module's `OUTB_MSG` and `INBX`. The Development seeder (`Romp.Api/Seeding/DemoCustomerSeeder`) inserts the stand-in and a second customer (for ownership tests); never run outside Development/Testing.

## NFR / security design
`DevelopmentCustomerHandler` authenticates `Policies.Customer` as the seeded customer unless the request carries `X-Dev-Customer: none` or the cookie set by `/dev/customer`; The start-up gate (`CustomerGate`, ACT-17) is **parked for S8**; the dev handler is simply registered only in Development/Testing. The endpoint-list architecture test asserts every route under `/api/customers/me` sits in a `Policies.Customer` group and no handler takes a customer id from the request (ACT-18).

## Frontend
Storefront `features/customers/accounts`: account shell (`/account`, `noindex`, `no-store`) with the left menu from `AccountPages` (Overview, Addresses, Wishlist, Preferences; Child profiles is parked; Orders and Sign out are not drawn until their sprints), a "Development customer" banner shown only when the dev switch is available, and a sign-in placeholder page for guests ("Sign-in arrives with the security release", dev only). Header account icon and footer link added to the chrome (module plan, step 7).

## Risks & alternatives
Alternatives and risks are in ADR 0021. The main risk is that the stand-in lulls a deployment; the start-up gate is the control.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | `Policies.Customer` on `/api/customers/**`; dev handler only in Development/Testing; start-up gate; ownership by construction (ADR 0021 §3) |
| Input | `/dev/customer` accepts `on`/`off` only; no other input |
| Output encoding | Names rendered by Angular templates (encoded) |
| Outbound calls | none |
| Resource limits | n/a (single-row read) |
| Concurrency | n/a |
| Idempotency | n/a |
| Audit | audit columns only |
| Data protection | email and name are personal data: never logged; not in events |
| Secrets and dangerous configuration | dev handler and `/dev/customer` registered only in Development/Testing; the start-up refusal is parked for S8 |
| Error responses | 401 problem details when signed out |
| New dependencies | none |
| Coupling | Contracts-free: other features use `ICurrentCustomer` from Application only |
| Abuse cases | (1) Production deployed with the stand-in: start-up refuses. (2) A request with `X-Dev-Customer` against a production-like host: header ignored because the handler is not registered. (3) Guessing another customer's id in a route: no route takes one; row filter returns 404 |

---

# Real sign-in (revised 2026-10-29): technical plan

- **Covers:** ACT-1..13, 15, 16, 19..26 · **ADR:** [0023](../../../adr/0023-customer-sign-in-sessions-and-methods.md) · supersedes the Sprint 10 slice above wherever they differ (the stand-in stays for Development/Testing only).

## Domain model
- `Customer` gains `Phone` (unique, Pakistani mobile, normalised to `+923xxxxxxxxx`), `PasswordHash` (nullable: a Google, Facebook or SMS-only customer has none), `LastSignedInAt`.
- `CustomerSession` (hashes of access and refresh, previous refresh hash, three expiries, `KeepSignedIn`, revoked date and reason); `Rotate`, `Revoke`, `IsUsable` as in the staff session.
- `ExternalLogin` (`CustomerId`, `Provider` lookup, `Subject`, `EmailAtLink`); unique on (provider, subject).
- `PhoneChallenge` (phone, code hash or provider reference, expiry, tries, used date).
- `PasswordResetToken` and `EmailVerificationToken` (token hash, expiry, used date); `Customer` also holds `EmailVerifiedAt` (null until the link is used, ACT-27, ACT-28).
- `PasswordRule` (pure): returns the list of rules with met or not met; used by the API response and by tests (ACT-2).
- `SignInThrottle` (pure): from failures in the window returns delay and `CaptchaRequired` (ACT-9 reading A).

## Application layer
| Command / Query | Output | Covers |
|---|---|---|
| `RegisterCommand` | session; refuses weak password, duplicate email or phone | ACT-1, 2, 3 |
| `SignInWithPasswordCommand` | session, or generic failure with delay | ACT-4, 5, 9, 10 |
| `RefreshSessionCommand`, `SignOutCommand` | cookies | ACT-11, 12 |
| `SendEmailVerificationCommand`, `VerifyEmailCommand` | link by email via the email port; single use, expiring | ACT-27, 28 |
| `RequestPasswordResetCommand`, `ResetPasswordCommand` | identical answer; revokes other sessions | ACT-6, 7, 8, 13 |
| `StartExternalSignInQuery` (provider) | redirect address, state and nonce cookie | ACT-22 |
| `CompleteExternalSignInCommand` | session, or link-needs-password, or ask-for-email | ACT-20, 21, 22 |
| `SendPhoneCodeCommand`, `VerifyPhoneCodeCommand` | identical answer; session or ask-for-name-and-email | ACT-23, 24, 25 |
| `CompleteProfileCommand` | finishes an account created by SMS or Facebook | ACT-20, 24 |
| `GetSignInMethodsQuery` | which methods are on | ADR 0023 §10 |
| `ICaptchaVerifier`, `ISmsSender`, `IExternalIdentityProvider` (Google, Facebook), `ICustomerSignedInHook` | ports with fakes for tests | ACT-14, 22, 23 |

## API endpoints (all under `/api/auth`, anonymous unless stated, `strict` rate limit)
`GET methods` · `GET password-rules` · `POST register` · `POST sign-in` · `POST refresh` · `POST sign-out` (customer) · `POST email/verify` · `POST email/resend` (customer) · `POST password-reset/request` · `POST password-reset/confirm` · `GET external/{provider}/start` · `GET external/{provider}/callback` · `POST phone/send` · `POST phone/verify` · `POST profile` (customer, limited session) · `POST external/facebook/deletion` (signed request). The storefront SSR server forwards the browser calls and the cookies; the pass-through allow-list in `server.ts` grows to exactly these routes.

## Data (schema `CUST`; new glossary words added as needed)
`CUST_MAIN` (adds `PHON`, `PSWD_HASH`, `LAST_SGIN_DTE`; `PHON` unique), `CUST_SESN`, `CUST_XLGN` (external login), `AUTH_PRVD_LKP` (GOOGLE, FACEBOOK), `CUST_PHCH` (phone challenge), `CUST_PWRT` (reset token), `CUST_EVRT` (email verification token), `CUST_MAIN` also gets `EML_VRFY_DTE`, `CUST_LGIN_ATMP` (one row per attempt, any method, partition key the attempt date). Every table: audit columns, `xmin`, declared and indexed FKs. Session and token tables are purged by a housekeeping job.

## NFR / security design
SEC-02, SEC-03, SEC-04, NFR-15 as in ADR 0023 (throttle without lock, Turnstile, Lax cookies with the CSRF header, rotation with reuse detection, one attempt row per try). SEC-25: no password, code, token, provider token or full phone number in logs, URLs or error bodies; reset and code values stored hashed. Sign-in p95 under 500 ms excluding the intended hash cost.

## Frontend
Storefront `features/customers/sign-in`: sign-in, register (with live password checklist), forgot and set-new-password, mobile sign-in (number then code), complete-profile pages. All `noindex`, `no-store`, every control labelled with the right `autocomplete`, errors move focus to the summary, Turnstile widget loaded only on these pages. Buttons "Continue with Google" and "Continue with Facebook" shown only when `GET methods` says so. `core/customer-auth` store, interceptor-free (cookies), a guard for `/account`, the header account link and sign out. The development customer banner stays for Development only.

## Order of work (becomes `tasks.md` after approval; email verification joins step 3)
1. Pure rules and entities (`PasswordRule`, `SignInThrottle`, session, phone normalisation). 2. Migration and glossary. 3. Password registration, sign-in, refresh, sign-out, reset with the fake CAPTCHA and email port. 4. Merge hook wired. 5. Google and Facebook against fake providers. 6. SMS code against a fake sender. 7. Pass-through, storefront pages and e2e. 8. Real-provider check when the owner has the accounts. The wishlist resumes after step 4 so its merge hook is real.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | Anonymous only for the listed `/api/auth` routes; everything else `Policies.Customer`; limited session (profile incomplete) can reach only `profile` and `sign-out` |
| Input | Password rule, email, phone and provider names validated; `state`, nonce and signed requests verified; request bodies size-limited |
| Output encoding | Angular encoding; no provider text rendered as HTML |
| Outbound calls | Google keys and token endpoint, Facebook Graph, Twilio, Turnstile: fixed hosts, timeouts, no user-supplied URLs |
| Resource limits | `strict` limiter, per-pair and per-IP throttle, SMS caps (they also bound cost) |
| Concurrency | unique indexes on email, phone and (provider, subject); refresh rotation under a row lock |
| Idempotency | reset and code single-use; repeat register returns the generic duplicate answer |
| Audit | attempt table; session create and revoke through the generic mechanism |
| Data protection | personal data never logged; passwords and codes hashed; provider tokens not stored |
| Secrets and dangerous configuration | credentials from secrets; host refuses to start in production with a method enabled but no secret, or with the stand-in registered |
| Error responses | generic sign-in failure; problem details otherwise |
| New dependencies | possibly a Google token validator and Twilio client (ADR 0023), pinned after a licence and vulnerability check |
| Coupling | `Customers` only; other modules know `ICurrentCustomer` and the hook; no table of another module is read |
| Abuse cases | (1) Credential stuffing: throttle plus Turnstile, identical answers. (2) SMS pumping: per-number and per-IP caps, Turnstile, Pakistani numbers only. (3) Account takeover through an unverified provider email: linking needs the verified flag or the password. (4) OAuth CSRF or code injection: state, nonce, PKCE. (5) Stolen refresh cookie: rotation and reuse detection. (6) Enumerating emails or numbers: identical answers and timing |
