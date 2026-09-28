# Romp

A direct-to-consumer kidswear platform for Pakistan: the storefront (with Fit Finder and Child Profiles) plus the supply-chain back office (vendors, QC, barcodes, warehouse, packing, couriers, returns).

- **Spec:** `Romp_Master_v9_Merged.docx` (BRD v9.0)
- **Backlog:** Jira project `SCRUM` at osamabuss048.atlassian.net
- **Architecture decisions:** [`docs/adr/`](docs/adr)

## Repository layout

```
src/
  api/                     .NET 10 modular monolith (Romp.slnx)
    src/Romp.Api/          host: loads modules, health checks, OpenAPI
    src/BuildingBlocks/    shared kernel (Entity, AggregateRoot, IDomainEvent, IModule)
    src/Modules/<Name>/    Domain / Application / Infrastructure per module
    tests/                 architecture + integration tests
  web/                     Angular 22 workspace
    projects/storefront/   customer site, server-side rendered
    projects/admin/        back-office app
docs/adr/                  architecture decision records
```

## Prerequisites

.NET SDK 10, Node 24, and Docker.

## Running locally

```bash
cp .env.example .env              # then set POSTGRES_PASSWORD
docker compose up -d postgres redis mailpit

# API: http://localhost:5051/health/live
cd src/api && dotnet run --project src/Romp.Api

# Storefront: http://localhost:4200
cd src/web && npm ci && npx ng serve storefront
# Admin
cd src/web && npx ng serve admin --port 4201
```

To run the whole stack in containers, use `docker compose up --build`.

## Try Sprint 1 (procurement)

Sprint 1 covers reference data, the style master, vendors, and purchase orders through
Acknowledged (`docs/specs/SCRUM-92-procurement-part-1/`). To see it end to end:

```bash
cp .env.example .env              # then set POSTGRES_PASSWORD
docker compose up --build
```

This starts Postgres, the API (migrations + REF lookup seed run automatically), the admin app,
and the storefront in one command, and seeds two sample styles and two sample vendors
(`DemoDataSeeder`, idempotent - safe to re-run) so the admin app isn't empty on first load.

Then, in the admin app at **http://localhost:4201**:

1. **Reference Data** - confirm sizes, colours, categories, cities, payment terms etc. are
   populated (seeded by migration); add a value if you need one that isn't there.
2. **Styles** - open one of the two seeded styles, or create your own: code, category, gender,
   age bracket, fabric, colourways, size run, and a target quantity per size × colour.
3. **Vendors** - open a seeded vendor, or create your own: contact, city, one or more
   specialisations, default payment term.
4. **Purchase Orders** - raise a PO against a vendor and a style. The payment term and advance %
   pre-fill from the vendor's default; the size × colour lines are validated against the style's
   own size run and colourways. Save it (Draft), then use the detail view's **Send to Vendor** and
   **Acknowledge** actions to walk it through the state machine, and watch the status timeline
   grow. **Cancel** is available from any of Draft/Sent/Acknowledged, with a mandatory reason.

The API's OpenAPI document is at `http://localhost:8080/openapi/v1.json` in Development.

## Tests

```bash
cd src/api && dotnet test Romp.slnx
cd src/web && npx ng test storefront --watch=false && npx ng test admin --watch=false

# End-to-end (needs the stack running - see "Try Sprint 1" above):
cd src/web && npx playwright install --with-deps chromium && npm run e2e
```

## Workflow

Features follow spec-driven development: spec → plan → tasks → test-first implementation. See [`CLAUDE.md`](CLAUDE.md) and the templates in [`docs/specs/_template/`](docs/specs/_template).

- One branch per Jira issue, e.g. `SCRUM-162-ef-core-wiring`, and a PR into `main`.
- CI (GitHub Actions) must pass before merging.
