# SCRUM-202: Staging environment and deploy pipeline

- **Status:** Approved (owner, 2026-10-16)
- **Jira:** SCRUM-202
- **BRD sections:** §9.3, §11.6, §11.7, §8.4
- **Requirement IDs:** NFR-10, SEC-23, SEC-24, SEC-22 (probes used by the deploy), NFR-07 (basic backup only)

## Problem & intent
Nothing has been deployed yet, so nothing about secure operation is proven. Sprint 8 ends with a real staging environment on the internet, reachable over HTTPS behind Cloudflare, updated by a pipeline, with the security gate of [ADR 0011](../../../adr/0011-security-and-coupling-baseline.md) satisfied by the real sign-in. A local docker-compose run is an interim CI proof only, not a substitute (owner, 2026-10-16).

## Requirements (quoted from the BRD)
> **NFR-10:** All traffic shall be served over HTTPS/TLS.

> **SEC-23:** CI/CD pipelines shall include automated dependency vulnerability scanning (e.g. Dependabot/Snyk for NuGet and npm packages) and fail the build on critical findings.

> **SEC-24:** Container images shall be scanned for known vulnerabilities before deployment to production.

> **SEC-22:** Health-check and readiness-probe endpoints shall be implemented for all deployed services to support automated recovery and load-balancer routing decisions.

> **NFR-07:** Payment and inventory data shall be backed up daily; RPO 24 hours, RTO 4 hours.

ADR 0003 (self-hosted VPS behind Cloudflare, not Azure) overrides the BRD's Azure wording.

