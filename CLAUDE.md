# Romp — working notes for Claude

A direct-to-consumer kidswear platform for Pakistan: the storefront (with Fit Finder and Child Profiles) plus the supply-chain back office (vendors → QC → barcodes → warehouse → packing → couriers → returns).

## Sources of truth (read before building anything)

| What | Where |
|---|---|
| Business requirements (BRD v9.0) | `Romp_Master_v9_Merged.docx`, which is being moved into `docs/specs/` one feature at a time |
| Feature specs, plans, tasks | `docs/specs/<JIRA-KEY>-<slug>/` |
| Architecture decisions | `docs/adr/` (these override the BRD where they conflict) |
| Backlog | Jira project `SCRUM`, osamabuss048.atlassian.net |

Requirement IDs come from the BRD and must be used unchanged everywhere: `FR-xx`, `FR-SC-xx`, `NFR-xx`, `NFR-FT-xx`, `NFR-SC-xx`, `SEC-xx`, `SEO-xx`, test cases `TC-<AREA>-xx`, features `FEAT-<AREA>-xx`. Each Jira task carries its ID as a label.

## Spec-driven workflow (required for every feature)

Do not write production code until the spec is approved. Work through the steps in order and stop for the user's approval at each ⏸:

1. **Spec**: copy `docs/specs/_template/spec.md` into `docs/specs/<JIRA-KEY>-<slug>/spec.md`.
   - Say *what* is needed and *why*, not *how*.
   - Quote the BRD requirement text exactly, with its IDs.
   - Take acceptance criteria from the BRD's `TC-*` cases. Add edge cases the BRD misses, marked `(new)`.
   - Record anything ambiguous under **Open questions**. Never resolve it silently. ⏸
2. **Plan**: write `plan.md` from the template: domain model, commands/queries, endpoints, events, schema changes, and which NFR/SEC items apply and how they're met. Any architectural choice not already covered by an ADR gets a new ADR. ⏸
3. **Tasks**: write `tasks.md` as small, ordered, checkable steps. Each step names the test that proves it.
4. **Implement test-first**: write the failing test for an acceptance criterion, then make it pass. Tag each test with the IDs it covers (see Testing).
5. **Close the loop**: if the implementation had to differ from the spec, update the spec in the same PR. The spec must describe what was actually built. The PR description links the spec folder and lists the requirement IDs covered.

Small changes (typo fixes, dependency bumps, CI tweaks, refactors that change no behaviour) skip steps 1–3.

**Rules**
- Never invent requirements. If the BRD doesn't say, ask.
- Where the BRD contradicts itself or an ADR, raise it; don't pick a side. Known conflicts are listed below.
- Phase 2/3 features (marked `P2`/`P3` in the BRD) are out of scope unless the user asks for them.

## Architecture (see `docs/adr/`)

- **Modular monolith** (ADR 0002). One host, `src/api/src/Romp.Api`. Each module lives in `src/api/src/Modules/<Name>/` with `Domain`, `Application` and `Infrastructure` projects and implements `IModule`.
  - Modules never reference each other's internals. They communicate through integration events sent via the outbox, or synchronously through another module's `*.Contracts` project (public DTOs and query interfaces only).
  - Each module owns its own PostgreSQL schema, and no module queries another module's tables.
  - `tests/Romp.ArchitectureTests` enforces this. Add every new module to `ModuleNames` there.
  - The Domain layer has no dependency on EF Core, ASP.NET Core, or any other layer.
- **Outbox** (ADR 0004). Domain events and notifications are written in the same transaction as the business change. A notification failure must never roll back the business transaction (NFR-FT-08).
- **Stack** (ADR 0003): .NET 10, Angular 22 (storefront is server-side rendered), PostgreSQL 17, Redis, Docker, self-hosted VPS behind Cloudflare. The BRD's Azure wording (Service Bus, App Insights, AKS, Azure DevOps) is stale; ignore it.

**Rules that apply everywhere (the BRD marks these launch-blocking)**
- Recalculate every price and total on the server. Never trust amounts sent by the client (SEC-09).
- Every endpoint that takes a resource ID checks that the resource belongs to the caller (SEC-05). This matters most for Child Profile data.
- Admin endpoints check the caller's role server-side on every request (SEC-08).
- Verify payment webhook signatures before changing any order state (SEC-10).
- Order placement is idempotent and stock decrement is atomic (FR-28, FR-29, NFR-08).
- Money is `decimal` / `numeric(12,2)` in PKR. Never `double`.
- Never log passwords, card data, or full Child Profile records (SEC-25).
- Child data is personal data about minors: owner-only access, and exportable or deletable on request (NFR-27..30).

## Database design (mandatory)

Full rules and the abbreviation glossary are in [`docs/db/naming.md`](docs/db/naming.md). Read it before adding or changing any table.

