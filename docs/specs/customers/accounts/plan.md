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
