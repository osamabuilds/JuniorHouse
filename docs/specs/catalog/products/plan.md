# SCRUM-201: Products, technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md) (data, publish gate, settings, shared security review)
- **Module(s):** Catalog (reads Labelling and Reference through contracts)

## Domain model
- **`Product`** (aggregate): `StyleId`, name, description, gender, category, age bracket, status (`Draft`, `Published`, `Unpublished`, `Archived`), slug, care note, planned variants, collections. Rules: one product per style; `Publish()` takes the gate result (it does not evaluate other features' data itself); `Unpublish()` only from Published; `Archive()` from any non-archived state, final; a published product's gender/category/slug may change only through `ChangePath` (which records history, see [urls](../urls/plan.md)).
- **`PlannedVariant`** (entity): colour, size, `Price`, `CompareAtPrice`. `SetPrice(price, compareAt)` enforces `price > 0`, `compareAt` null or `> price`, two decimals; every change returns a price-history entry.
- **`LabelledVariant`** (record owned by the consumer, table `VRNT_LBL`): Labelling variant id, style, colour, size, SKU, GTIN, voided.
- **`SellableVariants`** (domain service, pure): joins planned variants with labelled variants by (colour, size); sellable = issued GTIN, not voided, priced.
- **`PublishGateResult`**: list of `GateFailure(code, message, tab)`.

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `CreateProduct` | style id, name, description, gender, category, age bracket | product; backfills `VRNT_LBL` via `IVariantQueries`; prices pre-filled from the style's target retail price (Q-PRD-9) | PRD-1, D1 |
| `UpdateProductContent` | id, fields, concurrency token | product | PRD-5 |
| `SetVariantPrices` | id, list of (colour, size, price, compare-at), token | variants + history rows | PRD-2, PRD-7, PRD-8, D4 |
| `SetCollections` | id, collection ids | collections | PRD-12 |
| `PublishProduct` | id, token, acting person | published product or the full failure list | PRD-4, PRD-10, PRD-13 |
| `UnpublishProduct` / `ArchiveProduct` | id, token, person | product | PRD-3, PRD-6, PRD-10 |
| `RefreshVariantsFromLabelling` | id | variants (idempotent) | PRD-15 |
| `ListProducts` | page, status, category, search | paged summaries incl. sellable count (one query) | PRD-11 |
| `GetProduct` | id | product with variants (planned + SKU/GTIN + sellable), gate preview | PRD-1 |
| `LabellingVariantConsumer` (`VariantGtinAssignedEvent`, `GtinVoidedEvent`) | event | upsert `VRNT_LBL`; then `ProductChangedEvent` if the product is published | PRD-13..15 |

## API endpoints
| Method | Route | Auth / role | Ownership check |
|---|---|---|---|
| GET | `/api/catalog/products` | `Catalogue` | n/a (staff data, not customer-owned) |
| GET | `/api/catalog/products/{id}` | `Catalogue` | n/a |
| POST | `/api/catalog/products` | `Catalogue` | style must exist and be active |
| PUT | `/api/catalog/products/{id}` | `Catalogue` | token required (If-Match / body) |
| PUT | `/api/catalog/products/{id}/prices` | `Catalogue` | token |
| PUT | `/api/catalog/products/{id}/collections` | `Catalogue` | token |
| POST | `/api/catalog/products/{id}/publish`, `/unpublish`, `/archive` | `Catalogue` (strict rate limit) | token |
| POST | `/api/catalog/products/{id}/refresh-variants` | `Catalogue` | n/a |

## Events
| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|
| `ProductPublishedEvent`, `ProductUnpublishedEvent`, `ProductArchivedEvent`, `ProductChangedEvent` | Products | storefront (S9) | yes, same transaction |
| `VariantGtinAssignedEvent`, `GtinVoidedEvent` | Labelling (exists) | Products consumer | yes, inbox on this side |

## Data
`PRDT_MAIN`, `PRDT_STS_LKP`, `PRDT_VRNT`, `PRDT_PRIC_HIST`, `VRNT_LBL`, `PRDT_COLN_MAP` (see module plan). Indexes on every FK, on (`PRDT_STS_ID`, `CATG_ID`) for the list, and on `VRNT_LBL` (`STYL_ID`,`CLR_ID`,`SIZE_ID`) for the join. Query counts asserted: list and detail run a constant number of queries.

## NFR / security design
Money as `decimal` / `numeric(12,2)` with check constraints (PRD-8). Role checks via `Policies.Catalogue` (enforced S8, PRD-9). Optimistic concurrency (PRD-5). Paging ≤ 100 (PRD-11). Inbox + upsert for events (PRD-15). No prices accepted from clients on any other module: the storefront will read prices from here, and cart totals are recomputed server-side in S11 (SEC-09).

## Frontend
`features/catalog/products`: list container (filters, paging), editor container with tabs (Content, Variants and prices, Images, Size chart, Composition and care, URL, Publish). Variants table shows SKU/GTIN or "Not yet available". Publish tab lists failed gates as links. Forms re-validate as the user types. Stores per CONVENTIONS.md.

## Risks & alternatives considered
- Storing sellability as a flag was rejected: it would drift when Labelling voids or re-issues (ADR 0014).
- Letting Catalog mint SKUs was rejected by the owner (D1).
- If Labelling later re-issues a GTIN to a voided SKU (Q-PUT-2), the new event simply makes the variant sellable again.

## Security & coupling review
Shared rows: see [module plan](../plan.md#security--coupling-review-sprint-level). Feature-specific:

| Area | How this feature meets it |
|---|---|
| Authorization | `Policies.Catalogue` on the group; `EndpointSecurityTests` and a policy test cover every route |
| Input validation | length limits, price range, compare-at rule, ids checked through Reference/Labelling contracts; unknown ids → 400 |
| Concurrency | token on product; unique (`STYL_ID`) so a second product for a style is a 409 even under a race |
| Idempotency | events upserted by Labelling variant id; publishing an already published product returns the current state |
| Audit | price history trigger-protected; publish/unpublish/archive record person and time |
| Abuse cases | replayed or out-of-order Labelling events; two staff publishing at once; a negative or inverted price; publishing a product whose only variant was just voided |
