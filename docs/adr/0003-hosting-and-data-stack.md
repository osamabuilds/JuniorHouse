# 0003 — Self-hosted VPS, PostgreSQL, Redis, Cloudflare

- Status: Accepted
- Date: 2026-09-25

## Context
BRD v9.0 confirms a self-hosted VPS + PostgreSQL stack, replacing the earlier Azure-oriented draft. Some Azure wording is still left in the BRD (NFR-FT-02/04, §9.3, §9.4, §11.1). That wording is superseded by this ADR.

## Decision
- **Compute:** Docker containers on 2 VPS nodes behind Cloudflare load balancing, WAF and CDN. Docker Swarm or Kubernetes only once traffic justifies it.
- **Database:** Managed PostgreSQL with built-in HA at launch (SEC-19).
- **Cache:** Redis, single instance at MVP. The app falls back to reading from the database if Redis is unavailable.
- **Object storage:** S3-compatible storage (Backblaze B2 / DigitalOcean Spaces) for product images.
- **Monitoring:** OpenTelemetry with Prometheus and Grafana, not Application Insights.

## Consequences
- Lower running costs and no lock-in to one cloud provider.
- We handle patching, backups and monitoring ourselves, so infrastructure as code (Terraform + Ansible) is needed early.
