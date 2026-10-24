# SCRUM-206: Storefront settings: technical plan

- **Spec:** [spec.md](spec.md) · **ADR:** [0020](../../../adr/0020-storefront-settings-and-published-snapshot.md) · **Module plan:** [../plan.md](../plan.md)
- **Module(s):** Storefront (new, schema `STFR`), Admin web app, storefront web (consumer only, via SCRUM-205)

## Domain model
`Romp.Modules.Storefront.Domain/Settings/…`, no EF or ASP.NET references.
- **`NavItem`** (one self-referencing tree, aggregate root = the draft navigation): key (unique, `^[a-z0-9-]{2,40}$`), label (1..40), parent (nullable), level (1..3, derived and checked), kind (`Link` | `Group` | `CollectionList`), target (kind + reference, validated; none for a `Group`), sort (unique among siblings), enabled. Invariants: maximum depth 3 (one constant, Q-ST-9); a `Group` has at least one enabled child; a `Link` has a target and may also have children (it then opens them and is itself reachable from the panel); any entry may be a parent; a `CollectionList` adds the enabled collections as its children and may also have manual ones; disabling an item hides its subtree; an item cannot be its own ancestor.
- **`AgeStep`**: stable code (`^[a-z0-9-]{1,20}$`, immutable after first publish), label, sort, enabled. Launch seed `1y`..`8y`.
- **`FooterColumn` → `FooterLink`**: title, sort; link label, target, sort, enabled.
- **`SettingItem`** (brand and contact): key from a lookup (`BRAND_NAME`, `TAGLINE`, `WHATSAPP`, `EMAIL`, `HOURS`, `ANNOUNCEMENT`, `ANNOUNCEMENT_ON`), a typed value with a per-key rule (phone E.164, email, length).
- **`DeliveryRule`**: city (lookup-style row), min/max days (smallint, min ≤ max), charge `numeric(12,2)` PKR ≥ 0, free-delivery threshold `numeric(12,2)` nullable, courier name, enabled. **`ExchangePolicy`** (single row): window days, conditions (ordered plain-text lines).
- **`Page`** (aggregate with ordered `PageBlock`s): slug (`^[a-z0-9]+(-[a-z0-9]+)*$`, 2..60, not in the reserved list: `girls`, `boys`, `new-in`, `collections`, `sale`, `search`, `p`, `api`, `_foundations`, …), title, meta description (≤ 160), status (`Draft` | `Published` | `Archived`), blocks. Block types: `Heading` (level 2 or 3), `Paragraph`, `List`, `Table`, `Callout`, `Fact`. Invariants: first heading level 2; a `Fact` block names an existing fact key; a `Published` page has a non-empty title and description and no empty fact (ST-17).
- **`SettingsDraft`** (the working copy, version counter) and **`PublishedSnapshot`** (immutable: version, JSON document, SHA-256 hash, published by/at, note, `IsCurrent`). Publishing validates the whole draft (`SettingsValidator`: no dead links, no duplicate keys, no empty menu, no empty facts used by published pages, size limit) and produces the document.
- **Redirect**: old slug → page, written when a published page's slug changes.

## Application layer (`Romp.Modules.Storefront.Application/Settings/<Feature>/`)
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `GetDraftSettingsQuery` | none | whole draft (admin) | ST-1 |
| `SaveNavigationCommand` | the whole nested tree (items with children), row versions | saved tree | ST-1, 1a, 4, 14 |
| `SaveAgeStepsCommand` | ordered steps | saved steps | ST-2 |
| `SaveFooterCommand` | columns and links | saved footer | ST-1 |
| `SaveContactSettingsCommand` | key/value items | saved items | ST-4 |
| `SaveDeliveryAndExchangeCommand` | rules, policy | saved | ST-7 |
| `CreatePageCommand` / `UpdatePageCommand` / `ChangePageStatusCommand` | slug, title, description, blocks, row version | page | ST-8, 9, 10, 17 |
| `PreviewSettingsQuery` | none | draft rendered as the public document (admin) | spec scope 2 |
| `PublishSettingsCommand` | note | new version | ST-3, 12 |
| `RestoreSnapshotCommand` | version, note | new current version (a copy of the old snapshot, never an edit) | ST-12 |
| `ListSnapshotsQuery` | `page`, `pageSize` | `PagedResult<SnapshotRow>` | ST-12 |
| `GetPublishedSettingsQuery` | none | current snapshot document (cached 60 s) | ST-1, 13 |
| `GetPublishedPageQuery` | slug | page, or `Gone`/`NotFound`/`Redirect` | ST-8, 9 |
| `ListPagesQuery` | `page`, `pageSize`, status | `PagedResult<PageRow>` | ST-8 |

Every command validates with FluentValidation (messages name the field and the fix); handlers use the MediatR dispatch already in the repo.

