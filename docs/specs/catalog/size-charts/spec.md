# SCRUM-201: Size charts

- **Status:** Implemented (Sprint 7, SCRUM-201)
- **Jira:** SCRUM-201 (epic SCRUM-12); source tickets SCRUM-28, SCRUM-78
- **BRD sections:** §7.2 (FEAT-PDP-02), §12.3.2
- **Requirement IDs:** FR-08, FEAT-PDP-02, TC-PDP-01, NFR-17, NFR-19, SEO-01

## Problem & intent
With the Fit Finder descoped, the size chart is the main help a parent has to choose a size and the main defence against wrong-size returns. Staff need to maintain charts as data; the product page (S9) will show them.

## Requirements (quoted from the BRD)
> **FR-08:** Each product shall expose a size chart mapping size labels to age range and/or measurements.

> **TC-PDP-01 View size chart.** Open Size Chart on PDP → Age/height/measurement table displayed correctly for that category.

> **§12.3.2** `SizeCharts`: PK Id; CategoryId FK; SizeId FK; AgeMinMonths; AgeMaxMonths; HeightMinCm; HeightMaxCm; WeightMinKg; WeightMaxKg. "The size-chart data shown on PDP (FR-08)".

## User flow
1. Staff define a size chart for a category: one row per size with its age range and measurements (height, chest, waist, length as applicable), in cm.
2. A product uses its category's chart (see Q-CHT-2 on per-style overrides).
3. Staff edit a value: every product using the chart shows the new value (the storefront refreshes on the chart-changed event).
4. A product whose category has no chart can still be published; the storefront says "size chart coming soon" and Add to Cart still works.

## Acceptance criteria
| ID | Given / When / Then | Source |
|---|---|---|
| CHT-1 | Given a category's chart, when it is requested for a product, then each size of the product's size run shows its age range and measurements | TC-PDP-01, SCRUM-28 AC-1 |
| CHT-2 | Given an edit to a chart value, then the chart-changed event is written in the same transaction and every product of the category returns the new value | SCRUM-28 AC-3 |
| CHT-3 | Given a category without a chart, then publish is allowed and the product reports "no size chart" | SCRUM-28 AC-4 |
| CHT-4 | Given a row whose minimum is above its maximum, or a negative value, then it is refused | `(new)` |
| CHT-5 | Given two rows for the same size in one chart, then the second is refused | `(new)` |
| CHT-6 | Given the chart data, then it is returned as structured rows (size label, age range, measurements, unit) that the storefront renders as a real table with header cells; never an image | SCRUM-28 AC-5 |
| CHT-7 | Given the admin chart screen, then it is a real table with labelled inputs, keyboard-operable | NFR-19 |
| CHT-8 | Given the list of charts, then it is paged and runs a constant number of queries | CONVENTIONS.md |

## Non-functional constraints
NFR-17 (mobile-first), NFR-19, SEO-01 (content in the server-rendered HTML; S9). Charts are data in lookup-style tables, never hard-coded.

## Out of scope
Automatic size recommendation, body-measurement capture of children (descoped), highlighting the selected size (storefront, S9), inches toggle (see Q-CHT-3).

## Decisions (owner, 2026-10-14)
- **D3** The range is the business range, 1-12Y. The chart carries whatever sizes the product's size run has; no range is hard-coded.

## Open questions
- [ ] **Q-CHT-1 Sizes beyond 7-8Y.** The seeded sizes stop at 7-8Y (`SIZE_LKP`), so a 1-12Y range needs more size rows (for example 8-9Y … 11-12Y). Which labels and which pairs does the business use? Adding them is a lookup seed, not a schema change.
- [ ] **Q-CHT-2 Per category or per style.** SCRUM-28 Q2. Proposed: per category for MVP; a per-style override only if the owner needs it.
- [ ] **Q-CHT-3 Inches as well as cm?** Proposed: cm only at MVP.
- [ ] **Q-CHT-4 Which measurements.** The BRD table has age, height and weight; SCRUM-28 adds chest, waist, length. Proposed: age (months), height, weight (as BRD) plus chest, waist and length (optional). Confirm.
- [ ] **Q-CHT-5 Category granularity.** Categories are currently six flat ones (Tops, Bottoms, Dresses, Outerwear, Sleepwear, Accessories). Boys and girls often fit differently. Proposed: a chart per category and gender (one chart for Unisex products as the fallback). Confirm.

## As built (Sprint 7)
- **Sizes beyond 7-8Y** (8-9Y to 11-12Y) are seeded by migration `AddCatalogLookups` with the default labels (Q-CHT-1).
- **Missing chart** never blocks publishing: the publish tab shows the `NO_SIZE_CHART` warning and the shop will say "size chart coming soon" (CHT-3).
- **Open questions** Q-CHT-2..5 were built with the defaults above.
