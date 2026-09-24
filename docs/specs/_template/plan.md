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