## API endpoints
| Method | Route | Auth / role | Ownership check (SEC-05) |
|---|---|---|---|
| GET | `/api/storefront/settings` | anonymous (`Storefront.Public`) | n/a, no user data |
| GET | `/api/storefront/pages/{slug}` | anonymous | n/a |
| GET | `/api/admin/storefront-settings/draft` | `StorefrontSettings.Admin` | n/a, no per-user data |
| PUT | `/api/admin/storefront-settings/navigation` · `/ages` · `/footer` · `/contact` · `/delivery` | `StorefrontSettings.Admin` | n/a |
| GET/POST/PUT | `/api/admin/storefront-settings/pages` · `/pages/{id}` · `/pages/{id}/status` | `StorefrontSettings.Admin` | n/a |
| GET | `/api/admin/storefront-settings/preview` | `StorefrontSettings.Admin` | n/a |
| POST | `/api/admin/storefront-settings/publish` · `/restore` | `StorefrontSettings.Admin` | n/a |
| GET | `/api/admin/storefront-settings/snapshots?page=&pageSize=` | `StorefrontSettings.Admin` | n/a |

Responses are DTOs with problem-details errors; a stale `rowVersion` returns 409.

## Events
| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|
| `StorefrontSettingsPublished` (version, hash; ids only, ADR 0011) | `PublishSettingsCommand`, `RestoreSnapshotCommand` | Storefront cache invalidation (in-process), later the sitemap job | yes |
| `AuditRecorded` | audit interceptor (ADR 0016) | Identity | yes |

## Data (`STFR`, names follow `docs/db/naming.md`; any new abbreviation is added to the glossary in the same change)
Draft: `NAV_ITEM` (self-referencing, `PRNT_ID` indexed, check on level), `AGE_STEP`, `FOOT_COLM`, `FOOT_LINK`, `STNG_ITEM`, `DLVR_RULE`, `XCHG_POLY`, `XCHG_COND`, `PAGE_MAIN`, `PAGE_BLCK`, `PAGE_BLCK_ITM` (list items and table cells, ordered), `PAGE_RDRT`.
Lookups (`ID` smallint, `CODE`, `NAME`, `DSCR`, `SORT_SEQ`, `ACT_IND`; seeded by migration): `NAV_KIND_LKP`, `LINK_TRGT_LKP`, `STNG_KEY_LKP`, `PAGE_STS_LKP`, `BLCK_TYP_LKP`.
Published: `PUBL_SNAP` (`ID`, `VER_NO` unique, `DOC` jsonb, `DOC_HASH` char(64), `CURR_IND`, `NOTE`, publish audit columns); a partial unique index allows one `CURR_IND` row.
All transactional tables carry `INSR_DTE/INSR_BY/UPDT_DTE/UPDT_BY` and a `ROW_VER` concurrency token; every FK is indexed; unique constraints on `NAV_ITEM.ITEM_KEY`, `(NAV_ITEM.PRNT_ID, SORT_SEQ)`, `AGE_STEP.AGE_CODE`, `PAGE_MAIN.SLUG`. Migration seeds the launch set (S-1, S-2), the contact and brand items (WhatsApp `+923315613217`), and the eight page drafts (delivery, exchanges, size-guide, our-story, made-in-pakistan, contact, privacy, terms; Draft status; wording only, no facts). The snapshot is the only denormalised structure, justified in ADR 0020. Growth: tables are small (tens of rows); no partitioning. Add the module to `ModuleNames` in `Romp.ArchitectureTests`.

## NFR / security design
- **SEC-08:** policy on the route group, tested for 401 and 403 on every write route (ST-11). A host start-up check refuses to enable the admin settings routes when authentication is the development handler and the environment is not Development/Test.
- **Input handling:** DTOs are not entities; strings are trimmed, length-capped and rejected on control characters; block text is plain text and rendered by bindings only (ST-10); slugs and keys are pattern-checked; link targets are a typed reference (internal page, listing path, collection, external https URL on an allow-list), never a free `javascript:` URL.
- **Freshness and resilience (ST-13):** API cache 60 s, SSR last-good copy in memory and on disk, built-in launch set.
- **Concurrency (ST-14):** `ROW_VER` per row and a draft version on the aggregate; save with a stale version returns 409.
- **Size limits:** snapshot ≤ 512 KB, ≤ 100 navigation items in all, depth ≤ 3, ≤ 40 pages, ≤ 60 blocks per page, ≤ 5,000 characters per paragraph.

