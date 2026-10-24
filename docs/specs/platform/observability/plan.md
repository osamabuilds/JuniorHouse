# SCRUM-202: Health probes, logging policy and security events, technical plan

- **Spec:** [spec.md](spec.md)
- **Area:** platform (code in `Romp.Api`, `BuildingBlocks.Web`, Dockerfiles, compose)

## Design

| Part | Design | Covers |
|---|---|---|
| Probes | `/health/live` (no checks). `/health/ready` runs checks tagged `ready`: one PostgreSQL check per module connection string (`SELECT 1`, 2 s timeout), a migrations check (pending migrations for each module context = unhealthy), a `OutboxHeartbeatHealthCheck` (the dispatcher records the time of its last pass in memory; stale beyond 3x interval = unhealthy), a Redis check registered as degraded-on-failure. Results cached 5 s. A custom response writer prints only `Healthy`, `Degraded` or `Unhealthy`. `/api/admin/health` (`ops.admin`) returns per-check detail and durations. | AC-1, AC-2, AC-3 |
| Container health | API image: `HEALTHCHECK` using a tiny `curl`-free probe (a `dotnet Romp.Api.dll healthcheck` verb calling `/health/live` on localhost). nginx admin: `/healthz` returns 200 from a static location. Postgres: `pg_isready`; Redis: `redis-cli ping`. Compose `depends_on: condition: service_healthy`. | AC-4 |
| Logging policy | `docs/policies/logging.md`: safe / masked / forbidden lists as in the spec; how to log (message templates, never interpolate objects); security-event catalogue; retention (staging: 7 days of Docker logs, rotated). | AC-5 |
| Redaction | A `RedactingLoggerProvider` wrapper over the built-in JSON console formatter: masks values of state keys matching the sensitive list, and `SecretString` and `[Sensitive]` types print `***`. HTTP logging stays at the default (no bodies, no headers); the request log line uses the route template, not the raw path with query. A test pushes sample secrets through `ILogger`, the exception handler and the request pipeline and asserts they never appear in captured output. | AC-6, AC-7 |
| Structured logs | `AddJsonConsole` with UTC timestamps; scope with `RequestId`, `CorrelationId`, `UserName`; no new package. | AC-7 |
| Correlation id | Middleware reads or creates `X-Correlation-Id` (validated: 8 to 64 chars `[A-Za-z0-9-]`), stores it in `HttpContext`, adds it to the response, log scope and (via `ICorrelationContext`) the audit collector. | AC-8 |
| Security events | `SecurityEvents` static class: stable event ids (1001 sign-in failed, 1002 MFA failed, 1003 refresh reuse, 1004 rate limit, 1005 forbidden, 1006 unauthenticated to secured route, 1007 session revoked by admin), category `Romp.Security`, source-generated `LoggerMessage` methods with typed parameters (no free text). Raised from the identity handlers, the rate limiter's `OnRejected` and an authorization-result middleware. | AC-9 |
| Uptime check | External free-tier monitor on `https://admin.<domain>/health/ready`; alert to the owner by email/phone push after 3 minutes; documented in `deploy/README.md`. | AC-10 |
| Backup signal | `pg-backup.sh` ends with a call to a dead-man's-switch check URL on success (URL from the server env file) and a fail URL on error; the monitor alerts the owner if no success within 25 hours. | AC-12 |
| Dead-letter visibility | `OutboxHealthReader` already counts dead-lettered messages per schema; the total is added to `/api/admin/health`, shown on the Operations page, and exposed as a count on a second, token-protected probe path `/health/dlq` that the uptime monitor can poll (state word plus count; fails above `Health:DeadLetterThreshold`). | AC-13 |
| Personal-data marker | `[Sensitive]` / `[PersonalData]` attributes in `BuildingBlocks`; an architecture test (Roslyn-free, reflection-based over `LoggerMessage` parameter types) fails when a marked type is a log parameter. Weaker than a full analyser and documented as such. | AC-11 |

## Data
No schema. Config: `Health:ReadyCacheSeconds`, `Health:OutboxStaleFactor`, `Logging:Redaction:Keys`.

## Frontend
The admin app footer shows nothing new. An "Operations" page (existing outbox view) gains a health panel for `ops.admin` using `/api/admin/health`.

## Risks & alternatives considered
- A readiness check that fails because one module's database is slow could pull the API out of rotation during a load spike. Mitigation: 2 s timeouts and the 5 s cache; there is a single API node on staging, so the practical effect is a warning, not an outage.
- A full logging framework (Serilog) would add destructuring policies; rejected for Sprint 8 to avoid a new dependency for a need the built-in provider meets (revisit with OpenTelemetry in S18).

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | Probes anonymous by explicit allow-list entry and state-word-only; detail endpoint `ops.admin`. |
| Input validation | Correlation id header validated; otherwise no input. |
| Output encoding | Log output is JSON-encoded by the formatter; user-supplied values only as typed parameters. |
| Outbound calls | The uptime monitor calls us; the application calls nothing new. |
| Resource limits | Ready checks cached and timed out; probe routes exempt from rate limiting but cheap; log rotation caps disk. |
| Concurrency | Heartbeat is a single timestamp updated atomically. |
| Idempotency | Not applicable. |
| Audit | Admin health reads are not audited (no state change); security events go to the log, and sign-in events also to `LGIN_ATMP`. |
| Data protection | Redaction tests; masking rules for phone, email, IP; forbidden list enforced by policy and review. |
| Secrets and dangerous config | Redaction key list configurable but start-up refuses to remove the built-in minimum list. |
| Error responses | Problem details; probes return a bare word. |
| New dependencies | None. |
| Coupling | Hosted in `BuildingBlocks.Web` and `Romp.Api`; modules contribute checks through `IModule` registration, not references to each other. |
| Abuse cases | (1) Probe endpoint scraped to learn internals: only a state word is public. (2) A developer logs a request body or a password in a hurry: redaction and the marker test catch known shapes; the policy covers the rest in review. (3) Log forging through user input: typed parameters, JSON encoding, charset limits. (4) Health endpoint used for load: cached for 5 s. |
