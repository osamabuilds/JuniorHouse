# Customers (Sprint 10): module tasks

Ordered, checkable steps. Each names the test that proves it. Test-first: write the failing test, then make it pass. Run .NET builds and tests in the Linux SDK container (Windows Application Control blocks local DLLs; integration results are container or CI results). Commits start with `SCRUM-xxx: `; the Jira umbrella key for Sprint 10 is created when the owner approves the plan.

**Approval status:** the specs were approved on 2026-10-25; the module and feature plans and ADRs 0021 and 0022 were drafted the same day. On 2026-10-26 the owner asked to "start planning and implementing next sprint" and was away, so work proceeds on the drafted plans as written. Anything the plans leave open (the `Q-*` questions) takes the default stated in the spec, is logged under "Decisions taken on defaults" below, and stays open for the owner to overrule.

**In scope:** accounts (the stand-in customer only), addresses, wishlist, newsletter, notify-me. **Parked by the owner:** child-profiles (no table, endpoint, screen or menu entry), and real customer sign-in, registration, reset and the FR-39 vs SEC-02 decision (the final auth sprint).

## Foundations

- [x] **F1** Module skeleton: `Romp.Modules.Customers.{Domain,Application,Infrastructure,Contracts}`, `CustomersModule : IModule`, solution entries, `Customers` in `ModuleNames`, `Program.cs` registration, `CUST` schema row in the constitution and naming doc, new glossary words. *Proof:* `ModuleBoundaryTests`, `StructureSyncTests`, `NamingConventionTests`, `GlossaryTests`.
- [x] **F2** `Customer` aggregate and `CUST_MAIN` with the first migration (`OUTB_MSG`, `INBX`); `ICustomerDbContext`. *Proof:* `CustomerTests`, `CustomerMigrationTests`.
- [x] **F3** `ICurrentCustomer`, `Policies.Customer`, the development-only handler (`X-Dev-Customer: none`, `/dev/customer` switch) and the seeder for the stand-in and a second customer. *Proof:* `DevelopmentCustomerHandlerTests`, `CustomerEndpointsTests.Me_ReturnsTheStandIn_AndNobodyWhenSwitchedOff` (ACT-14, ACT-18).
- [x] **F4** `GET /api/customers/me`. *Proof:* `CustomerEndpointsTests`, matrix row added to `authorization-matrix.csv` (customer routes are a separate policy, listed with `customer`).
- [x] **F5** Architecture tests: customer-owned data is never queried without the customer filter; no handler takes a customer id from the request; features depend one way only. *Proof:* `CustomersFeatureDependencyTests`, `CustomerOwnershipArchitectureTests`.

## addresses

- [x] **A1** Reference: `PROV_LKP`, `CITY_LKP.PROV_ID`, seed, `IGeographyQueries`, `GET /api/geography/provinces` and `/provinces/{id}/cities`. *Proof:* `GeographyMigrationTests`, `GeographyQueriesTests`, `GeographyEndpointsTests` (ADD-4).
- [x] **A2** `CustomerAddress` and `AddressBook` (default rules, limit, phone normalisation). *Proof:* `AddressBookTests`, `PhoneNumberTests` (ADD-2, ADD-5, ADD-6).
- [x] **A3** `CUST_ADDR` migration with the partial unique default index. *Proof:* `AddressMigrationTests`.
- [x] **A4** Commands and the paged list query, with the row lock on `CUST_MAIN`. *Proof:* `AddressCommandTests`, `AddressConcurrencyTests` (ADD-1, ADD-2, ADD-6, ADD-7).
- [x] **A5** Endpoints under `/api/customers/me/addresses`, ownership by construction. *Proof:* `AddressEndpointsTests.AnotherCustomersAddress_Is404ForReadEditRemoveAndDefault` (ADD-3).
- [x] **A6** `ICustomerAddressQueries` in `Customers.Contracts`. *Proof:* `AddressContractTests`.
- [x] **A7** Storefront `features/customers/addresses` (labels, dependent selects, focus trap), account shell, `noindex`, `no-store`. *Proof:* component and store specs, `AddressesPage` e2e (ADD-8).

## accounts: real sign-in

From the "Real sign-in" section of [accounts/plan.md](accounts/plan.md) and [ADR 0023](../../adr/0023-customer-sign-in-sessions-and-methods.md) (accepted 2026-11-02). Scope: email and password only, the mobile is collected but not verified for now, email verified by link, nothing ever blocks an order. No Google, Facebook or mobile-only sign-in. Turnstile keys are needed only for S11 (the SMS gateway waits for mobile verification); everything before it runs against fakes. Wishlist resumes after S6.

