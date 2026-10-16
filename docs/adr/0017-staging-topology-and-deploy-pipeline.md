# 0017 — Staging topology and the deploy pipeline

- Status: Accepted (approved by the owner 2026-10-16)
- Date: 2026-10-16

## Context

ADR 0003 chose self-hosted VPS nodes behind Cloudflare with Docker, and ADR 0005 chose GitHub Actions. Sprint 8 needs a real staging environment to prove secure operation, and cookie authentication needs the admin app and API on one origin (ADR 0015). Provisioning status is confirmed by the owner; anything missing is provisioned in Sprint 8.

## Decision

1. **One Hetzner VPS for staging** (cheapest region; production region decided after a latency measurement) running Docker Compose: a reverse proxy container (terminates TLS with a Cloudflare origin certificate, serves the admin app's static files, forwards `/api` and the health paths to the API), the API, PostgreSQL 17, Redis. Only the proxy publishes ports (443, and 80 for redirect), and only to Cloudflare ranges through the firewall.
2. **One origin:** `https://admin.<domain>` serves the app and `/api`. The admin app is built with an empty `apiBaseUrl`; local development uses an Angular dev-server proxy to the API, so CORS stays a development-only setting.
3. **Images:** built in GitHub Actions, tagged with the commit SHA, scanned (Trivy, critical fails), pushed to GitHub Container Registry.
4. **Deploy:** a GitHub Actions workflow bound to the `staging` environment (required reviewer: the owner) connects over SSH as a restricted deploy user, uploads the compose file and env references, pulls the tagged images, runs the migration step as a one-off container, then starts the new containers and waits for readiness. A failing migration or readiness check restores the previous tag. Rollback is the same workflow with an older tag.
5. **Migrations** run from the API image as an explicit `migrate` command (a mode of the host executable), not at start-up, so a deploy can run them first and abort cleanly. The Development-only start-up migrator stays.
6. **Secrets:** GitHub environment secrets for pipeline credentials; a `0600` env file on the server for runtime secrets, written by the deploy from environment secrets. Nothing in images or the repository.
7. **Backups:** a nightly `pg_dump` per schema group, encrypted with `age` before it leaves the server, uploaded to Cloudflare R2 (a different provider than the VPS), seven kept, a dead-man ping on success, plus one recorded restore test as a sprint acceptance item.
8. **Automation level:** a documented, scripted server bootstrap (`deploy/bootstrap.sh`, run once) and the compose files in `deploy/`. Terraform or Ansible is deferred until a second environment (production, S19) makes it worth the cost; the script is written so that it is the first step of that automation.
9. **Platform spec area:** `docs/specs/platform/` has no code module; `StructureSyncTests` treats it as a spec-only area.

## Alternatives considered

- **Docker Swarm or Kubernetes:** unjustified for one node; ADR 0003 says only when traffic requires it.
- **Deploying by pulling the repository on the server and building there:** no scanned artefact, slow, and needs the toolchain on the VPS.
- **A PaaS (Fly.io, Render):** contradicts ADR 0003 and the self-hosting cost model.
- **Running migrations at API start-up in staging:** hides failures inside a restarting container and races when more than one API node exists.

## Consequences

- The owner must supply a provider account, a domain and a Cloudflare account before the deploy work can finish.
- The staging data is disposable except for the nightly dump; it is not a production environment and makes no availability promise.
- S9 adds the storefront container to the same compose file and pipeline; S19 adds a second node, HA database and DDoS tier.

## Addenda (owner, 2026-10-16)

- **Hostname is configuration, never a constant.** One deploy variable (`ROMP_PUBLIC_HOST`) feeds the proxy server name, allowed hosts, allowed origins (development only), cookie domain (host-only by default) and `Site:PublicBaseUrl`, which S9 reads for canonical URLs and the sitemap. The staging domain is neutral (not the brand name); the production domain is chosen before launch and indexing, and switching is a configuration, DNS and redeploy change. Staging stays `noindex, nofollow`.
- **Accounts and access:** all accounts are held by the owner with MFA; a non-root deploy user with key-only SSH; secrets scoped to the `staging` GitHub environment; no root credential is given to any automation agent.
- **Production region** is chosen from measured latency (Pakistan to Singapore and EU) using short hourly-billed test servers; staging uses the cheapest region.
