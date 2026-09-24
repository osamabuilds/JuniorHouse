# SCRUM-92: Technical plan

- **Spec:** [spec.md](spec.md)
- **Module(s):** `REF` (shared reference data), `CTLG` (Catalog — style master only), `VNDR` (Vendor & Procurement)
- **New ADR:** [0006 — Business document number generation](../../adr/0006-business-number-generation.md)
- **Glossary additions (already applied to `docs/db/naming.md`):** `advance→ADV`, `bracket→BRKT`, `on-quantity→ONQT`, `on-time→ONTM`, `rate→RATE`, `year→YR`

## Domain model

**`REF` (lookups — no aggregates, just reference rows; each is independently maintained, not part of any other aggregate):**
`SIZE_LKP`, `CLR_LKP`, `FBRC_LKP`, `GNDR_LKP`, `AGE_BRKT_LKP`, `CATG_LKP` (self-referencing via `PRNT_CATG_ID` for hierarchy), `CITY_LKP`, `PAYM_TERM_LKP` (carries `DFLT_ADV_PCT`), `VNDR_SPCL_LKP`, `PO_STS_LKP` (system-owned, no admin CRUD), `PO_CNCL_RSN_LKP`. Invariant: a retired (`ACT_IND=false`) row is never returned for new selection but is never deleted, so historical rows that reference it stay valid (spec AC-2).

**`CTLG.Style` aggregate:**
- Root: `STYL_MAIN` — code (unique), name, collection (free text), category, gender, age bracket, fabric, target unit cost, target retail price.
- Children: `STYL_CLR_MAP` (which colourways this style comes in), `STYL_SIZE_MAP` (its size run), `STYL_TGT_LINE` (target quantity for each size×colour cell — only valid for combinations present in the two maps above).
- Invariant: `STYL_TGT_LINE` rows can only reference a `(SIZE_ID, CLR_ID)` pair that exists in `STYL_SIZE_MAP`/`STYL_CLR_MAP` for the same style — enforced in the command handler (a DB constraint can't easily express "this size and colour are each individually valid for this style," since that's two separate map tables).

