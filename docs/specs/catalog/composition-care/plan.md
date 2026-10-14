# SCRUM-201: Composition and care, technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md)
- **Module(s):** Catalog (fibres and care instructions via the Reference contract)

## Domain model
- **`Composition`** (value object over the product's rows): rows of (fibre, percentage with one decimal). Rules: no duplicate fibre; each percentage 0 < p ≤ 100; the sum is compared in integer tenths, so 33.3 + 33.3 + 33.4 = 100.0 exactly (CMP-7). A composition may be saved incomplete (draft) but is *complete* only at exactly 100.0.
- **`CareSet`**: the set of chosen care instruction ids plus an optional free-text note (≤ 500 characters, plain text).
- **Gate (`ICompositionGate`)**: fails with `CompositionMissing`, `CompositionTotal(actual)`, or `CareMissing`.

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `SetComposition` | product id, rows, token | rows and running total | CMP-1, CMP-6, CMP-7 |
| `SetCare` | product id, care ids, note, token | care set | CMP-2, CMP-4 |
| `GetCompositionAndCare` | product id | rows, total, care items (code, name, symbol key), note | CMP-3 |
| `ListCareInstructions` / `ListFibres` | none | active lookup rows (from Reference, cached) | CMP-8 |
| gate | product id | failures | CMP-1, CMP-2 |

Both setters raise `ProductChangedEvent` when the product is published (CMP-5).

## API endpoints
| Method | Route | Auth / role | Ownership check |
|---|---|---|---|
| GET | `/api/catalog/products/{id}/composition-care` | `Catalogue` | n/a |
| PUT | `/api/catalog/products/{id}/composition` | `Catalogue` | token |
| PUT | `/api/catalog/products/{id}/care` | `Catalogue` | token |
| GET | `/api/catalog/lookups/fibres`, `/care-instructions` | `Catalogue` | n/a (also served by Reference lookup endpoints) |

## Events
`ProductChangedEvent` only.

## Data
`PRDT_CMPS`, `PRDT_CARE_MAP`, `PRDT_MAIN.CARE_NOTE`; Reference `FIBR_LKP`, `CARE_LKP` (with `SYMB_CODE`, the ISO 3758 symbol key; the icon files themselves are bundled with the storefront in S9 once the licence question Q-CMP-2 is settled). Check constraint 0 < `PCT` ≤ 100; unique (product, fibre) and (product, care).

## NFR / security design
Care and fibre lists are cached read-only lookups; saving replaces a product's rows in one transaction (delete + insert) with the token check. Care symbols always come with a text name, so the admin and the storefront never rely on the icon alone (CMP-4).

## Frontend
Composition and care tab: a table of rows (fibre select, percent input, remove button) with an "add fibre" button and a live total announced politely (`aria-live`) and shown as "Total 100%" / "Total 95%: 5% missing"; care instructions as a checkbox group with name and (later) symbol; a note textarea. Every control labelled.

## Risks & alternatives considered
Composition per colour (Q-CMP-1) is deferred; per-product rows can be keyed by colour later without breaking the API. The style's single `FabricId` stays as the main fabric.

## Security & coupling review
Shared rows: see the [module plan](../plan.md#security--coupling-review-sprint-level). Feature-specific:

| Area | How this feature meets it |
|---|---|
| Authorization | `Policies.Catalogue` |
| Input validation | percentage as `decimal` with one decimal place; fibre/care ids checked through the Reference contract; note length and plain text |
| Concurrency | product token; replace-all inside one transaction |
| Abuse cases | percentages like 100.01 or -5; the same fibre twice; thousands of rows (capped at the number of fibres); a floating-point total of 99.9999 (integer tenths) |
