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
    src/Modules/<Name>/    Domain / Application / Infrastructure per module, one folder per feature
    tests/                 architecture, module and integration tests (mirror the feature folders)
  web/                     Angular 22 workspace
    projects/storefront/   customer site, server-side rendered
    projects/admin/        back-office app (features/<module>/<feature>)
    e2e/                   Playwright specs (e2e/<module>/<feature>.e2e.spec.ts)
docs/CONSTITUTION.md       the module/feature hierarchy used everywhere, and the architecture rules
docs/specs/                <module>/<feature>/ specs, plans and tasks
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
Acknowledged (`docs/specs/vendor/_sprint-specs/po-creation-and-master-data/`). To see it end to end:

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
dispatcher (`docs/specs/vendor/_sprint-specs/po-amendments-and-spec-files/`). Same stack as Sprint 1
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

## Try Sprint 3 (production tracking)

Sprint 3 follows a purchase order after the vendor acknowledges it: milestones, the pre-production
sample gate, expected completion and vendor delivery notes (`docs/specs/production/_sprint-specs/milestones-and-deliveries/`).
Same stack (`docker compose up -d --build`). The demo seed adds six more purchase orders, **PO-2026-90002**
to **PO-2026-90007**, one in each production state.

**Demo script** (admin app, **Production** in the top navigation):

1. **See every run.** The list shows each acknowledged PO with its sample, expected completion and what has
   shipped. Warnings are written out ("At risk of being late", "Delivered short"), never colour alone.
   Filter by PO number, status or *At risk of being late*.
2. **Open PO-2026-90003** (a sample rejected once, then approved). The *Pre-production sample* section shows
   round 1 with the reason it was rejected and round 2 approved. Only now can bulk cutting be recorded.
3. **Try the gate.** Open **PO-2026-90002** (no sample yet), choose *Record a milestone*: *Bulk cutting started* is
   listed but greyed out with the reason. *Record a sample*, then decide it (rejecting needs a reason), attach a
   photo, approve, and record bulk cutting.
4. **Watch the PO follow.** Bulk cutting moves the purchase order to *In Production*, and delivery notes move it to
   *Partially Delivered* and *Delivered*. PROD and VNDR never touch each other's data: events cross between them
   through the outbox, so the PO catches up a few seconds later. The footer's *Background updates* line says
   whether anything is waiting or stuck (`GET /api/admin/outbox`).
5. **Record deliveries.** On **PO-2026-90005** (partially delivered) record another delivery note. The form shows
   what was ordered and what has already shipped. Tick *final shipment* to say nothing more is coming; a short
   final shipment still counts as Delivered but is flagged "Short by N pieces". More than the allowance is recorded
   and flagged "Over by N pieces", never refused.
6. **Close it.** **PO-2026-90006** is Delivered (short, final shipment). Open the PO and choose *Close order*.
   Closing is always manual.
7. **Cancel after production started.** **PO-2026-90007** was cancelled in production. On another In Production PO,
   *Cancel PO* shows a prominent warning and needs a tick to confirm you understand the vendor may have spent
   money; the reason *Cancelled after production started* is offered.
8. **Amend after a sample was approved.** Amending an order whose sample was approved shows a "costly for the
   vendor" notice. A change to fabric supplier, colour or files also says a new sample is strongly recommended
   (and the run then shows that as a warning); a change to price, dates or terms says a new sample is optional.

Every action asks for your name (kept in this browser for next time), because there is no sign-in until Sprint 8.
Settings: `Prod__RecordedLateDays` (default 3) and `Prod__AtRiskBufferDays` (default 7) in `docker-compose.yml`.

## Try Sprint 4 (receiving, quality control and the vendor scorecard)

Sprint 4 takes a vendor's delivery from the dock to the shelf: count it against the delivery note (goods receipt), inspect it,
record the two child-safety checks, decide what happens to it, and follow anything sent back for rework
(`docs/specs/quality/_sprint-specs/receiving-and-inspection/`). Same stack (`docker compose up -d --build`). The demo seed adds nine delivered
purchase orders, **PO-2026-91001** to **PO-2026-91009**, one for each state below.

