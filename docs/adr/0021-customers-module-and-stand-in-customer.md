# 0021 — Customers module, and a stand-in customer until sign-in is built

- Status: Proposed (plan for Sprint 10; spec approved by the owner 2026-10-25)
- Date: 2026-10-25

## Context

Sprint 10 adds addresses, a wishlist, newsletter consent, Child Profiles and Notify Me for storefront customers. Real customer sign-in, registration, password reset and failed-login protection are deliberately built last, with Sprint 8 (owner, 2026-10-25), and the FR-39 vs SEC-02 lockout question is open. Child Profiles are personal data about minors (NFR-27, NFR-29). The storefront browser talks only to the SSR server; the API is reachable only from it (ADR 0018, decision 4).

## Decision

1. **New module `Customers`**, schema `CUST`, projects `Domain`, `Application`, `Infrastructure`, `Contracts` (ADR 0002). It is separate from `Identity` (staff only, parked). The two share no tables. If `Identity` later wants the same session code, it moves to a building block then, not before.
2. **A stand-in customer for Sprints 10 onward, until real sign-in exists.**
   - `ICurrentCustomer` (`Romp.Modules.Customers.Application.Abstractions`) exposes `CustomerId` (and nothing else). Every customer endpoint and every query takes the customer from it, never from a route, query string or body (SEC-05).
   - `Policies.Customer` guards signed-in customer endpoints. Until sign-in exists it is satisfied only by a **development-only handler** (same pattern as the staff one, ADR 0011) that signs in a seeded test customer, or "nobody" when the request says so (`X-Dev-Customer: none`), so the guest flows can be exercised.
   - **Parked for S8 (owner, 2026-10-25):** extending the deployment gate (`SecurityGate`) so the host refuses to start outside Development/Testing while `Policies.Customer` is backed only by the development handler. Until S8 the project rule applies (nothing is deployed before S8, CLAUDE.md), and the dev handler is registered only in Development/Testing.
3. **Customer data is scoped by construction.** Every customer-owned table carries `CUST_ID`; every repository and query method on customer-owned data takes the `CustomerId` from `ICurrentCustomer` and filters by it in the same statement that reads or writes (`WHERE "ID" = @id AND "CUST_ID" = @cust`). A row belonging to someone else is indistinguishable from a missing one (404). An architecture test fails if a customer-owned entity is queried without the customer filter (global query filter plus a test over the endpoint list).
4. **Browser access.** The storefront SSR server exposes same-origin pass-through routes (`/customer-api/**`) to the API and attaches the dev header in Development only. No cookies are involved in Sprint 10; the real session mechanism (cookie forwarding, SEC-03/SEC-04) is decided by its own ADR in the auth sprint. Public (anonymous) endpoints Notify Me and newsletter subscribe use `Policies.Public` with the `strict` rate-limit policy, plus IP limits at Cloudflare and in the SSR layer (ADR 0018, decision 5).
5. **Cross-module reads go through contracts.** Customers reads product and variant display data from a new additive `Storefront.Contracts.IStorefrontCatalogQueries` (bulk by id, no per-row calls), city and province from a new `Reference.Contracts.IGeographyQueries`, and consumes Warehouse's `StockLevelChangedEvent` through one anti-corruption mapper. It publishes `INewsletterAudience` in its own Contracts for a future campaign sender.
6. **(Applies when Child Profiles are unparked; parked by the owner 2026-10-25.) Child data is deleted, not hidden.** A Child Profile row is deleted for good on request (FR-21, NFR-29); nothing in other modules references it. Logs and events never carry profile fields (SEC-25). Encryption at rest is disk-level for now; column-level encryption was evaluated and not adopted in Sprint 10 because the only sensitive columns are a nickname and a birth month, nothing can search on them, and the extension adds key management before there is a production host. Revisit in the auth sprint with the production database decision.

## Alternatives considered

- **Extend `Identity` with customers.** Mixes staff and customer lifecycles, volumes and privacy classes; Identity is parked and approved only for staff.
- **A header-driven fake user in every environment.** Rejected: the gate in decision 2 exists so a server can never run on a fake customer.
- **Build real registration and sign-in now.** Owner decision: last.
- **Soft-delete Child Profiles with a separate erasure job (BRD §12.9).** Doubles the mechanism for no benefit when no order references a profile.

## Consequences

- Sprint 10 can be demonstrated locally on the stand-in, but nothing customer-facing may be deployed before the auth sprint.
- The auth sprint replaces one handler and adds registration, sessions and reset; no feature code changes because they only know `ICurrentCustomer`.
- Reference and Storefront gain small additive contracts; neither has existing consumers affected.
