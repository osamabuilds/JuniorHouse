# 0020: Storefront settings, draft/publish and the published snapshot

- **Status:** Proposed (owner decisions S-1..S-5 and Q-ST-1..8 approved 2026-10-24; this ADR awaits approval with the plan)
- **Date:** 2026-10-24

## Context
The owner wants the storefront to be configured from the admin app, so that navigation, age steps, footer links, contact, delivery and exchange facts and content pages change without a code change or a deployment (`docs/specs/storefront/storefront-settings/spec.md`). ADR 0018 created the `Storefront` module as a rebuildable read model with no writes of its own. Settings are different: they are the source of truth, they are authored by staff, they must go live all at once, and they must keep serving if their source is unavailable.

## Decision
1. **Home.** The `Storefront` module (schema `STFR`) owns settings as a second, separate feature area (`Settings`, with sub-features `Navigation`, `Ages`, `Footer`, `Contact`, `Delivery`, `Pages`, `Publishing`). It stays free of other modules' tables; collections and product counts reach it through `Catalog.Contracts` or its own read model, as ADR 0018 already says. The read-model part and the settings part share no tables.
2. **Draft tables are normalised; the published set is a snapshot.** Staff edit normalised draft tables (3NF, lookups, standard audit columns, concurrency tokens). **Publish** serialises the whole draft into one immutable JSON document (`jsonb`) in `PUBL_SNAP` with a version number and a content hash, inside one transaction, and marks that version current. This is a read model and therefore the allowed denormalisation (CLAUDE.md database rules); the reason is atomic go-live, cheap rollback (re-point to an earlier snapshot) and one cacheable read for the storefront.
3. **The storefront only ever reads the current snapshot.** `GET /api/storefront/settings` returns the document (public, no personal data, `ETag` = content hash, cached 60 s in memory in the API and in the SSR server). A publish raises `StorefrontSettingsPublished` through the outbox (ADR 0004); the host invalidates its caches on it; the 60 s expiry is the backstop.
4. **Last good copy.** The SSR server keeps the last good snapshot in memory and on disk; if the API is unreachable it serves that, and on first start with none it serves a **built-in launch set** compiled into the storefront (the same set that seeds the database), so the header is never empty.
5. **Page content is structured blocks, never HTML.** A page is an ordered list of typed blocks (heading, paragraph, list, table, call-out, fact). Text is plain text with no markup; the storefront renders blocks with Angular bindings. A `fact` block inserts a typed settings value (a delivery rule, the exchange window, a contact detail) by key, so the same fact is stored once. A page that references an empty fact cannot be published.
6. **Authorisation.** Every admin endpoint requires the policy `StorefrontSettings.Admin` (role Admin), checked server-side per request (SEC-08). Read endpoints for the public snapshot are anonymous. Until the Identity work in S8, the settings endpoints are enabled only in non-public environments (a configuration switch that the production host refuses to turn on without real authentication).
7. **Audit.** The standard ADR 0016 interceptor audits every draft change. Publish and restore are additionally recorded as `PUBL_SNAP` rows (who, when, note, version).
8. **Redirects and removal.** A page slug change inserts a row in a redirect table (old slug to page); an archived page returns 410. The sitemap is produced from the snapshot.
9. **Admin UI** lives in the admin project as `features/storefront/settings` using the admin conventions (NgRx, containers/components, paging for lists).

## Alternatives considered
- **A separate `Content` module.** Cleaner name, but it would immediately need a contract to the Storefront for every read and a second schema for a small amount of data; revisit if settings grow beyond the storefront (for example emails).
- **Edit the live rows directly (no draft).** A half-edited menu would be public and rollback would need history tables. Rejected.
- **Store everything as one JSON document in the draft too.** Fast to build, but no validation by constraint, no per-row audit and no foreign keys. Rejected.
- **Free-form rich text / HTML pages.** Needs a sanitiser and invites XSS and inconsistent typography. Rejected for launch (Q-ST-2).
- **A third-party headless CMS.** A new service to host, secure and learn for six pages and a menu. Rejected.

## Consequences
- The Storefront module is created in SCRUM-206 (its host registration, schema, architecture-test entry, migrations), earlier than the read-model work of ADR 0018; the read model joins it later.
- A new table family and a new admin section; a published-snapshot size grows with page text (small, kept under a size limit in validation).
- Facts used by several pages are single records, which later checkout and emails can read from the same snapshot.
- The deployment must keep the settings feature switched off on public hosts until S8 lands.