**Demo script** (admin app, **Receiving & QC** in the top navigation):

1. **The receiving queue.** Every vendor delivery note recorded on a production run shows up here. *Waiting to be received* is the
   default view; the demo deliveries are already received, so switch the filter to *Received* (or *All deliveries*). To count a
   new one, record a delivery note on a production run (Sprint 3), wait a few seconds for the event, and choose *Receive goods*.
2. **Count the goods.** *Receive goods* starts every count at what the vendor claims. Change a number and the page says
   "Short by 5" or "Over by 4" in words. A difference is recorded, never refused. Each delivery note gets exactly one goods
   receipt (`GRN-YYYY-NNNNN`); trying again says so.
3. **See each state in QC batches.** Open **QC batches** and look at each demo batch:
   **91001** counted, not inspected; **91002** short count, accepted; **91003** accepted with a concession;
   **91004** rejected after a critical defect; **91005** part accepted, the rest waiting for rework at the vendor;
   **91006** rework came back, was re-inspected and accepted; **91007** split into a passing and a rejected part;
   **91008** a failed choking-hazard check on the size 5 lines; **91009** more arrived than the delivery note says.
4. **Inspect a batch.** On **91001** choose *Inspect the batch*. The form shows the AQL plan that will apply (sample size, how many
   defects pass or fail), one Pass/Fail per tech-pack item and a list of defects. A failed item needs a defect of its type, and
   every defect needs a written note. The result is worked out for you, never typed in. Small lots (50 pieces or fewer) are
   inspected 100%.
5. **The child-safety checks.** Both (choking hazard on small trims, drawstring safety on necklines) must be recorded before
   anything can be accepted; try recording an outcome first and it says so. *Not applicable* needs a reason. If a check fails for
   some sizes and colours, that quantity can only be rejected or sent for rework, and there is no override.
6. **Decide, or split.** Choose *Record the outcome*. Give the whole batch one outcome, or *Split it* so the passing part is
   released now and does not wait for the rest. A split must place every piece: the balance table says what is still to place.
   Concession, Reject and Rework need a reason.
7. **Follow the rework.** On **91005**, choose *Record return*, enter how many came back (a short return leaves the rest outstanding),
   *Re-inspect* the returned part, record its safety checks again, and *Record outcome*. Rounds are unlimited.
8. **Vendor scorecard.** Open **Vendors**, choose *Scorecard* for a vendor: on-time %, on-quantity % and defect rate, all time and
   for the last 90 days, each with the counts behind it. A measure with no data says "No data yet", never 0% or 100%. Deliveries that
   were late only because Romp sent the fabric late are left out of the on-time figure and shown as a count.
9. **Goods received against the order.** On a purchase order's screen, *Goods received* shows counted against ordered per size and
   colour. It never changes the order's status; closing an order stays a manual step.

