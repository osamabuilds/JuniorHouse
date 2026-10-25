# Customers: module design

- **Module README:** [README.md](README.md) · **Features:** [accounts](accounts/plan.md) · [addresses](addresses/plan.md) · [child-profiles](child-profiles/plan.md) (parked, not built) · [wishlist](wishlist/plan.md) · [newsletter](newsletter/plan.md) · [notify-me](notify-me/plan.md)
- **ADRs:** [0021 Customers module and stand-in customer](../../adr/0021-customers-module-and-stand-in-customer.md) · [0022 email sender port](../../adr/0022-email-sender-port.md) (both new, proposed) · [0004 outbox](../../adr/0004-postgres-transactional-outbox.md) · [0011 security and coupling](../../adr/0011-security-and-coupling-baseline.md) · [0018 storefront edge](../../adr/0018-storefront-module-read-model-and-edge.md)

Shared design for the six features (child-profiles is parked by the owner and not built in Sprint 10); each feature plan holds its own domain, use cases, endpoints and security review.

## Module layout

`Romp.Modules.Customers.{Domain,Application,Infrastructure,Contracts}`, tests `Romp.Modules.Customers.Tests`. `CustomersModule : IModule`, DbContext `CustDbContext` (schema `CUST`), `AddOutboxModule("CUST", …)`, inbox `CUST.INBX`. Registered like `Storefront`: add `Customers` to `ModuleNames` in `ModuleBoundaryTests`, the `modules` array and MediatR assembly list in `Program.cs`, `Romp.slnx`, and the constitution and `docs/db/naming.md` §4 tables (the schema row already reads `CUST`).

| Feature (same name in every tree) | API folder | Storefront web | Admin web |
|---|---|---|---|
| accounts | `Accounts/` | `features/customers/accounts` (account shell, dev switch) | n/a |
| addresses | `Addresses/` | `features/customers/addresses` | n/a |
| child-profiles (**parked**) | `ChildProfiles/` | `features/customers/child-profiles` | n/a |
| wishlist | `Wishlist/` | `features/customers/wishlist` | n/a |
| newsletter | `Newsletter/` | `features/customers/newsletter` | n/a |
| notify-me | `NotifyMe/` | `features/customers/notify-me` (form on the product page) | `features/customers/notify-me` (demand list) |

Shared code that belongs to no single feature sits in `Abstractions/` (`ICurrentCustomer`, `ICustomerDbContext`, `ICustomerEmailQueue`) and `Emails/` (templates). Features do not call each other except where stated: `accounts` provides `ICurrentCustomer`; `wishlist` registers a sign-in hook with `accounts`; everything else is independent. An architecture test (`CustomersFeatureDependencyTests`, copying the Warehouse one) enforces this and that Domain stays pure.

## Order of work and why

1. Foundations: module skeleton, `CUST_MAIN`, `ICurrentCustomer`, dev handler, gate, email port (ADR 0022). 2. `addresses` (needs the Reference geography contract). 3. `wishlist` (needs the Storefront catalog contract). 4. `newsletter` (uses the email port). 5. `notify-me` (stock event + email + admin screen). 6. Storefront chrome (account and wishlist links, footer newsletter form) and e2e/accessibility pass.

## Cross-module contracts (all additive)

| Need | Provider | Change |
|---|---|---|
| Province and city lists; "is this city in this province" | `Reference.Contracts.IGeographyQueries` (new). Reference gains `PROV_LKP`, `CITY_LKP.PROV_ID` and the seed | additive migration; existing `CITY_LKP` users unaffected |
| Size labels for "usual size" | `Reference.Contracts.IApparelCodeQueries` (exists) | none |
| Product summaries (name, slug, price, compare-at, availability, image) by product id; variant summaries (product name, colour, size, slug, sold out) by variant id | `Storefront.Contracts.IStorefrontCatalogQueries` (new; reads the read model, bulk, max 100 ids per call) | new interface in a project that already exists |
| Stock changed | event `StockLevelChangedEvent` (existing v1, `Warehouse.Contracts`) | none |
| Subscribed addresses (for a future campaign sender) | `Customers.Contracts.INewsletterAudience` (new) | new, no consumer yet |

The one file that knows the shape of the Warehouse event is `NotifyMe/StockChangedMapper.cs`.

## Events

| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|
| `StockLevelChangedEvent` v1 (existing) | Warehouse | notify-me | yes (existing) |
| `CustomerEmailRequestedEvent` v1 (`Kind`, `RelatedId`; ids only) | notify-me, newsletter | the Customers email handler (ADR 0022) | yes |

No event carries a name, address, email, phone or child field (ADR 0011, SEC-25). Every event with no registered consumer gets a logging handler, as elsewhere.

## Data (schema `CUST`, names per `docs/db/naming.md`)

| Table | Owner | Notes |
|---|---|---|
| `CUST_MAIN` | accounts | customer record (`EML`, names, `PHON`); the auth sprint adds credential tables beside it |
| `CUST_ADDR` | addresses | up to 10 per customer |
| `CUST_CHLD` | child-profiles (**parked**; no migration until unparked) | child profile, up to 8 per customer |
| `CUST_WSHL` | wishlist | one row per customer and product |
| `NWSL_SUBS`, `NWSL_HIST`, `NWSL_STS_LKP`, `NWSL_SRC_LKP`, `NWSL_ACTN_LKP` | newsletter | consent record and its append-only history |
| `NTFM_REQ`, `NTFM_STS_LKP` | notify-me | requests per variant and email |
| `OUTB_MSG`, `INBX` | module | |

