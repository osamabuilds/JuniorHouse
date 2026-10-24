# SCRUM-202: Staging environment and deploy pipeline, technical plan

- **Spec:** [spec.md](spec.md) · **ADR:** [0017](../../../adr/0017-staging-topology-and-deploy-pipeline.md) (Proposed), [0003](../../../adr/0003-hosting-and-data-stack.md), [0005](../../../adr/0005-github-actions-ci.md)

## Repository additions

```
deploy/
  README.md                 provisioning checklist, runbook (first Owner, MFA reset, rollback, restore), costs
  bootstrap.sh              one-time server prepare: user, SSH hardening, firewall, updates, Docker, time sync, log rotation
  compose.staging.yml       proxy, api, postgres, redis (images by tag from env), healthchecks, volumes
  proxy/nginx.conf          TLS (origin certificate), HTTP redirect, static admin, /api proxy, security headers, noindex
  backup/pg-backup.sh       nightly dump, encrypted with age (public key on the server, private key held offline by the owner), uploaded to Cloudflare R2, keep 7, dead-man ping on success; restore.sh + notes
  latency/measure.sh        run from the owner machine against test servers in Singapore and EU (AC-17)
.github/workflows/
  deploy-staging.yml        build/scan/push/deploy/smoke-test/rollback (manual rollback input: image tag)
  scheduled-scan.yml        weekly dependency and image re-scan of main
docs/policies/logging.md    (from the observability plan)
```

## Pipeline (`deploy-staging.yml`)

1. Triggered by a successful `api` and `web` CI on `main` (and by `workflow_dispatch` with an optional image tag for rollback).
2. **build:** `docker build` the API image (`src/api`) and the admin image (`src/web`, `Dockerfile.admin` with the nginx config moved into the proxy), tagged `ghcr.io/<owner>/romp-api:<sha>` and `romp-admin:<sha>`.
3. **scan:** Trivy on both images, critical and fixable fails the run (SEC-24).
4. **push** to GHCR.
5. **deploy** job, environment `staging` (owner approval): `ssh` as the deploy user with a key from environment secrets; copy `compose.staging.yml`; write the runtime env file from secrets (`0600`); `docker compose pull`; run `docker compose run --rm api migrate`; `docker compose up -d`; wait up to 120 s for container health.
6. **smoke test** from the runner against the public URL: `/health/ready` 200; `/` serves the sign-in shell with `noindex`; `GET /api/auth/me` returns 401; `Strict-Transport-Security`, `X-Content-Type-Options` and `X-Robots-Tag` present.
7. **on failure:** the job re-points the compose file at the previous tag (kept in a `previous.env` file on the server), starts it, and reports the failure. Database migrations are written expand-then-contract, so the previous version still works against a migrated database for one release; a migration that cannot satisfy this is marked and requires a manual rollback plan in the PR.

## Application changes needed (small)

| Change | Why |
|---|---|
| `migrate` verb on `Romp.Api` runs every module's migrator and exits (non-zero on failure); start-up migration stays Development-only | AC-5 |
| `healthcheck` verb | Container health without installing `curl` in the image |
| `Dockerfile.admin` serves static files only; nginx proxy config in `deploy/proxy` | AC-3 |
| `API_BASE_URL` empty in staging; `ng serve` proxy file for local work | same origin |
| Forwarded-headers configuration from `Network:TrustedProxies` | sessions AC-15 |
| `Demo__SeedSampleData` false and `ASPNETCORE_ENVIRONMENT=Staging` | AC-10 |
| `SiteOptions` (`Site:PublicBaseUrl`, validated https outside Development) and one deploy variable `ROMP_PUBLIC_HOST` feeding: nginx `server_name` (envsubst template), `AllowedHosts`, `Cors:AllowedOrigins` (Development only), cookie domain (unset = host-only), `Site:PublicBaseUrl`, and the smoke-test URL. No hostname literal in code, images or committed config | AC-16 |
| `StructureSyncTests`: `platform` and `identity` spec areas are recognised (platform is spec-only; identity needs API folders to exist before its cards stop being `Planned`) | constitution |
| `web.yml`: add `npm audit --audit-level=critical`; `scheduled-scan.yml` | AC-14 |

