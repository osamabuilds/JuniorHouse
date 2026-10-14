# SCRUM-201: Size charts, technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md)
- **Module(s):** Catalog (sizes, genders and categories via the Reference contract)

## Domain model
- **`SizeChart`** (aggregate): category, gender (Unisex = fallback for any gender without its own chart), name, lines. Unique per (category, gender).
- **`SizeChartLine`**: size, age range in months, height range cm, weight range kg, optional chest/waist/length cm. Rules: each minimum ≤ its maximum; no negative values; one line per size; a line must have at least an age range or one measurement (FR-08: "age range and/or measurements").
- **Lookup rule (domain service):** the chart for a product is the chart for (category, gender), else (category, Unisex), else none.
- **Gate (`IChartGate`)**: never fails; reports `NoChart` so the product says so (CHT-3).

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `CreateSizeChart` / `UpdateSizeChart` | category, gender, lines, token | chart; `SizeChartChangedEvent` | CHT-2, CHT-4, CHT-5 |
| `DeleteSizeChart` | id | none (allowed; products then report no chart) | `(new)` |
| `ListSizeCharts` | page | paged | CHT-8 |
| `GetSizeChart` | id | chart with lines | CHT-7 |
| `GetChartForProduct` | product id | rows for the product's size run, in size order | CHT-1, CHT-6 |

## API endpoints
| Method | Route | Auth / role | Ownership check |
|---|---|---|---|
| GET | `/api/catalog/size-charts` | `Catalogue` | n/a |
| GET | `/api/catalog/size-charts/{id}` | `Catalogue` | n/a |
| POST / PUT / DELETE | `/api/catalog/size-charts[/{id}]` | `Catalogue` | token on PUT |
| GET | `/api/catalog/products/{id}/size-chart` | `Catalogue` | n/a |

## Events
`SizeChartChangedEvent(categoryId, genderId)` via outbox; the storefront (S9) refreshes affected product pages.

## Data
`CHRT_MAIN`, `CHRT_LINE` (module plan); lines cascade-delete with their chart; check constraints for min ≤ max and non-negative values; index on (`CATG_ID`, `GNDR_ID`) and the FK.

## NFR / security design
Constant number of queries for the product chart (one join of chart and lines, sizes ordered by `SORT_SEQ`). Server-rendered table later (S9) uses `th scope`; the admin editor is a real table with a label on every input.

## Frontend
`features/catalog/size-charts`: list (paged), editor with a row per size (inputs labelled by size and column, units shown in headers), validation messages inline. In the product editor, the Size chart tab shows the chart that applies (read-only) with a link to edit it, or "No size chart for this category yet".

## Risks & alternatives considered
A per-style override (Q-CHT-2) and inches (Q-CHT-3) are deferred; adding them later is additive (an optional `STYL_ID` column, a display setting).

## Security & coupling review
Shared rows: see the [module plan](../plan.md#security--coupling-review-sprint-level). Feature-specific:

| Area | How this feature meets it |
|---|---|
| Authorization | `Policies.Catalogue` on the group |
| Input validation | numeric ranges and the min ≤ max rules in the domain and as check constraints; size ids checked through Reference |
| Concurrency | token on the chart; unique (category, gender) |
| Abuse cases | absurd values (height 9,999 cm); two lines for one size; deleting a chart that products rely on (they report "no chart", nothing breaks); a very large number of lines (capped at the number of size rows) |
