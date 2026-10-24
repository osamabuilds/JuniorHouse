# Platform (Sprint 8): tasks

Order of work and the owner actions that gate it. Feature steps are in [observability/tasks.md](observability/tasks.md) and [deployment/tasks.md](deployment/tasks.md). Identity steps are in [../identity/tasks.md](../identity/tasks.md). Jira: SCRUM-202.

## Owner actions (needed before the Friday deploy; tracked in `deploy/README.md`)

| By | Action |
|---|---|
| Monday | Confirm what is already provisioned (VPS, Cloudflare account, GitHub environment, bucket). |
| Before Tuesday | Register the neutral staging domain (not the brand name) and put it on Cloudflare. |
| Wednesday | Hetzner account (MFA on) and an `admin.<domain>` DNS record; Cloudflare R2 bucket and write-only token; `age` key pair (private key kept offline); GitHub `staging` environment with required reviewer and environment-scoped secrets; an uptime and dead-man's-switch monitor account. |

Nothing here is done with credentials given to Claude: the owner provisions and holds every account; any command that needs a root or provider credential is given to the owner to run.

## Order

1. Observability steps O1 to O6 (code, test-first) alongside identity on Tuesday and Wednesday.
2. Deployment steps D1 to D7 (files, scripts and the pipeline, rehearsed against a local compose run) on Wednesday and Thursday.
3. D8 to D12 (real staging) as soon as the owner actions are done; Friday at the latest.
4. Close the loop: cards Implemented, specs updated, README "Try Sprint 8", the logging policy and deployment runbook linked.

If an owner action slips, everything up to D7 still ships and the staging deploy (D8 onward) moves to the next day. The sprint is not reported done until a real staging deploy and a tested restore exist.
