# SCRUM-202: Health probes, logging policy and security-event logging

- **Status:** Approved (owner, 2026-10-16)
- **Jira:** SCRUM-202
- **BRD sections:** §11.6, §11.8
- **Requirement IDs:** SEC-22, SEC-25, NFR-15, SEC-26 (groundwork only; alerting is S18)

## Problem & intent
The deploy pipeline and any future load balancer need a truthful answer to "is this service alive, and ready for traffic?". Logs must be useful to find a fault yet never leak a password, a token or a child's record. Security-relevant events must be distinguishable from routine noise so alerting can be added later without rewriting the code.

## Requirements (quoted from the BRD)
> **SEC-22:** Health-check and readiness-probe endpoints shall be implemented for all deployed services to support automated recovery and load-balancer routing decisions.

> **SEC-25:** Application logs shall explicitly exclude passwords, full card data, and full Child Profile records; a documented logging policy shall define what is safe to log versus what must be redacted/masked.

> **SEC-26:** Security-relevant events (failed admin logins, webhook signature failures, repeated IDOR-blocked requests, rate-limit triggers) shall be logged and alerted on distinctly from routine application errors.

> **NFR-15:** Login attempts shall be logged and rate-limited to mitigate brute-force/credential-stuffing.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| AC-1 | Given the API, then `/health/live` returns 200 whenever the process runs (no dependency checked), and `/health/ready` returns 200 only when PostgreSQL answers on every module connection, all module migrations are applied, and the outbox dispatcher has run within its expected interval; otherwise 503. | SEC-22 |
| AC-2 | Given Redis is unreachable, then `/health/ready` stays 200 with a `Degraded` state (ADR 0003: the app falls back to the database), and the degraded state is logged. | SEC-22, ADR 0003 |
| AC-3 | Given the public probe responses, then they contain only the overall state word; per-check detail is available to callers with `ops.admin` at `/api/admin/health` and never to anonymous callers. | SEC-22 (new) |
| AC-4 | Given every deployed container (API, admin web, PostgreSQL, Redis), then each has a container health check, and the compose files start dependants only when their dependencies are healthy. | SEC-22 |
| AC-5 | Given `docs/policies/logging.md`, then it states what is safe to log (request id, route template, status, duration, user name of staff, entity ids, outcome codes), what is masked (phone numbers, email addresses, IP addresses beyond security events), and what is forbidden in any log, event, error or audit entry (passwords, hashes, tokens, cookies, MFA secrets and codes, card data, full Child Profile records). | SEC-25 |
| AC-6 | Given the logging pipeline, then a redaction step masks the values of any property or query-string key named like `password`, `secret`, `token`, `authorization`, `cookie`, `code`, `card`, and request and response bodies are never logged; a test logs sample secrets through every logging path and asserts they never appear. | SEC-25 |
| AC-7 | Given the API, then logs are structured JSON on standard output with timestamp, level, category, request id, route template and staff user name when known; exceptions carry stack traces in logs only, while clients receive problem details without internals. | SEC-25 |
| AC-8 | Given each request, then it has a correlation id returned in a response header, present in its log lines, and copied into audit entries ([audit-trail](../../identity/audit-trail/spec.md) AC-2). | (new) |
| AC-9 | Given a failed sign-in, a failed MFA code, a reused refresh value, a rate-limit rejection, a 403 caused by a missing permission, or an anonymous call to a secured route, then a log event is written under the dedicated category `Romp.Security` with a stable event id and outcome code, and no secret. | SEC-26, NFR-15 |
| AC-10 | Given the staging VPS, then an external uptime check polls `/health/ready` every minute and the owner is told when it fails for 3 minutes. | SEC-22 (new) |
| AC-11 | Given the architecture tests, then logging calls that interpolate objects marked as personal or secret fail the build (the marker attributes are introduced here and used by later sprints). | SEC-25 (new) |
| AC-12 | Given the nightly backup, then it pings a monitoring check when it succeeds (a dead-man's-switch), and the owner is notified if no success arrives within 25 hours or the script reports failure. | NFR-07, owner 2026-10-16 |
| AC-13 | Given the dead-letter messages of the outbox, then their count is visible on the admin Operations page and in `/api/admin/health`, and the staging uptime check also fails when the dead-letter count is above a configured threshold. | owner 2026-10-16 |

## Non-functional constraints
- `/health/live` answers in under 50 ms; `/health/ready` under 1 s and cached for 5 s so probes cannot load the database.
- Log volume at default level stays below 1 MB per hour of idle running.

## Out of scope
- Alert routing, dashboards and security alerts (S18, SEC-26 completion).
- Metrics and traces export (OpenTelemetry, Prometheus, Grafana from ADR 0003): see the open question.
- Log shipping to a central store.

## Open questions
- [x] ~~Prometheus, Grafana~~ Decided: wait for S18. Sprint 8 ships the uptime monitor on `/health/ready` (AC-10), the backup-failure signal (AC-12) and the visible dead-letter count (AC-13).
- [ ] Which external uptime and dead-man's-switch service (any free tier is assumed; only the staging URL and a ping URL are shared). Owner picks the account.
