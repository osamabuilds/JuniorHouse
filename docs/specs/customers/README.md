# Customers

Customer accounts for the storefront (Sprint 10, "Sell: customers"): registration and sign-in, password reset, saved addresses, newsletter consent, wishlist, basic Child Profiles and Notify Me. Nothing here takes an order (cart is S11, orders S12, order history S15).

**Status:** Specs approved (owner, 2026-10-25); [module plan](plan.md) and six feature plans drafted 2026-10-25, awaiting approval; ADRs [0021](../../adr/0021-customers-module-and-stand-in-customer.md) and [0022](../../adr/0022-email-sender-port.md) proposed · **Schema:** `CUST` · **Code:** `src/api/src/Modules/Customers/` (planned) · **Web:** `features/customers/` in the storefront app (planned) · **Jira:** sources SCRUM-59 (FR-38), SCRUM-60 (FR-39, SEC-02 reconcile), SCRUM-61 (FR-40), SCRUM-62 (FR-41), SCRUM-37 (FR-17), SCRUM-38 (FR-18), SCRUM-41 (FR-21), SCRUM-31 (FR-11), SCRUM-122 and SCRUM-199 (SEC-02), SCRUM-125 (SEC-05); a sprint umbrella ticket is created on approval · **Rules:** [constitution](../../CONSTITUTION.md), [CONVENTIONS](../../CONVENTIONS.md), [CLAUDE.md](../../../CLAUDE.md) · **Design:** `docs/design-system/` `AuthScreens`, `AccountPages`, `WishlistPage` (storefront only)

## Features

| Feature | What it does | Requirements | Status |
|---|---|---|---|
| [accounts](accounts/spec.md) | Register, sign in, sign out, password reset, failed-login protection | FR-38, FR-39, SEC-02, SEC-03, SEC-04, NFR-15, SEC-25 | Draft |
| [addresses](addresses/spec.md) | Saved address book with a default, selectable at checkout later | FR-40, SEC-05 | Draft |
| [newsletter](newsletter/spec.md) | Subscribe and unsubscribe, honoured on every future send | FR-41 | Draft |
| [wishlist](wishlist/spec.md) | Save products, guest prompt with the item kept, sale and stock flags | FR-17, SEC-05 | Draft |
| [child-profiles](child-profiles/spec.md) | Create, edit, delete profiles; owner-only; export and erase | FR-18, FR-21, NFR-27, NFR-29, SEC-05, SEC-25 | **Parked** (owner, 2026-10-25): not in Sprint 10, not built until the owner says |
| [notify-me](notify-me/spec.md) | Back-in-stock capture on sold-out variants and the restock email | FR-11 | Draft |

Order of work (proposed): accounts → addresses → wishlist → newsletter → notify-me (child-profiles parked). Accounts come first because everything but Notify Me and the guest newsletter signup needs a signed-in customer.

## Owner decisions (2026-10-25)

- **Child Profiles are parked.** Spec and plan are kept; nothing is implemented, and no table, endpoint, screen or menu entry is created, until the owner says. Sprint 10 builds five features.
- **Login and security come last**, with Sprint 8. Sprint 10 runs on a development-only stand-in customer (`ICurrentCustomer`); registration, sign-in, reset, throttling, CAPTCHA and the FR-39 vs SEC-02 decision all move to the final auth sprint. Ownership checks (SEC-05) are still built and tested now.
- **Child Profiles drop height and weight** (no Fit Finder). Logged as a deviation from FR-18.
- **Email sender: recommendation accepted** (D-3).

## Module decisions to confirm

These are proposals, not decisions. Each is also an open question in the feature spec that needs it.

| ID | Proposal |
|---|---|
| D-1 | **New module `Customers` (schema `CUST`)**, separate from `Identity`. Identity (S8) is parked and covers staff only; customers have a different lifecycle, volume and privacy class (minors' data). The two share no tables. Session mechanics are written in `Customers` now and lifted into BuildingBlocks only if Identity later wants the same code. Needs an ADR in the plan (new module, ADR 0002). |
| D-2 | **Customer sign-in follows SEC-02** (progressive throttling plus CAPTCHA, never a hard lock). This is the BRD's own recommendation and CLAUDE.md lists FR-39 vs SEC-02 as undecided, so the spec carries both readings until you answer Q-ACT-1. |
| D-3 | **A small `IEmailSender` port with an SMTP adapter** (Mailpit locally) is added now, because password reset, Notify Me and newsletter confirmation all need to send email and no sender exists. Messages are written through the outbox (ADR 0004). Guaranteed delivery, retry, DLQ, WhatsApp and resend from admin stay in S13. |
| D-4 | **The account area is `noindex` and blocked in `robots.txt`** (SEO-05); every customer endpoint takes the customer id from the session, never from the URL or body (SEC-05). |

## Requirement IDs for the module

FR-11, FR-17, FR-18, FR-21, FR-38, FR-39, FR-40, FR-41, SEC-02, SEC-03, SEC-04, SEC-05, SEC-25, NFR-15, NFR-26, NFR-27, NFR-29, NFR-17, NFR-19, SEO-05. Test cases: TC-ACC-01..17, TC-PDP-05..07. Criterion IDs are prefixed by feature: `ACT-`, `ADD-`, `NLS-`, `WSL-`, `CHD-`, `NTM-`.

## Out of scope for the sprint

Order history and guest tracking (S15, FR-36/37, SEC-06/07); cart merge on login (S11, FR-35: the sign-in feature exposes the hook); checkout address selection (S12); loyalty, referral and shareable registry (P2); Fit Finder, growth prediction and measurement history (descoped); staff sign-in (S8).

## Open questions that block the plans

- [x] ~~Q-ACT-1/2 and the other Q-ACT questions~~ Deferred to the final auth sprint.
- [x] **Q-MOD-1 (owner, 2026-10-25): recommendation accepted**, specified as NTM-11..13 in [notify-me](notify-me/spec.md). Original proposal: **one read-only screen, "Notify Me demand"**: sold-out variants with the number of people waiting, newest first. It needs no personal data on screen, helps buying and reorder decisions, and is the only S10 data the back office can act on. Skip the newsletter export (no campaigns exist) and never show Child Profiles to staff. If you would rather have no admin screen, say so and S10 stays storefront-only.
- [ ] **Q-MOD-2** Does the feature order above suit you, and is D-1 (separate `Customers` module) accepted?

## Parked for Sprint 8 (owner, 2026-10-25)

Anything that only matters once there is a server, real sign-in or real roles waits for S8 and is done there:

- The start-up gate that refuses a non-development host while customer sign-in is the stand-in (ACT-17, ADR 0021).
- Production-only configuration checks: `LinkSigningKey` must be set, SMTP must not be the local Mailpit address.
- Real roles on `Policies.CustomerDemand` (Admin-equivalent until then) and the audit-trail integration.
- All sign-in and security work already deferred to the auth sprint: registration, sign-in, reset, throttling, CAPTCHA, the FR-39 vs SEC-02 decision, cookie forwarding through the SSR server (SEC-03, SEC-04), the registration newsletter checkbox, and the column-encryption review for Child Profile data.
- The SSR rate limiter shared with `seo` (already deferred), so public Notify Me and newsletter endpoints rely on the API `strict` limit for now.
