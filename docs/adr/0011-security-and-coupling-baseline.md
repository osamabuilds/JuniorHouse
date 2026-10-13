# 0011 — Security and coupling baseline, and the pre-authentication deployment gate

- Status: Accepted (implemented in SCRUM-198)
- Date: 2026-10-14

## Context

Two properties are required of every feature: modules stay loosely coupled (ADR 0002), and there are no security holes. Today the rules are scattered across `CLAUDE.md` (SEC-05, SEC-08, SEC-09, SEC-10, SEC-25) and are checked by review only. A review of the API host (`Romp.Api/Program.cs`) on 2026-10-14 found:

1. No authentication or authorization anywhere. This is a deliberate scope decision (staff login is S8), but nothing prevents an unauthenticated build from being deployed. `/api/admin/outbox` exposes operational data to anyone who can reach the host.
2. No rate limiting, no request-size limits beyond Kestrel defaults, and no security response headers.
3. No place where a feature states its threats; security thinking depends on whoever writes the plan.

## Decision

### 1. Deployment gate (closes the "forgot to secure it" loophole)

- Every endpoint group is registered with an authorization policy name (`Policies.Staff`, later `Policies.Admin` and so on) through one helper, `MapSecuredGroup(...)`, never as bare `MapGroup`. Until S8, the `Staff` policy is satisfied by a **development-only** handler.
- The host refuses to start (`InvalidOperationException` at startup) when the environment is not `Development` or `Testing` and no real authentication scheme has been registered. S8 registers the scheme and the gate then lets production start. A deployment without login therefore fails loudly.
- An architecture test fails when an endpoint is mapped without a policy.

### 2. Security baseline: required of every feature, and written in the plan

Each plan has a "Security & coupling review" table covering at least:

| Area | Rule |
|---|---|
| Authorization | Every endpoint declares a policy; ownership is checked server-side where a resource belongs to a caller (SEC-05); role checks are never client-side (SEC-08) |
| Input | Every request is validated for type, range, length and character set before it reaches a handler; request DTOs are separate from entities (no mass assignment) |
| Output encoding | Any text placed into another language (ZPL, PDF, SQL, HTML, a file path) is escaped or encoded for that language; raw SQL is parameterised and quotes every identifier |
| Outbound calls | A feature never connects to an address taken from a request; outbound hosts come from configuration and are allow-listed with timeouts (SSRF) |
| Resource limits | Maximum sizes and counts on bodies, collections, page size (1..100) and any queue; expensive endpoints are rate limited |
| Concurrency | Uniqueness and state transitions are enforced in the database (unique and partial unique indexes, conditional updates, concurrency tokens), not by check-then-act in code |
| Idempotency | Every event consumer is inbox-guarded; commands that can be retried by a client are safe to repeat |
| Audit | State changes record who (acting person plus actor), when and why, in append-only history; free-text actor and reason fields are length-limited and stripped of control characters (no log injection) |
| Data | Money is `decimal`; no personal data in events, logs or errors (SEC-25); child data rules (NFR-27..30) |
| Secrets and config | No secret in source; dangerous configuration (test prefixes, dev handlers) is rejected at start-up in production |
| Errors | Problem-details responses only; no stack traces, SQL or internal ids beyond what the caller needs |
| Dependencies | New packages are added in `Directory.Packages.props` only, pinned, MIT/Apache-class licences, named in the plan |

### 3. Host hardening (one change in `Romp.Api`, done with S5's cross-cutting group)

- `AddRateLimiter` with a default fixed-window policy and a stricter policy for expensive endpoints.
- Kestrel `MaxRequestBodySize` set explicitly (small default, a larger limit only on upload endpoints).
- Security headers on every response: `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY` (the admin app sets its own CSP in nginx).
- `/api/admin/outbox` moves into a `Policies.Admin` group.
- CORS stays an explicit origin allow-list from configuration.

### 4. Coupling rules (additions to ADR 0002)

- A module consumes another module's events through a mapper (an anti-corruption layer) in its own `Application` project, so a change to the other module's contract touches one file.
- Event and contract changes are additive within a major version; breaking changes publish a new schema version alongside the old one.
- Domain projects reference nothing outside the building-blocks domain project (no EF Core, ASP.NET Core, MediatR, or other modules). The architecture tests enforce it.
- Cross-module reads go through query interfaces in `*.Contracts` that return DTOs shaped for the caller (no entities, no `IQueryable`).
- A module is replaceable: its public surface is its `Contracts` project plus its endpoints, nothing else.

## Consequences

- Sprint 5 carries a small cross-cutting task group (gate, rate limiter, headers, admin policy, tests). It is separate from Labelling's own code so it can be reviewed on its own.
- Every future plan has a security table that a reviewer can check, instead of relying on memory.
- Local development is unchanged: the dev handler allows everything in `Development` and `Testing`.
- The gate means S8 (login) is a hard prerequisite for any deployment, matching `CLAUDE.md` today.