**`VNDR.Vendor` aggregate:**
- Root: `VNDR` (schema `VNDR`, table `VNDR` — kept without a `_MAIN` suffix, matching `docs/db/naming.md`'s own example) — name, contact, city, default payment term, active flag, and three nullable scorecard metrics (`ONTM_PCT`, `ONQT_PCT`, `DFCT_RATE_PCT`) that stay `null` (not `0`) until Sprint 4 starts calculating them, so "not yet measured" is never confused with "0% on-time."
- Child: `VNDR_SPCL_MAP` — many-to-many, a vendor can hold more than one specialisation (Decisions in spec.md).

**`VNDR.PurchaseOrder` aggregate — the only aggregate with real behaviour/invariants in this sprint:**
- Root: `PO_MAIN` — PO number (system-generated, ADR 0006), vendor reference, style reference, unit cost, expected delivery date, payment term + advance % (copied from the vendor's default at creation, independently editable), current status.
- Children: `PO_LINE` (quantity per size×colour, validated against the referenced style's `STYL_SIZE_MAP`/`STYL_CLR_MAP`), `PO_STS_HIST` (append-only status-transition ledger — immutable, so it skips `UPDT_DTE/BY` and the concurrency token per `docs/db/naming.md` rule 6's append-only-ledger allowance).
- State machine (this sprint's slice): `Draft → SentToVendor → Acknowledged`, plus `Cancelled` reachable from any of those three. No other transitions exist yet (In Production onward is Sprint 3+, out of scope).
- Invariants: quantity lines can only be edited while `Draft`; once `Acknowledged`, only `Cancel` (with a mandatory reason) is a legal action — everything else requires FR-SC-03 amendment, out of scope. Every transition writes one `PO_STS_HIST` row and one outbox event, in the same transaction as the status change.

Cross-module references (`PO_MAIN.STYL_ID` → a `CTLG` style, any `*_ID` column pointing at a `REF` lookup) are plain `bigint`/`smallint` columns with **no database-level foreign key** — per `docs/db/naming.md` rule 7, modules never take a cross-schema FK. Validity is checked at the command-handler level through `Romp.Modules.Catalog.Contracts` / `Romp.Modules.Reference.Contracts` query interfaces before the write, not by the database.

## Application layer

The nine staff-maintained `REF` lookups (`Sizes`, `Colours`, `Fabrics`, `Genders`, `AgeBrackets`, `Categories`, `Cities`, `PaymentTerms`, `VendorSpecialisations`) share one CRUD shape — listed once instead of nine near-identical rows:

| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `ListLookupQuery<T>` (one per lookup type) | none (or `includeInactive: bool` for admin) | `LookupDto[]` | AC-1, AC-2 |
| `CreateLookupCommand<T>` / `UpdateLookupCommand<T>` / `RetireLookupCommand<T>` | code, name, description, sort order | `LookupDto` | SCRUM-172 |
| `ListPoStatusesQuery`, `ListPoCancelReasonsQuery` | none | `LookupDto[]` | AC-1 (`PO_STS_LKP` is read-only — no create/update/retire handler exists for it) |
| `CreateStyleCommand` | code, name, collection, category, gender, age bracket, fabric, colourways[], size run[], target unit cost, target retail price, target qty per size×colour[] | `StyleId` | AC-3, AC-4 |
| `UpdateStyleCommand` | style id + same fields | — | AC-3 |
| `GetStyleByIdQuery` / `SearchStylesQuery` | id / filters (code, name, category, active) | `StyleDto` / paged `StyleSummaryDto[]` | AC-3 |
| `CreateVendorCommand` | name, contact, city, specialisations[], default payment term | `VendorId` | AC-5, AC-5a |
| `UpdateVendorCommand` | vendor id + same fields + active flag | — | AC-6 |
| `GetVendorByIdQuery` / `SearchVendorsQuery` | id / filters (name, city, specialisation, active) | `VendorDto` / paged `VendorSummaryDto[]` | AC-5, AC-6 |
| `CreatePurchaseOrderCommand` | vendor id, style id, lines[] (size, colour, qty), unit cost, expected delivery date, payment term id, advance % | `PoId`, `PoNo` | AC-7, AC-7a, AC-7b, AC-8 |
| `UpdatePurchaseOrderCommand` | po id + same editable fields (Draft only) | — | AC-9, AC-13 |
| `SendPurchaseOrderCommand` | po id | — | AC-10 |
| `AcknowledgePurchaseOrderCommand` | po id | — | AC-11 |
| `CancelPurchaseOrderCommand` | po id, cancel reason id | — | AC-12, AC-12a |
| `GetPurchaseOrderByIdQuery` | id | `PoDto` (includes `PO_STS_HIST` timeline) | AC-14 |
| `SearchPurchaseOrdersQuery` | filters (vendor, status, date range) | paged `PoSummaryDto[]` | — |

All commands go through the MediatR validation/logging/transaction pipeline (SCRUM-171); a failing `FluentValidation` rule surfaces as field-level `ProblemDetails` (AC-15).

## API endpoints

No authentication or role checks exist yet in Sprint 1 (CLAUDE.md scope decision — SEC-08 lands S8); "Auth / role" is `None (S1)` throughout, and "Ownership check" is `N/A` since these are internal staff records with no per-customer owner (SEC-05 governs customer-owned data, not back-office records).

| Method | Route | Auth / role | Ownership check (SEC-05) |
|---|---|---|---|
| `GET` | `/api/ref/{lookup}` (`sizes`, `colours`, `fabrics`, `genders`, `age-brackets`, `categories`, `cities`, `payment-terms`, `vendor-specialisations`, `po-statuses`, `po-cancel-reasons`) | None (S1) | N/A |
| `POST` / `PUT` / `POST .../{id}/retire` | `/api/ref/{lookup}` (all except `po-statuses`) | None (S1) | N/A |
| `POST` | `/api/catalog/styles` | None (S1) | N/A |
| `PUT` | `/api/catalog/styles/{id}` | None (S1) | N/A |
| `GET` | `/api/catalog/styles/{id}` , `/api/catalog/styles` (search) | None (S1) | N/A |
| `POST` | `/api/vendors` | None (S1) | N/A |
| `PUT` | `/api/vendors/{id}` | None (S1) | N/A |
| `GET` | `/api/vendors/{id}`, `/api/vendors` (search) | None (S1) | N/A |
| `POST` | `/api/purchase-orders` | None (S1) | N/A |
| `PUT` | `/api/purchase-orders/{id}` | None (S1) | N/A |
| `POST` | `/api/purchase-orders/{id}/send` | None (S1) | N/A |
| `POST` | `/api/purchase-orders/{id}/acknowledge` | None (S1) | N/A |
| `POST` | `/api/purchase-orders/{id}/cancel` | None (S1) | N/A |
| `GET` | `/api/purchase-orders/{id}`, `/api/purchase-orders` (search) | None (S1) | N/A |

## Events

| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|
| `PoCreated` (Draft) | `CreatePurchaseOrderCommand` | none yet | Yes, `VNDR.OUTB_MSG` (write only) |
| `PoSentToVendor` | `SendPurchaseOrderCommand` | none yet | Yes |
| `PoAcknowledged` | `AcknowledgePurchaseOrderCommand` | none yet | Yes |
| `PoCancelled` (includes reason) | `CancelPurchaseOrderCommand` | none yet | Yes |

The background dispatcher (retry/backoff/dead-letter, per ADR 0004) is built in SCRUM-165 as a shared building block but its *consumer* side has nothing to dispatch to this sprint — the first real consumer (Production Tracking, reacting to `PoAcknowledged`) arrives in Sprint 2/3. `VNDR.OUTB_MSG`'s own column shape is owned by SCRUM-165, not redefined here.

## Data

Schema/table list (all columns follow `docs/db/naming.md`; every transactional table also gets `INSR_DTE`, `INSR_BY`, `UPDT_DTE`, `UPDT_BY` and an `xmin` concurrency token unless noted as an immutable ledger row):

**`REF` schema**
- `SIZE_LKP`, `CLR_LKP`, `FBRC_LKP`, `GNDR_LKP`, `AGE_BRKT_LKP`, `CITY_LKP`, `VNDR_SPCL_LKP`, `PO_STS_LKP`, `PO_CNCL_RSN_LKP` — standard lookup shape (`ID` smallint PK, `CODE` unique, `NAME`, `DSCR`, `SORT_SEQ`, `ACT_IND`).
- `CATG_LKP` — standard lookup shape + `PRNT_CATG_ID smallint null` (self-FK, same schema, real FK + index) for the hierarchy.
- `PAYM_TERM_LKP` — standard lookup shape + `DFLT_ADV_PCT numeric(5,2)`.

**`CTLG` schema**
- `STYL_MAIN` (`ID bigint` PK, `STYL_CODE varchar(30)` unique, `STYL_NAME varchar(200)`, `COLN_NAME varchar(100) null`, `CATG_ID smallint`, `GNDR_ID smallint`, `AGE_BRKT_ID smallint`, `FBRC_ID smallint`, `TGT_UNIT_COST_AMT numeric(12,2)`, `TGT_RTL_PRIC_AMT numeric(12,2)`, `ACT_IND boolean default true`, audit + `xmin`).
- `STYL_CLR_MAP` (`STYL_ID bigint` FK→`STYL_MAIN`, `CLR_ID smallint`, composite PK).
- `STYL_SIZE_MAP` (`STYL_ID bigint` FK→`STYL_MAIN`, `SIZE_ID smallint`, composite PK).
- `STYL_TGT_LINE` (`ID bigint` PK, `STYL_ID bigint` FK→`STYL_MAIN`, `SIZE_ID smallint`, `CLR_ID smallint`, `TGT_QTY integer`, unique on `(STYL_ID, SIZE_ID, CLR_ID)`, audit + `xmin`).

**`VNDR` schema**
- `VNDR` (`ID bigint` PK, `VNDR_NAME varchar(200)`, `CNTC_NAME varchar(100)`, `CNTC_PHON varchar(20)`, `CNTC_EML varchar(200) null`, `CITY_ID smallint`, `PAYM_TERM_ID smallint`, `ONTM_PCT numeric(5,2) null`, `ONQT_PCT numeric(5,2) null`, `DFCT_RATE_PCT numeric(5,2) null`, `ACT_IND boolean default true`, audit + `xmin`).
- `VNDR_SPCL_MAP` (`VNDR_ID bigint` FK→`VNDR`, `SPCL_ID smallint`, composite PK).
- `PO_MAIN` (`ID bigint` PK, `PO_NO varchar(15)` unique, `VNDR_ID bigint` FK→`VNDR`, `STYL_ID bigint` [cross-schema, app-validated, indexed, no FK], `UNIT_COST_AMT numeric(12,2)`, `EXPC_DLVR_DT date`, `PAYM_TERM_ID smallint`, `ADV_PCT numeric(5,2)`, `PO_STS_ID smallint`, audit + `xmin`).
- `PO_LINE` (`ID bigint` PK, `PO_ID bigint` FK→`PO_MAIN`, `SIZE_ID smallint`, `CLR_ID smallint`, `QTY integer`, unique on `(PO_ID, SIZE_ID, CLR_ID)`, audit + `xmin`).
- `PO_STS_HIST` (`ID bigint` PK, `PO_ID bigint` FK→`PO_MAIN`, `PO_STS_ID smallint`, `PO_CNCL_RSN_ID smallint null`, `INSR_DTE timestamptz`, `INSR_BY varchar(100)` — append-only, no `UPDT_*`/`xmin`, per naming.md rule 6).
- `PO_NO_SEQ` (`YR smallint` PK, `SEQ integer not null default 0` — the ADR 0006 counter; internal allocation primitive, not business data, so it's exempt from the audit-column rule).
- `OUTB_MSG` — shape owned by SCRUM-165.

Migrations: one migration per schema (`REF`, `CTLG`, `VNDR`), applied at API startup in Development only (SCRUM-162). Lookup seed data is part of the `REF` migration, version-controlled per §12.4's requirement that seed data never be entered manually in an environment.

Partitioning: none of these tables are expected to grow large in the way orders/stock-ledger will (BRD §12.6) — no partitioning key needed for this sprint's tables.

## NFR / security design

- **Money (`numeric(12,2)`):** `TGT_UNIT_COST_AMT`, `TGT_RTL_PRIC_AMT`, `UNIT_COST_AMT` all typed `numeric(12,2)`.
- **PO number concurrency safety (ADR 0006):** atomic `INSERT ... ON CONFLICT DO UPDATE ... RETURNING` on `PO_NO_SEQ`, in the same transaction as the `PO_MAIN` insert — no read-then-increment race.
- **DB naming enforcement:** SCRUM-170's architecture test covers every table/column added here; nothing here is exempt.
- **Module boundary:** confirmed above — no cross-schema FK anywhere; `CTLG`/`REF` data reaches `VNDR` only through their `.Contracts` query interfaces.
- **Outbox writer:** `PoCreated`/`PoSentToVendor`/`PoAcknowledged`/`PoCancelled` all written transactionally via the SCRUM-165 EF interceptor.
- **No auth (deliberate gap):** every endpoint above is open; this closes in S8 (SEC-08). Flagged here so the S8 spec has a concrete endpoint list to lock down, rather than rediscovering it.
- **Validation:** every command runs through FluentValidation in the MediatR pipeline (SCRUM-171) before touching the database.

## Frontend

Admin app (SCRUM-174 shell) gains four nav sections:
- **Reference Data** — one screen with a lookup-type selector (tabs or a side list), a table with search, and add/edit/retire forms. `PO Statuses` shown read-only (no add/edit/retire controls rendered for it).
- **Styles** — list (search by code/name/category, filter by active), create/edit form with a colour × size grid for entering target quantities (only cells for colours/sizes actually in that style's colourways/size run are editable).
- **Vendors** — list (search by name/city/specialisation), create/edit form with a multi-select for specialisations.
- **Purchase Orders** — list (filter by vendor/status/date), create/edit form (Draft only; locked once Sent/Acknowledged/Cancelled), detail view showing the PO_STS_HIST timeline and the available status actions (Send / Acknowledge / Cancel-with-reason) as buttons, each disabled when illegal for the current status.

All screens: semantic layout inherited from the SCRUM-174 shell, `<label>` on every form control, `ProblemDetails` field errors shown inline, `noindex, nofollow` (admin app-wide), WCAG 2.1 AA baseline (keyboard operable status-action buttons, visible focus). No SSR/SEO requirements apply — this is the admin app, not the storefront.

## Risks & alternatives considered

- **PO number generation pattern sets precedent** for GRN/RMA/waybill numbers in later sprints — captured in new ADR 0006 rather than left as an undocumented one-off choice, so later sprints don't have to re-litigate it.
- **Considered a single polymorphic `REF.LOOKUP` table** (one table, a `TYPE` discriminator column, shared by all nine lookup types) instead of nine separate tables. Rejected: it would need a nullable/overloaded shape to hold `DFLT_ADV_PCT` (payment terms only) and `PRNT_CATG_ID` (categories only), which either forces those columns onto every other lookup row or requires a side table anyway — the nine-small-tables approach stays closer to 3NF and matches the shape the BRD's own §12.4 catalogue already uses for its lookups.
- **Considered storing PO unit cost per line instead of once on `PO_MAIN`.** The BRD's §5.9 text uses the singular "agreed unit cost" for the whole PO, and Sprint 1 POs are single-style, so one cost value on `PO_MAIN` matches both the BRD wording and Jira SCRUM-92's scope note. If a future sprint needs per-line cost variation (e.g. multi-style POs), that's a schema addition to `PO_LINE`, not a breaking change — flagged here rather than over-built now.
- **`STYL_TGT_LINE`'s cross-map validity check lives in the command handler, not the database**, because Postgres can't cleanly express "this (size, colour) pair must independently exist in two separate join tables" as a single constraint. This is the same class of validation as PO line vs. style size-run/colourways (spec AC-8) — both handled the same way, at the application layer, for consistency.