Every action asks for your name (kept in this browser for next time), because there is no sign-in until Sprint 8.
Settings in `docker-compose.yml`: `Qc__FullInspectionMaxLot` (50), `Qc__DefaultInspectionLevelCode` (II), `Qc__DefaultAqlLevelCode`
(Aql2_5), `Qc__CriticalDefectTolerance` (0) and `Vndr__Scorecard__WindowDays` (90). The AQL tables are data seeded by the QC migration;
check them against the published ISO 2859-1 tables before production use (see the spec's implementation notes).

## Try Sprint 5 (labelling: SKUs, barcodes and tag printing)

Sprint 5 turns stock that QC has accepted into something that can be shelved and sold: every size-and-colour gets a readable SKU
and its own GTIN-13 barcode number, and the tags are printed through a queue that retries when a printer jams
(`docs/specs/labelling/`). Same stack (`docker compose up -d --build`). The accepted batches from the Sprint 4 demo reach
**Labelling** on their own a few seconds after the stack starts.

**Demo script** (admin app, **Labelling** in the top navigation, three screens):

1. **Worklist.** Each accepted line of a QC batch is one row, already carrying its SKU (`ROMP-<style>-<colour>-<size>`) and barcode
   number. Concession stock is marked "Concession (2ND)" because its tag carries a small "2ND" mark. *Preview tag* opens the PDF of
   exactly what will be printed.
2. **Queue tags.** Choose *Queue tags*, a count (never more than the accepted quantity) and a printer. Saving never waits for the
   printer: the job goes into the queue and prints in the background. Try asking for more than are allowed and it says how many are left.
3. **Print queue.** Jobs show *Waiting*, *Printing now*, *Printed* or *Failed*, and refresh by themselves. Queue a job on the
   **Jammed printer (always fails)** to see the retry path: it tries again after a few seconds, then ends *Failed* with the reason.
   *Retry* runs the same job again; *Cancel* ends it. A jammed printer never stops the others.
4. **SKU register.** Search by SKU or by barcode number. The banner shows how much of the barcode range is used and, in this demo,
   that the **GS1 test prefix** is in use (numbers made now must never go on real stock).
5. **Reprint.** *Reprint* on a SKU opens the Print queue with a reprint form: a count, a reason, and your name. The tag shows
   today's price and the **same** barcode number, so the SKU's history stays continuous. *Print history* lists every printing.
6. **Void a barcode.** *Void barcode* (with a reason) retires the number for good: it can never be used again, tags already on
   garments are untouched, and the SKU gets a new number with its next accepted stock. *Barcode history* shows the retired number.
7. **Price changed?** Change a style's target retail price, then look at the worklist: rows whose tags were queued before the change
   say so, and a reprint puts the new price on the tag.

Every action asks for your name (kept in this browser for next time), because there is no sign-in until Sprint 8. For the same reason
the API now refuses to start outside Development unless real authentication is registered (`docs/adr/0011-security-and-coupling-baseline.md`).
Settings under `Labelling:` (see `appsettings.Development.json`): `CompanyPrefix` (the GS1 test range `0200000` until your real prefix
arrives; **production refuses a test prefix**), `SparePercent` (0), `MaxTagsPerJob` (5000), `MaxAttempts`, `BackoffSeconds`,
`AllowFakePrinters` (Development and Testing only) and `AllowedPrinterHosts` for a real label printer.

## Try Sprint 6 (warehouse: bins, putaway, stock and alerts)

Sprint 6 puts accepted, tagged stock on a shelf and keeps an exact, append-only record of where every unit is
(`docs/specs/warehouse/`). Same stack (`docker compose up -d --build`). Accepted batches from the earlier demos reach
**Warehouse** on their own a few seconds after the stack starts.

**Demo script** (admin app, **Warehouse** in the top navigation):

1. **Bins.** Add a bin as zone + aisle + shelf (for example `A`, `03`, `2` gives `A-03-2`). A duplicate or a code outside the pattern is
   refused in plain words. A bin that holds stock cannot be deactivated.
2. **Putaway.** Stock QC accepted is *not available to sell* until it is shelved. The cursor sits in the barcode box: scan (or type) the
   13-digit barcode and press Enter, give the bin and the quantity, and confirm. Part of a batch can go to different bins. An item
   with no tag yet is chosen from the list instead (search by SKU). Asking for more than is waiting is refused and points you to a
   *Found stock* adjustment.
3. **Stock.** On hand per variant and grade (accepted or concession), with the bins it sits in, and a full movement history
   (who, when, before and after). Nothing here can be edited: corrections are new movements.
4. **Adjustments.** Add or remove units with a reason: *Damage*, *Miscount*, *Theft* or *Found stock*. A change of more than 20 units
   (setting `Warehouse:LargeAdjustmentUnits`) asks you to confirm a second time.
5. **Cycle counts.** Pick bins (the picker lists the longest-uncounted first and can be narrowed by zone), count them **blind** (the
   system quantity is hidden), then review the differences. Each difference needs a reason and becomes an audited adjustment on
   confirm. Stock that moved while you were counting is flagged for a recount.
6. **Alerts.** A variant falling to its threshold (default 5, set per variant) raises a *Low stock* alert; reaching zero escalates it
   to *Out of stock*; restocking closes it by itself. The count shows as a badge on the Warehouse menu. Each alert can be
   acknowledged. Alerts are in-app and an event only: no email or WhatsApp.

Every action asks for your name (kept in this browser), because there is no sign-in until Sprint 8; the `Inventory` role is declared
and enforced then. Stock changes reach other modules through the outbox within seconds; the dispatcher's poll interval is
`Outbox:PollIntervalSeconds` (default 1). Open questions: bin code format (Q-BIN-1) and what to do with unshelved stock of a voided
barcode (Q-PUT-2); see the module README.

## Try Sprint 7 (catalog: publish a product)

Sprint 7 turns an accepted, labelled style into a product shoppers will be able to buy (`docs/specs/catalog/`). Same stack
(`docker compose up -d --build`). A sample girls-dresses size chart is seeded in Development.

**Demo script** (admin app, **Catalog** in the top navigation):

1. **Create.** Products, then *Create a product*: choose a style, name it, say who it is for, the category and the age group. It starts
   as a **Draft**. Its sizes and colours are the style's. A size gets its SKU and barcode only when Labelling has issued one; the others
   read "Not yet available" and do not stop you publishing.
2. **Variants and prices.** Set a price (PKR) per variant and, optionally, a higher "was" price. Every price change is kept as history.
3. **Images.** Add pictures (JPEG, PNG or WebP; the file type is checked from the file itself, not its name) with alt text. Alt text is
   required once a product is published.
4. **Size chart.** Catalog, then *Size charts*: one chart per category and audience. The product's Size chart tab shows it; with no chart
   the product can still be published and the shop says "size chart coming soon".
5. **Composition and care.** Fibre shares must total exactly 100% before publishing; the running total is announced as you type. Pick the
   care instructions.
6. **URL.** The address is built from audience, category and name (`/girls/dresses/floral-summer-dress`). Rename it and the old address
   keeps working (a permanent redirect for the shop); reserved words such as `checkout` are refused.
7. **Publish.** The Publish tab lists **every** missing item at once, each a link to the tab that fixes it. *Take off sale* hides the
   product without deleting anything; *Archive* is final and keeps the product for order history.
8. **Two editors.** If someone else saved the product first, your save is refused with a message and a *Reload* button.

Every action asks for your name (kept in this browser), because there is no sign-in until Sprint 8; the `Catalogue` role is declared and
enforced then. Publishing writes an event through the outbox; the storefront that reads it arrives in Sprint 9. Open questions (defaults
adopted, see `docs/specs/catalog/README.md`): the age-band names (Q-AGE-2), the launch fibre and care lists, and the URL shape for unisex.

## Try Sprint 9 (storefront settings)
Staff change the shop's menu, ages, footer, contact, delivery and exchange facts and content pages in the admin app; a publish puts the whole set on the shop within a minute (SCRUM-206, [spec](docs/specs/storefront/storefront-settings/spec.md)).

```bash
docker compose up -d --build api admin storefront   # admin :4201, storefront :4000, API :8080
```

1. Open http://localhost:4201/storefront (the **Storefront** link at the right end of the top bar). Edit the navigation, then **Pages**: mark the pages you want live as published.
2. Open **Publish**, add a note and press **Publish now**. Until the first publish the shop shows its built-in launch set.
3. Reload http://localhost:4000 within a minute: your menu and pages are there; an archived page answers 410.
4. Opening hours, the four delivery cities and the exchange window are **placeholders**: replace them under Settings and Delivery before launch. The settings screens have no login until Sprint 8, so they must not be deployed publicly yet.

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