## User flow
1. **Provision (once, part of this sprint):** the owner chooses a provider and a domain; a VPS, a Cloudflare zone and DNS records are created; the server is prepared from a documented, repeatable procedure kept in the repository.
2. **Deploy:** a change merged to `main` that passes CI is built into images, scanned, pushed to a registry, and (after the owner approves the `staging` environment in GitHub) deployed to the VPS, which migrates the databases and starts the new version.
3. **Check:** the pipeline runs a smoke test against the public staging URL. If it fails, the previous version is restored automatically.
4. **Roll back:** the owner can redeploy any earlier image tag from the pipeline with one action.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| AC-1 | Given the staging VPS (Hetzner; the cheapest region is acceptable for staging), then it runs a supported Linux LTS, accepts SSH by key only through a non-root deploy user (no root login, no passwords), has a firewall allowing only HTTPS from Cloudflare and SSH from a restricted source, applies security updates automatically, and its setup is written down in `deploy/README.md` so it can be rebuilt. All provider, Cloudflare, registry and bucket accounts are held by the owner with MFA; no root credential is ever given to an automation agent. | NFR-10, owner 2026-10-16 |
| AC-2 | Given the domain, then DNS and TLS are served through Cloudflare in Full (strict) mode with an origin certificate on the VPS; HTTP redirects to HTTPS; `Strict-Transport-Security` is sent; no plain-HTTP path reaches the application. | NFR-10 |
| AC-3 | Given the staging stack, then the admin app and the API are reachable on **one origin** (the admin's reverse proxy forwards `/api` to the API); the API, PostgreSQL and Redis ports are not published to the internet. | D-3 |
| AC-4 | Given a push to `main` with green CI, then the pipeline builds the API and admin images tagged with the commit, scans them (critical findings fail the run, SEC-24), pushes them to the registry, waits for approval of the `staging` environment, and deploys. | SEC-24 |
| AC-5 | Given a deploy, then database migrations run before the new API receives traffic; if a migration fails, the old version keeps running and the run fails with the error. | (new) |
| AC-6 | Given the deploy, then the new containers must report ready ([observability](../observability/spec.md) AC-1) within a time limit, or the previous version is restored and the run fails. | SEC-22 |
| AC-7 | Given a deployed commit, then a smoke test over the public URL checks: readiness returns 200, the admin sign-in page loads, an unauthenticated API call returns 401, and the security headers are present; a failure rolls back. | (new) |
| AC-8 | Given the pipeline, then any earlier image tag can be redeployed on demand (rollback) using the same steps as a normal deploy. | (new) |
| AC-9 | Given secrets (database password, MFA encryption key, registry and SSH credentials, MediatR licence key), then they live only in GitHub environment secrets, scoped to the `staging` environment, and a server-side file readable by the deploy user alone; none is in the repository, an image, a build log or a container's published configuration. | ADR 0011 |
| AC-10 | Given staging runs with `ASPNETCORE_ENVIRONMENT=Staging`, then sample-data seeding is off, the development sign-in handler is absent, and the host starts only because the real cookie scheme is registered. | ADR 0011 |
| AC-11 | Given staging, then it is not indexable: it sends `X-Robots-Tag: noindex, nofollow` and `robots.txt` disallows all. | CLAUDE.md (admin noindex) |
| AC-12 | Given the first deploy, then the Owner account is created with the operator step from [staff-accounts](../../identity/staff-accounts/spec.md) following a runbook in `deploy/README.md`, and the Owner enrols MFA and signs in on the public staging URL. | D-1 |
| AC-13 | Given the database, then it is dumped nightly, **encrypted before upload**, to storage at a different provider than the VPS (Cloudflare R2), the last 7 dumps are kept, and a restore of the latest dump into a scratch database has been performed and its steps recorded; that tested restore is an acceptance item of the sprint. A failed or missing backup raises a signal the owner receives within 24 hours (see [observability](../observability/spec.md) AC-12). The full restore drill and HA are S19. | NFR-07, owner 2026-10-16 |
| AC-14 | Given the existing CI, then NuGet vulnerability scanning stays, and npm dependencies are scanned too with critical findings failing the build; a weekly scheduled run re-scans `main`. | SEC-23 |
| AC-15 | Given container logs on the VPS, then rotation limits their size so the disk cannot fill from logs. | (new) |
| AC-16 | **Hostname is configuration.** Given the staging domain is neutral (not the brand name) and a production domain is chosen later, then the host name, public base URL, cookie domain (host-only by default), allowed origins, allowed hosts and the proxy server name all come from configuration (one variable, `ROMP_PUBLIC_HOST`, feeds them in the deploy), never from constants or committed files; the API exposes `Site:PublicBaseUrl` to code that needs it (S9 canonical and sitemap read it); switching domain is a configuration, DNS and redeploy change, proven by deploying staging under a second hostname once. | Owner decision 2026-10-16 |
| AC-17 | Given the production region is still undecided, then the README holds a short procedure and script to measure latency from Pakistan to Hetzner's Singapore and EU locations using short hourly-billed test servers, so the region is chosen on data before S19. | Owner decision 2026-10-16 (new) |

## Non-functional constraints
- A deploy of an unchanged database completes in under 5 minutes; downtime during deploy is acceptable on staging (a short restart), but rollback must restore service within 5 minutes.
- Monthly running cost of staging is stated in the plan and approved by the owner before provisioning.

## Out of scope
- Production environment, database HA and failover, Cloudflare DDoS tier, backup restore drill, incident runbook (all S19; SEC-19, SEC-20, SEC-27).
- Storefront deployment (S9 adds its container to the same pipeline).
- Kubernetes or Swarm; Terraform for the whole estate (the plan decides how much automation is justified).

## Open questions
- [ ] **What is already provisioned?** Osama confirms which of the VPS, Cloudflare account, GitHub environment and bucket already exist. Anything missing is provisioned in this sprint (owner decision, 2026-10-16).
- [ ] **Neutral staging domain.** Not decided; Osama registers it before Tuesday and puts it on Cloudflare. Blocks only the Friday deploy (AC-2, AC-16).
- [x] ~~Provider~~ Hetzner. Production region chosen after the latency measurement (AC-17).
- [x] ~~Backup target~~ Cloudflare R2 (different provider than the VPS), encrypted before upload, tested restore in the sprint (AC-13).
- [x] ~~Registry~~ GitHub Container Registry. ~~Hostname layout~~ `admin.<staging-domain>` on one origin, storefront takes the bare domain in S9.
- [x] ~~Database~~ A container on the VPS is accepted for staging; managed HA at launch (S19).
