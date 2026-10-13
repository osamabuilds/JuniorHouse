# <JIRA-KEY>: Technical plan

- **Spec:** [spec.md](spec.md)
- **Module(s):** <Catalog | Orders | …>

## Domain model
Aggregates, entities, value objects and the rules (invariants) each one must keep.

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `CreateXCommand` | … | … | AC-1, FR-xx |

## API endpoints
| Method | Route | Auth / role | Ownership check (SEC-05) |
|---|---|---|---|

## Events
| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|

## Data
Schema and table changes, indexes, concurrency control, and migration notes.

## NFR / security design
How each constraint from the spec is met (caching, rate limiting, idempotency, redaction, …).

## Frontend
Components and routes affected in the storefront and/or admin app. SSR and SEO notes.

## Risks & alternatives considered
If a choice here sets precedent for other features, link or create an ADR.

## Security & coupling review (required; a plan without it is not approved)
Rules and rationale: [ADR 0011](../../adr/0011-security-and-coupling-baseline.md) and constitution section 6. One row per area; write "n/a" with a reason, never leave a row out.

| Area | How this feature meets it |
|---|---|
| Authorization (policy per endpoint, ownership SEC-05, roles SEC-08) | |
| Input validation (type, range, length, charset; DTO ≠ entity) | |
| Output encoding (ZPL, PDF, SQL, HTML, file paths) | |
| Outbound calls (config-only, allow-listed, timeouts) | |
| Resource limits (sizes, counts, page size, queue length, rate limits) | |
| Concurrency (constraints, conditional updates, concurrency tokens) | |
| Idempotency (inbox, retried commands) | |
| Audit (who, when, why; append-only; log-injection safe) | |
| Data protection (money as decimal, no personal data in events/logs/errors, child data) | |
| Secrets and dangerous configuration | |
| Error responses (problem details only) | |
| New dependencies (name, version, licence) | |
| Coupling (contracts used, mapper for consumed events, ports and fakes, no cross-module internals) | |
| Abuse cases considered (at least three: what would a hostile or careless user do?) | |