## Frontend (admin: `src/web/projects/admin/src/app/features/storefront/settings/`)
Admin conventions (CONVENTIONS §1): `containers/` (navigation-page, ages-page, footer-page, contact-page, delivery-page, pages-list-page, page-editor-page, publish-page), `components/` (presentational; nested tree editor (add child, move up/down, indent/outdent buttons; no drag-only action), link-target picker, block editor with one component per block type), `forms/`, `models/`, `services/settings-api.service.ts`, `store/` (NgRx), `utils/`, `settings.routes.ts`, `index.ts`, one lazy route and one nav group in the admin shell. Lists use `<app-pagination>` and the paged endpoints. Inline, specific validation messages; reorder by buttons (WCAG 2.5.7); an unsaved-changes guard; a Preview button that opens the storefront with the draft (admin-only, `noindex`). Admin E2E in `src/web/e2e/storefront/settings.e2e.spec.ts`.

## Dependency: Catalog category hierarchy (SCRUM-207)
Navigation items of kind `Category` need Catalog categories as a tree. SCRUM-207 is specified before it is built (it changes the Catalog category, URL (ADR 0013) and listing specs). Sequence: this feature ships with targets of kind `Path`, `Page`, `Collection` and `External`, and a `Category` kind that the validator accepts only once the Catalog contract is present; the seeded tree uses `Path` targets until then. No settings table changes when SCRUM-207 lands (a target is a kind plus a code).

## Risks & alternatives considered
Alternatives in ADR 0020. Risks: the settings routes before S8 (mitigated by the start-up check and the deployment note); a wrong publish (mitigated by validate-on-publish, preview and one-click restore); page abuse by a compromised admin account (plain-text blocks only, external links allow-listed, audit trail).

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization (policy per endpoint, ownership SEC-05, roles SEC-08) | Admin policy on every write and draft/preview route, server-side; public routes explicitly anonymous; no per-user resources (SEC-05 n/a) |
| Input validation (type, range, length, charset; DTO ≠ entity) | FluentValidation per command; caps and patterns listed above; typed link targets; DTOs only |
| Output encoding (ZPL, PDF, SQL, HTML, file paths) | Plain-text blocks rendered by Angular bindings; no `innerHTML`; JSON-LD serialised by the framework; EF parameters for SQL |
| Outbound calls (config-only, allow-listed, timeouts) | None from the API; external link targets allow-listed by host list in configuration |
| Resource limits (sizes, counts, page size, queue length, rate limits) | Snapshot, link, page and block caps; paged lists clamped 1..100; public read cached |
| Concurrency (constraints, conditional updates, concurrency tokens) | `ROW_VER` on all rows; one-current-snapshot partial unique index; publish in one transaction |
| Idempotency (inbox, retried commands) | Publish with the same draft version and hash returns the existing snapshot instead of a duplicate; event consumers are idempotent (cache invalidation) |
| Audit (who, when, why; append-only; log-injection safe) | ADR 0016 interceptor for draft changes; `PUBL_SNAP` rows with user, time and note; values logged by key only |
| Data protection (money as decimal, no personal data in events/logs/errors, child data) | Charges `numeric(12,2)`; the settings contain no personal or child data; the event carries version and hash only |
| Secrets and dangerous configuration | No secrets; the "enable admin settings routes" switch refuses production without real authentication |
| Error responses (problem details only) | ProblemDetails; 409 on conflict; no stack traces |
| New dependencies (name, version, licence) | None planned (FluentValidation and MediatR already in use) |
| Coupling (contracts used, mapper for consumed events, ports and fakes, no cross-module internals) | Collections and categories come through `Catalog.Contracts` (a new `ICollectionQueries` and a category-tree query from the Catalog hierarchy task SCRUM-207); navigation targets of kind Category store the category code and are validated through that contract at publish; Storefront never reads Catalog tables; the admin app talks only to the API |
| Abuse cases considered (at least three: what would a hostile or careless user do?) | (1) Script or HTML typed into a block or title: stored and rendered as text. (2) A `javascript:` or phishing link in the footer: rejected by the typed target and host allow-list. (3) Two admins overwrite each other: 409. (4) Publishing a menu with dead links or a page with an empty fact: blocked by publish validation. (5) Slug set to a reserved route (`api`, `girls`): rejected. (6) Huge payload: size caps |

## Contract additions from SCRUM-205 (site chrome), 2026-10-24
The storefront built against the `SiteSettings` contract and added to it; SCRUM-206 must store and publish these: `NavItem.showAges` (boolean per navigation item); `popularSearches` (ordered list of terms, a setting); `PageBlock` type `fact` with an optional `label`, and the fact keys `contact.whatsapp`, `contact.email`, `contact.hours`, `exchange.window`, `exchange.conditions`, `delivery.rules` (a page that uses a fact whose value is empty cannot be published, ST-17); privacy and terms are seeded as Draft with no text. The seeded wording lives in the storefront's `launch-pages.ts` and must equal the migration seed (contract fixture test, task 5 and 21).