**Naming**
- Every table, column, schema, index and constraint name is **UPPERCASE snake_case built from abbreviated words of 2–4 characters**: `CUSTOMER_ORDER_MAIN` → `CUST_ORDR_MAIN`, `CREATED_AT` → `INSR_DTE`.
  - Every word must match `[A-Z0-9]{2,4}`.
  - Use the abbreviation already in the glossary for a word. Only invent one if the word isn't there, and add it to the glossary in the same PR. The same word must never get two abbreviations.
  - Avoid SQL reserved words (`DESC`, `USER`, `ORDER`); the glossary gives alternatives (`DSCR`, `USR`, `ORDR`).
- Names are **quoted** identifiers in PostgreSQL (case-sensitive). EF Core quotes them automatically. **Any raw SQL must quote every identifier**: `SELECT "ORDR_NO" FROM "VNDR"."PO_MAIN"`.
- Every EF entity configuration maps its table and every column explicitly (`ToTable`, `HasColumnName`). Never rely on the default naming.
- An automated test checks every table and column name in the EF model against the rule. A name that fails it fails the build.

**Structure**
- Normalise to at least **3NF**. No repeating groups, no comma-separated lists in a column, and no duplicated attributes. Denormalise only in read models (Redis or read tables), never in the transactional tables, and record the reason in the plan.
- **Lookup tables** for every enumerated value (statuses, types, sizes, colours, cities, payment terms, reason codes, …). Never use free-text strings or C# enum ordinals stored as ints without a lookup row. Standard lookup shape: `ID` (smallint PK), `CODE` (unique, stable, used in code), `NAME`, `DSCR`, `SORT_SEQ`, `ACT_IND`. Lookups are seeded through migrations.
- Keys: surrogate `ID` primary keys (`bigint` identity for transactional tables, `smallint` for lookups). Real-world identifiers (PO number, SKU, GTIN) get their own columns with unique constraints. All foreign keys are declared, and every FK column is indexed.
- Every transactional table has the audit columns `INSR_DTE`, `INSR_BY`, `UPDT_DTE`, `UPDT_BY` and a concurrency token.
- Types: `timestamptz` for timestamps, `numeric(12,2)` for money (PKR), `numeric` for quantities that may be fractional, `varchar(n)` with a sensible `n`, never unbounded text for coded values.
- Each module owns one schema, named by the same rule (`VNDR`, `CTLG`, `WHSE`, …). Nothing joins across schemas; data from another module comes through that module's contract.
- Design for growth: no schema change should be needed for new categories, sizes, colours, couriers or statuses. Add a lookup row instead. Tables expected to grow large (orders, stock ledger, notifications, audit) note their partitioning key in the plan (BRD §12.6).

## Frontend: semantic HTML & SEO (mandatory, no compromise)

The goal is for Romp to rank as high as possible. Google decides the actual position, so no code can guarantee #1. What the code **must** guarantee is that nothing technical ever limits ranking. Every item below is required on every public page, and a PR that misses one is not done.