- [x] **S1** Pure rules: `PasswordRule` (8..128, capital, small, digit, no spaces, specials allowed; returns each rule met or not), `SignInThrottle` (delay and CAPTCHA from the 3rd failure, never a lock), phone normalisation to `+923xxxxxxxxx`. *Proof:* `PasswordRuleTests`, `SignInThrottleTests`, `PhoneNumberTests` (ACT-2, ACT-9, ACT-29).
- [x] **S2** Entities: `CustomerSession` (rotate, revoke, reuse detection), `PasswordResetToken`, `EmailVerificationToken`; `Customer` gains phone, password hash, email-verified date. *Proof:* `CustomerSessionTests`, `TokenTests`, `CustomerTests` (ACT-11..13, 27, 28). *As built:* `PhoneNumber` moved to the Domain root so Accounts and Addresses share it; tokens are hashed (`SecretString`); tests `CustomerSessionTests`, `OneTimeTokenTests`, `CustomerCredentialsTests`, `LoginAttemptTests`.
- [x] **S3** Migration and glossary: `CUST_SESN`, `CUST_PWRT`, `CUST_EVRT`, `CUST_LGIN_ATMP` (partitioned by date), `CUST_MAIN` new columns with an indexed, non-unique phone. *Proof:* `CustomerAuthMigrationTests`, `NamingConventionTests`, `GlossaryTests`. *As built:* tables are `CUST_SESS`, `CUST_PWD_RSET`, `CUST_EML_VRFY`, `CUST_LGIN_ATMP` (not partitioned: indexed, with a daily purge instead), lookups `SESS_RVKD_RSN_LKP`, `LGIN_OTCM_LKP`; migration `AddCustomerSignIn`; proof `CustomerSignInMigrationTests`.
- [x] **S4** Ports and fakes: `ICaptchaVerifier`, `IPasswordHasher` (Argon2id), email port (ADR 0022). `ISmsSender` and its dev and gateway adapters are deferred with mobile verification. *Proof:* `FakeAdapterTests`. *As built:* ports `IPasswordHasher` and `ICaptchaVerifier` in `Customers.Application/Accounts`; adapters `Argon2PasswordHasher`, `FakeCaptchaVerifier` (token `dev-captcha-pass`, Development and Testing only), `TurnstileCaptchaVerifier` (fixed host, 5 s timeout, fails closed); email is the new building block `Romp.BuildingBlocks.Email` (`IEmailSender`, `FakeEmailSender`, `SmtpEmailSender` on MailKit 4.18.1; `System.Security.Cryptography.Pkcs` pinned to 10.0.13 for advisories GHSA-846f-f764-76mx and GHSA-2fmv-rv2c-59x7). Production needs `Customers:Turnstile:SecretKey` and `Email:Smtp:Host` (checked when first used). Proof: `PasswordHasherTests`, `FakeAdapterTests` (31 tests).
- [ ] **S5** Register in one step (form, mobile format-checked and stored unverified), sign in, refresh with rotation, sign out, attempt log, CSRF header, cookies `HttpOnly`, `Secure`, `SameSite=Lax`. *Proof:* `RegisterTests`, `SignInTests`, `RefreshTests`, `AttemptLogTests` (ACT-1..5, 9..12, 29).
- [ ] **S6** Email verification (send, verify, resend, change email) and password reset (identical answers, single use, revoke other sessions); `ICustomerSignedInHook` called after sign-in and registration. *Proof:* `EmailVerificationTests`, `PasswordResetTests`, `SignedInHookTests` (ACT-6..8, 13, 14, 27, 28).
- [ ] **S7** Endpoints under `/api/auth`, rate limits, matrix rows, start-up checks (production refuses the stand-in handler). *Proof:* `AuthEndpointsTests`, `AuthorizationMatrixTests`, `StartupGateTests` (ACT-15, 26).
- [ ] **S8** `Policies.Customer` backed by the session handler (the stand-in stays for Development/Testing); ownership tests unchanged. *Proof:* `CustomerEndpointsTests`, `CustomerOwnershipArchitectureTests` (ACT-18).
- [ ] **S9** Storefront `core/customer-auth` store and `features/customers/sign-in`: sign-in, register with live password checklist and code step, forgot and set-new-password, verify-email result page; Turnstile widget; `/account` guard; header account link and sign out. *Proof:* component and store specs (ACT-2, 16, 29).
- [ ] **S10** Unverified-email notice bar with "Verify my email" button (design system; raise it if no component fits). *Proof:* component spec (ACT-31).
- [ ] **S11** `server.ts` pass-through grows to the auth routes with cookies; e2e journeys (register, verify, sign in, reset, throttle) and axe; spec "As built"; rebuild docker; real Twilio and Turnstile check once the owner has the accounts. *Proof:* `sign-in.e2e.spec.ts`.

