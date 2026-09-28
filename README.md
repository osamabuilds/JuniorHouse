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

## Try Sprint 2 (amendments, files, event dispatch)

Sprint 2 adds versioned PO amendments, spec-file attachments, a vendor-facing view and the outbox
dispatcher (`docs/specs/SCRUM-93-procurement-part-2/`). Same stack as Sprint 1
(`docker compose up --build`); the demo seed also creates **PO-2026-90001**: an Acknowledged PO
with the vendor's counter-proposal already waiting as a Pending revision.

**Demo script** (admin app, **Purchase Orders**):

1. **See an amendment waiting.** Open PO-2026-90001. The *Revision 1 awaits a decision* panel says
   who proposed it and whose decision it is. The *Revision history* below shows Rev 0 (the terms as
   sent) and Rev 1 with a before/after of every changed term, the impact (PO value, advance amount,
   delivery shift) and the internal note.
2. **Decide it.** Choose *Accept*. Rev 1 goes *In force*, Rev 0 becomes *Superseded* and the PO's
   terms above now show PKR 480 and the new delivery date. (*Reject* would leave the terms alone;
   *Withdraw* is the proposer taking it back.)
3. **Send a PO properly.** Raise a PO (the form now has *Latest acceptable delivery date* and
   *Fabric responsibility*, both required before Send; tolerances are optional). Under **Files**,
   upload a Tech Pack Spec (PDF, PNG, JPEG, Excel or Word). Click *Send to Vendor*: the checklist
   is a reminder, and without a tech pack you must tick *Send anyway*, which is recorded on the
   status timeline.
4. **Record what the vendor said.** *Record vendor response* replaces the old Acknowledge button.
   Pick *Confirmed*, *Countered* (opens the counter-proposal form) or *Declined*, plus the channel
   (WhatsApp, phone, ...), who responded, when, and optionally an evidence screenshot. A declined
   PO is not cancelled for you; *Cancel* opens with "Vendor declined" already selected.
5. **Amend it.** On a Sent or Acknowledged PO, *Amend* lets you change terms, quantities and
   vendor-visible files; a reason and an internal note are mandatory. On a *Sent* PO the change is
   in force immediately; on an *Acknowledged* PO it waits as Pending. A change that alters nothing
   is rejected.
6. **See what the vendor sees.** *Vendor view* opens a read-only page with no admin navigation
   (print it to A4). It never shows the internal note, cost sheets, evidence or target prices.
7. **Watch events flow.** Every change writes an event to the outbox in the same transaction; a
   background dispatcher delivers it (the placeholder handler just logs it):

   ```bash
   docker compose exec postgres psql -U romp -d romp -c \
     'SELECT "EVNT_TYP", "AGGR_ID", "PROC_DTE", "ATMP_CNT" FROM "VNDR"."OUTB_MSG" ORDER BY "ID"'
   ```

   Rows with a `PROC_DTE` were delivered; a row that keeps failing is retried with back-off and
   finally marked dead-lettered (`DEDL_IND`).

Uploaded PO files live in the `po-files` Docker volume. Validation problems name the actual rule
that was broken (for example "A channel is required.") instead of a generic message.

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