**Semantic HTML (storefront and admin)**
- Use landmark elements: `<header>`, `<nav>`, `<main>` (exactly one), `<aside>`, `<footer>`. Content blocks are `<article>`/`<section>` with a heading. No `div` soup: use a `div` only when no semantic element fits.
- Exactly one `<h1>` per page, and headings never skip a level.
- Use `<a href>` for navigation and `<button>` for actions, never a clickable `div`/`span`. Lists are `<ul>`/`<ol>`, tables are `<table>` with `<th scope>`, and images are `<img>` (inside `<figure>`/`<figcaption>` where there's a caption).
- Every form control has a `<label>`. Use `aria-*` only when native semantics can't express the meaning.
- Meet WCAG 2.1 AA (NFR-19): contrast, visible focus, full keyboard operation, meaningful alt text (empty `alt=""` only for decorative images).

**SEO (storefront)**
- **Every public route is server-rendered** (SEO-01). The full content, links, meta tags and structured data must be in the HTML the server returns, never added only after hydration.
- Clean, keyword-descriptive, lowercase, hyphenated URLs (`/girls/dresses/floral-summer-dress`), with no IDs or query strings on indexable pages (SEO-02). If a slug changes, the old URL gets a **301** redirect to the new one.
- Every page has a unique `<title>` and meta description from the BRD §14.3 templates (SEO-04), a self-referencing `<link rel="canonical">`, and Open Graph + Twitter Card tags (SEO-08). Filter, sort and pagination variants carry a canonical tag (SEO-06).
- JSON-LD structured data: `Organization` + `WebSite` (with `SearchAction`) site-wide, `Product` + `Offer` on product pages, and `BreadcrumbList` on every page below the homepage (SEO-03). It must pass Google's Rich Results Test.
- Breadcrumbs are visible and use semantic markup. Categories, products and guides link to each other (§14.2).
- Real HTTP status codes from SSR: 404 for unknown pages, 410 for permanently removed products, never a soft 404 (a "not found" page returned with 200).
- `sitemap.xml` is generated automatically and updated on publish/unpublish, and `robots.txt` blocks cart, checkout and account pages (SEO-05). The admin app sends `noindex, nofollow`.
- Images: `NgOptimizedImage`, WebP/AVIF with `srcset`, explicit `width`/`height` (no layout shift), the product's main image loaded eagerly with `fetchpriority="high"`, everything else lazy-loaded. Alt text is required (SEO-07).
- Performance budgets enforced in CI (Lighthouse CI) on the home, category and product pages: **SEO score 100, Accessibility ≥ 95, Performance ≥ 90 on mobile, LCP ≤ 2.5s, CLS ≤ 0.1, INP ≤ 200ms** (NFR-01, SEO-10). A drop below any budget fails the build.
- `<html lang="en">`, a correct `<meta name="viewport">`, and a mobile-first layout (NFR-17).

## Commands

```bash
# Backend (from src/api)
dotnet build Romp.slnx
dotnet test Romp.slnx

# Frontend (from src/web)
npx ng build storefront | npx ng build admin
npx ng test storefront --watch=false
npx ng serve storefront            # http://localhost:4200

# Local infrastructure (from repo root; copy .env.example to .env first)
docker compose up -d postgres redis mailpit
```

The API runs on http://localhost:5051 (`/health/live`, `/health/ready`).

## Testing

- Backend: xUnit v2. Use `Method_Scenario_Expectation` names (underscores are allowed in `tests/`).
- Tag tests with the requirement or test-case IDs they verify:
  `[Trait("Spec", "FR-SC-02")] [Trait("Spec", "TC-CAT-03")]`
- Every acceptance criterion in a spec has at least one test.
- Integration tests that touch PostgreSQL use Testcontainers, not an in-memory database.

## Conventions

- **Code structure (mandatory):** every feature follows [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md): API feature folders with namespaces matching folders; web features with containers/components/store (NgRx)/models/services; **paged list endpoints, no N+1 queries**; verification steps. Read it before writing code in any sprint, and put its folder/paging tasks in each feature's plan and tasks.
- Branches: `<JIRA-KEY>-<short-slug>`, e.g. `SCRUM-162-ef-core-wiring`. One Jira issue per branch.
- Commits: start with `SCRUM-xxx: `. Author is Osama Shafique <osama.shafique11@gmail.com> (set per repo).
- Jira workflow: Idea → To Do → In Progress → Testing → Done. Move an issue to In Progress when work starts, to Testing when its PR is open, and to Done when the PR is merged.
- New NuGet packages: add the version in `src/api/Directory.Packages.props` only. Warnings are treated as errors.

## Environment notes (developer machine)

- Windows 11. The shell is PowerShell 5.1, so `&&` doesn't work; use `;`.
- **Windows Application Control blocks locally built DLLs** from loading in `Romp.Api.IntegrationTests`. This is a machine policy, so don't try to bypass it. CI (Linux) is the verification for those tests; say so when reporting results.
- The `gh` CLI is not installed. Open PRs through a compare link: `https://github.com/osamabuilds/JuniorHouse/compare/main...<branch>?expand=1`.
- Docker Desktop may not be running. Check before relying on it.

## Scope decisions (override the BRD)

- **Descoped:** the Fit Finder (FR-12..16, FR-19, FR-20, FR-56, NFR-03/21/25, TC-FIT-*) and Virtual Try-On / AR (FEAT-PDP-10/12, NFR-28/30, SEC-15..18). Don't build them. The size chart (FR-08) stays.
- **Child Profiles:** basic profiles only (FR-18 create/edit/delete, FR-21 delete keeps past orders). The child-data rules NFR-27 and NFR-29 still apply.
- **Build order:** follow the BRD business flow from §5.9 onwards (plan → vendor PO → production → QC → barcodes → warehouse → publish → storefront → fulfilment → returns). See `docs/roadmap/sprint-plan.md` (1-week sprints). Each sprint delivers a module that runs end to end (database → API → admin UI) and is tested.
- **No authentication yet.** The early sprints run locally without login. Staff authentication (S8) must be done before anything is deployed to a server.
- **CQRS dispatch:** MediatR. It requires a licence key, which is read from configuration or secrets and never committed.

## Open decisions (don't assume an answer)

- **FR-39 vs SEC-02**: hard account lockout, or progressive throttling + CAPTCHA? The recommendation is SEC-02, but it's unconfirmed.
- **BRD errata** (SCRUM-168): FR-10 cites "FR-20" but means FR-28. The supply-chain section cites old section numbers.