## wishlist

From [wishlist/plan.md](wishlist/plan.md). Two things the plan assumes do not exist yet: `IStorefrontCatalogQueries` (Storefront.Contracts has only `IStorefrontPageQueries`) and an `ICustomerSignedInHook` (sign-in is parked). W2 adds the first; W7 builds the merge endpoint only and the hook waits for the auth sprint.

- [ ] **W1** `WishlistItem` and `WishlistFlags` (pure: saved price, current price, availability to `PriceDropped`, `OnSale`, `SoldOut`, `Unavailable`), `MaxWishlistItems` option validated at start. *Proof:* `WishlistFlagsTests`, `WishlistItemTests` (WSL-4, WSL-5, WSL-6).
- [ ] **W2** Storefront contract `IStorefrontCatalogQueries.GetProductSummariesAsync(ids)`: one bulk call, DTOs only (id, slug, name, price, was-price, availability, image, link path); unpublished ids are returned as absent. *Proof:* `StorefrontCatalogQueriesTests` in Storefront integration tests, including a query-count assertion (WSL-5, WSL-8).
- [ ] **W3** `CUST_WSHL` migration (`UQ_CUST_WSHL_CUST_ID_PRDT_ID`, index on `CUST_ID`, `SAVD_PRIC_AMT` numeric(12,2)); glossary words (`WSHL`, `SAVD`, `PRIC`, `AMT` if missing). *Proof:* `WishlistMigrationTests`, `NamingConventionTests`, `GlossaryTests`.
- [ ] **W4** Commands: add (idempotent `INSERT ... ON CONFLICT DO NOTHING`, price taken from the read model, 200 cap) and remove by product id. *Proof:* `WishlistCommandTests` (WSL-1, WSL-4, WSL-7).
- [ ] **W5** Queries: paged list (summaries in one contract call, no N+1, page size 1..100) and ids (max 200). *Proof:* `WishlistQueryTests` incl. query count (WSL-5, WSL-6, WSL-8).
- [ ] **W6** Endpoints `GET/PUT/DELETE /api/customers/me/wishlist...`, ownership by filter, matrix rows. *Proof:* `WishlistEndpointsTests.AnotherCustomersItem_Is404AndUnchanged` (WSL-7), authorization matrix test.
- [ ] **W7** Merge command and `POST .../wishlist/merge` (ids only, at most 50, unknown or unpublished skipped, repeat-safe). *Proof:* `WishlistMergeTests` (WSL-3).
- [ ] **W8** Architecture tests still green (`CustomerOwnershipArchitectureTests` covers the new table); extend the `/customer-api` pass-through allow-list in `server.ts` for the wishlist routes. *Proof:* `rt.sh Romp.ArchitectureTests`, storefront server spec or e2e.
- [ ] **W9** Storefront `features/customers/wishlist`: store, service, `WishlistHeartComponent` (button, `aria-pressed`, named for product and state), guest prompt dialog with the `localStorage` key (at most 50 ids), `/wishlist` page, header link with count, `noindex`, `no-store`. *Proof:* component, store and service specs (WSL-1, WSL-2, WSL-9).
- [ ] **W10** Wire the heart into the product page and listing tiles without breaking the shared edge cache (state fetched client-side after load). *Proof:* product-page and listing specs still pass; heart renders off on the server.
- [ ] **W11** E2E journey and axe scan; spec "As built"; rebuild docker. *Proof:* `wishlist.e2e.spec.ts` (WSL-1, WSL-9).

## newsletter, notify-me

Tasks are added when their turn comes, from their feature plans, in that order (module plan, "Order of work").

## Decisions taken on defaults

Recorded as the work reaches them (Q-ADD-1 provinces and cities seed list, Q-ADD-2 fields, Q-ADD-3 limit of 10, Q-ACT-7 phone format). Each is a data or configuration change, not a code change, if the owner chooses otherwise.

- **Q-ADD-1 (A1):** seven provinces and territories and 27 cities (the original 7 plus 20 main cities) are seeded. `CITY_LKP.PROV_ID` is nullable, not required: the admin Reference Data screen creates a city with no province, and a city without one is simply not offered to customers. Provinces are read-only in the admin API (a migration adds one); assigning a province to an admin-added city has no screen yet, so it is a data fix until the owner asks for one.
- `GET /api/geography/*` are anonymous and `public, max-age=3600`; the prefix is on the authorization-matrix anonymous list.