## Provisioning checklist (owner actions, tracked in `deploy/README.md`)

1. Hetzner account (owner, MFA on). Staging uses the cheapest region; approve the monthly cost (target: about 2 vCPU / 4 GB / 40 GB). The latency script (AC-17) is run later, with hourly-billed test servers, to choose the production region.
2. Register the neutral staging domain (not the brand name) before Tuesday; create the Cloudflare zone; SSL mode Full (strict); origin certificate; proxied `A` record for `admin.<domain>`. Production domain is needed before launch and indexing, not this sprint.
3. Cloudflare R2 bucket for backups (private; a token that can only write the backup prefix) and an `age` key pair for backup encryption; private key kept offline by the owner.
4. GitHub environment `staging` with required reviewer; secrets scoped to that environment only (no repository-wide secrets for deploy). The deploy SSH key belongs to a non-root deploy user; no root credential is shared with any automation agent.
5. Run `bootstrap.sh`; first deploy; run `create-owner`; enrol MFA; sign in.

Everything the owner must supply is listed up front so the Friday deploy is not blocked by account creation; work that does not need it (pipeline, compose, proxy, scripts) is done Tuesday to Thursday and tested against a local compose run, which is a rehearsal, not the deliverable.

## NFR / security design
- TLS only; Cloudflare Full (strict); HSTS; origin firewall accepts Cloudflare ranges only (updated by a script run monthly and noted in the README).
- Least privilege: deploy user can run `docker compose` for this project only (no general sudo); the application connects to PostgreSQL with a role that cannot create schemas outside migration runs (a separate migration role is used by the `migrate` step).
- Secrets as in the spec (AC-9); the workflow never prints secrets (masking is verified by a dry run).
- Docker log rotation (`max-size`, `max-file`) in the compose logging options.
- Staging data is non-production: no real customer or child data is ever loaded into it.

## Risks & alternatives considered
- **Provisioning lead time** (accounts, DNS propagation, certificate): the earliest risk for Friday. Mitigation: ask the owner for the provider/domain decision on Monday.
- **Single VPS means a restart during each deploy**: accepted on staging.
- **Rollback after a destructive migration**: addressed by expand-then-contract and the marker rule above.
- **Cost:** a VPS plus domain plus a free uptime monitor and a small bucket; amounts recorded after the choice.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | Deploys need the GitHub environment approval; SSH by key to a deploy user; admin app protected by the Identity feature. |
| Input validation | `workflow_dispatch` image tag validated against `^[0-9a-f]{7,40}$` before use. |
| Output encoding | Shell steps quote variables; no user-controlled strings reach `eval`. |
| Outbound calls | Pipeline to GHCR, the VPS and the backup bucket only; the app makes none new. |
| Resource limits | Log rotation; compose memory limits for each container; backup keeps 7 dumps. |
| Concurrency | `concurrency: staging` group in the workflow so two deploys cannot overlap. |
| Idempotency | Re-running a deploy of the same tag is safe (compose converges; migrations are idempotent). |
| Audit | GitHub run history is the deployment audit; deploy user actions logged by SSH. |
| Data protection | Staging holds no real personal data; backups are encrypted in transit and the bucket is private. |
| Secrets and dangerous config | Environment secrets only; image builds contain none (`.dockerignore` checked); production-only settings refused at start-up. |
| Error responses | Not applicable. |
| New dependencies | GitHub Actions already used; Trivy action already used; pin new actions by commit SHA. |
| Coupling | No code coupling; only the `migrate` and `healthcheck` verbs in the host. |
| Abuse cases | (1) A stolen SSH key: key is restricted to the deploy user and command set, source IP limited at the firewall. (2) A malicious pull request triggering a deploy: deploy runs only on `main` after CI and manual approval. (3) A leaked image: no secrets inside; registry package private. (4) Origin reached directly bypassing Cloudflare: firewall only admits Cloudflare ranges. |
