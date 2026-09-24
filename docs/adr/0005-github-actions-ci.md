# 0005 — GitHub Actions for CI/CD

- Status: Accepted
- Date: 2026-09-25
- Supersedes: BRD §9.4 (Azure DevOps)

## Context
The code is hosted on GitHub (`osamabuilds/JuniorHouse`), and the Claude GitHub App is used for PR work. Running Azure DevOps as well would mean a second system to connect and keep up.

## Decision
- GitHub Actions, with separate workflows for the API and the web apps, so each deploys independently (NFR-24).
- Every PR runs build, tests, a vulnerable-dependency check (SEC-23) and a Trivy container-image scan (SEC-24).
- Dependabot keeps NuGet, npm, Docker and Actions dependencies up to date.
- Deployment to the VPS (a blue-green container swap behind Traefik/Nginx) will be added as a separate workflow once staging exists.
