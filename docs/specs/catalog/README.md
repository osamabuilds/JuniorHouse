# Catalog

Styles (Sprint 1) and, from Sprint 7 (SCRUM-201), **products**: the storefront-facing record a style becomes when it is published (BRD §5.8, §5.9, §7.8, §14.1).

**Plan:** [plan.md](plan.md) · **Tasks:** [tasks.md](tasks.md) · **Schema:** `CTLG` · **Code:** `src/api/src/Modules/Catalog/` · **Web:** `features/catalog/` · **Jira:** SCRUM-201 under epic SCRUM-12 (sources SCRUM-78, SCRUM-28, SCRUM-29, SCRUM-149, SCRUM-154) · **Rules:** [constitution](../../CONSTITUTION.md)

## Features

| Feature | What it does | Status |
|---|---|---|
| [styles](styles/README.md) | Pre-production style record, colourways, size run, targets | Implemented |
| [products](products/spec.md) | A product (from a style) with storefront content, price, categories and collections; draft, publish, unpublish, archive | Approved, in progress (Sprint 7) |
| [product-media](product-media/spec.md) | Images with ordering and mandatory alt text at publish | Approved, in progress (Sprint 7) |
| [size-charts](size-charts/spec.md) | Size charts per category: size → age range and measurements | Approved, in progress (Sprint 7) |
| [composition-care](composition-care/spec.md) | Fabric composition (fibre + %) and wash-care instructions | Approved, in progress (Sprint 7) |
| [urls](urls/spec.md) | The URL convention, slug generation, slug history and 301 resolution | Approved, in progress (Sprint 7) |

## Sprint 7 scope and boundaries

**In:** everything needed so that, at the end of the sprint, staff can take an accepted style, build its storefront page content, and publish it, with the data the storefront (S9) will render. Publishing emits an event; the storefront read model and pages arrive in S9.

**Out (later sprints):** the storefront itself, search, sitemap generation and SEO tags (S9, SEO-03..06), cart/stock checks (S11), the Merchant Center feed (S18), bulk import (stretch in SCRUM-78, not planned), Fit Finder (descoped), staff roles and audit enforcement (S8: the policy is declared now, enforced then), image derivatives for the CDN beyond what S9 needs.

**Order of work:** urls (convention, slug rules) → products → size-charts and composition-care (both are publish-gate data) → product-media → publish gate and event, then the admin screens for each.

**Module decisions** are recorded here or in the ADRs under [`docs/adr/`](../../adr/). Open questions that block more than one feature are listed below; per-feature ones are in each spec.

## Decisions (owner, 2026-10-14)

| ID | Decision |
|---|---|
| D1 | Catalog owns descriptive content and price and never mints SKUs or GTINs. It receives each variant (size, colour, SKU, GTIN) from Labelling through its contract and events ([ADR 0014](../../adr/0014-product-variants-from-labelling.md)). A product may be published with only some variants available: a variant becomes sellable as soon as Labelling has issued its GTIN; variants still waiting for QC or labelling are listed as "not yet available". |
| D2 | Age is a filter attribute, never a URL path segment, because the age bands are not settled. The product path is built from gender and category ([ADR 0013](../../adr/0013-product-url-convention.md); see Q-URL-1). |
| D3 | The business range 1-12Y is authoritative; the BRD's 0-2 / 3-8 / 9-14 brackets were illustrative. Proposed navigation bands: Toddler (1-3Y), Kids (4-8Y), Junior (9-12Y), for the owner to confirm or rename (Q-AGE-2). |
| D4 | `CompareAtPrice` (optional "was" price) must be higher than the selling `Price`. The Jira ticket's inverted wording is an error. |
| D5 | Store the original image and its dimensions only; WebP/AVIF derivatives are decided in Sprint 9. |
| D6 | Care symbols use the standard ISO 3758 icon set, not custom artwork; the care text per garment comes from the owner or the vendor. |

**Checked against the data while planning:** the reference data has six flat categories (Tops, Bottoms, Dresses, Outerwear, Sleepwear, Accessories) and gender is a separate attribute (Boys, Girls, Unisex), so the category list is *not* a tree under Boys and Girls. The path therefore uses both attributes (Q-URL-1). The seeded age brackets are Newborn (0-3M), Infant (3-12M), Toddler (1-3Y), Kids (4-8Y) and Tween (9-12Y); the seeded sizes stop at 7-8Y.

## Open questions (defaults adopted unless you say otherwise)

Shared:
- [ ] **Q-AGE-2 Age band names.** Existing rows: Newborn, Infant, Toddler (1-3Y), Kids (4-8Y), Tween (9-12Y). Proposed: deactivate Newborn and Infant (outside 1-12Y) and rename Tween to Junior, after the owner confirms the names.
- [ ] **Q-SKU-1 SKU format.** Stays as Sprint 5 made it (`ROMP-<style>-<colour>-<size>`); Catalog only displays it.

Per feature (full wording in each spec):
- **products:** Q-PRD-7 at least one sellable variant to publish (proposed yes); Q-PRD-8 which variants need a price to publish (proposed only the sellable ones); Q-PRD-3 publish does not need stock; Q-PRD-4 the style code is the product code; Q-PRD-5 collections are lookup rows, launch names from the owner; Q-PRD-6 no free-text tags; Q-PRD-9 price pre-filled from the style's target retail price.
- **product-media:** Q-MED-2 limits (5 MB, 10 images); Q-MED-3 alt text in English only; Q-MED-4 local file storage until the S8 staging server; Q-MED-5 an image belongs to the product or to one colour.
- **size-charts:** Q-CHT-1 sizes beyond 7-8Y need new size rows and labels; Q-CHT-2 per category (and gender), no per-style override; Q-CHT-3 cm only; Q-CHT-4 which measurement columns; Q-CHT-5 a chart per category and gender, Unisex as the fallback.
- **composition-care:** Q-CMP-1 composition per product, not per colour; Q-CMP-2 where the ISO 3758 artwork comes from and its licence; Q-CMP-3 the launch fibre list; Q-CMP-4 which care instructions to offer.
- **urls:** Q-URL-1 confirm `/{gender}/{category}/{slug}`; Q-URL-5 the segment for Unisex products; Q-URL-2 collections not in the product URL; Q-URL-3 category slugs are stable codes; Q-URL-4 slug from the name only; Q-URL-6 slugs unique within gender + category.

Back to the [spec index](../README.md).
