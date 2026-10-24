# Platform

Cross-cutting work that belongs to no business module: how the system is deployed, observed and kept healthy. It is not a module (no schema, no `Modules/` folder); its code lives in `Romp.Api`, `BuildingBlocks`, the Dockerfiles, `.github/workflows/` and `deploy/`. It is the same kind of work as the shared outbox dispatcher.

**Jira:** SCRUM-202 (same sprint umbrella as [identity](../identity/README.md)) · **Rules:** [constitution](../../CONSTITUTION.md), [ADR 0003](../../adr/0003-hosting-and-data-stack.md), [ADR 0005](../../adr/0005-github-actions-ci.md)

## Features

| Feature | What it does | Status |
|---|---|---|
| [deployment](deployment/spec.md) | A real staging environment (VPS, domain, Cloudflare) and a GitHub Actions deploy pipeline | Planned |
| [observability](observability/spec.md) | Liveness and readiness probes, logging policy with redaction, structured logs, security-event logging groundwork | Planned |

The Sprint 8 verification pass that every endpoint from Sprints 1 to 7 carries the correct permission is specified in [identity/roles](../identity/roles/spec.md) (AC-9 to AC-12), because it tests that module's policies.

`StructureSyncTests` currently expects every spec folder to match an API feature folder. `platform` has none, so Sprint 8 teaches that test that `platform` is a spec-only area (see the [deployment plan](deployment/plan.md)).