Every table has the standard audit columns and `xmin`; foreign keys are declared and indexed; no foreign key leaves the schema (product, variant, province, city and size ids are plain references). No table needs partitioning at the expected volume (tens of thousands of customers); `NTFM_REQ` is purged by a job (feature plan).

**New glossary words**, added to `docs/db/naming.md` in the same PR. Words that already exist and are reused: `CUST`, `ADDR`, `EML`, `PHON`, `NAME`, `LINE`, `CITY`, `DFLT`, `IND`, `STS`, `PRDT`, `VRNT`, `PRIC`, `AMT`, `SIZE`, `HIST`, `DTE`, `DT`, `ACT`, `REQ`, `LKP`, `SORT`, `SEQ`, `MAIN`, `ITEM`, `MON`, `CODE`, `SENT`, `HASH`. New: `CHLD` child, `NICK` nickname, `BRTH` birth, `USUL` usual, `WSHL` wishlist, `NWSL` newsletter, `SUBS` subscription, `NTFM` notify-me (`SRC` "source" is added by Sprint 8), `PROV` province, `PCDE` postcode, `RCPT` recipient, `FRST` first, `ONE`, `TWO`, `SAVD` saved, `NTFD` notified, `EXPD` expired, `SUBD` subscribed, `UNSB` unsubscribed, `ACTN` action, `OPEN` (exists). (`LAST` and `NAME` exist.)

## Policies, configuration and the web edge

- `Policies.Customer` (signed-in customer; development handler only until the auth sprint, ADR 0021), `Policies.CustomerDemand` (admin demand list; capability-shaped, Admin-equivalent until S8), `Policies.Public` (Notify Me, newsletter subscribe and unsubscribe). Rate-limit policy `strict` on every public write.
- Options `Customers:*`, validated at start-up: `MaxAddresses` 10, `MaxWishlistItems` 200, `MaxNotifyPerVariant` 5000, `NotifyOpenDays` 90, `NotifyClosedDays` 30, `LinkSigningKey` (secret; a missing or default value stops the host outside Development), `Email:*` (ADR 0022).
- **Storefront SSR:** account pages render with `Cache-Control: private, no-store` and `noindex`; `robots.txt` disallows `/account`, `/wishlist` and `/newsletter`; the browser calls the API through same-origin `/customer-api/**` pass-through routes in the SSR server (confirmed against `server.ts` in task 1.4). Product and listing pages stay shared-cacheable: the wishlist heart is filled in by the browser after load.
- **Admin app:** one lazy route `customers/notify-me`.

## Module-wide security & coupling review

| Area | How the module meets it |
|---|---|
| Authorization | Customer endpoints in `MapSecuredGroup(..., Policies.Customer)`; admin list in `Policies.CustomerDemand`; public writes in `Policies.Public` + `strict`. Every owned-resource query filters by `CUST_ID` from `ICurrentCustomer` (ADR 0021 §3); architecture and API tests prove customer A cannot read or change customer B's rows (SEC-05) |
| Input | FluentValidation on every command; lengths, character sets and ranges per feature; DTOs separate from entities |
| Output encoding | Email templates HTML-encode every value; links built from slugs and signed tokens only; no raw SQL beyond quoted-identifier statements |
| Outbound calls | SMTP host from configuration only (ADR 0022) |
| Resource limits | Counts per customer (above), page size 1..100, notify cap per variant, request body limits, `strict` rate limit on public writes |
| Concurrency | Unique and partial unique indexes (one default address, one open notify request per email and variant, one wishlist row per product); limits enforced under a row lock on `CUST_MAIN` |
| Idempotency | Inbox on the stock consumer and the email handler; wishlist add, subscribe and notify are repeat-safe by unique indexes |
| Audit | Standard audit columns; `NWSL_HIST` append-only consent history; no staff-visible personal data |
| Data protection | No personal data in events, logs or errors; child fields never logged (SEC-25); child rows hard-deleted on request; emails/phones are personal data (NFR-26) |
| Secrets and config | `LinkSigningKey` and SMTP password from secrets; dev handler registered only in Development/Testing; production-config start-up checks parked for S8 |
| Errors | Problem details only; 404 for other customers' ids; identical responses for known and unknown emails on public endpoints |
| Dependencies | MailKit (MIT) only, ADR 0022 |
| Coupling | Contracts and one consumed event through one mapper; one-way feature dependencies; no cross-schema foreign keys |

## Risks

- **Nothing here is safe to deploy** until the auth sprint replaces the stand-in; the gate in ADR 0021 enforces it.
- **Restock email before the read model catches up.** Customers and Storefront consume the same stock event in parallel, so for a few seconds the product page can still say sold out when the email arrives. Accepted: the email link works within seconds, and the SLA is minutes.
- **Mail clients prefetching the unsubscribe link** would unsubscribe someone silently. Accepted (it errs toward not mailing); the link is idempotent.
- **Guest wishlist before sign-in exists** can only be demonstrated with the dev switch; the merge endpoint and hook are tested directly.
- **Integration tests cannot load locally built DLLs here** (Application Control): run them in the Linux SDK container or CI and say so.
