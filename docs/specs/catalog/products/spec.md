# SCRUM-201: Products (create, edit, publish)

- **Status:** Approved (owner decisions 2026-10-14; open questions below use the stated defaults)
- **Jira:** SCRUM-201 (epic SCRUM-12); source ticket SCRUM-78
- **BRD sections:** §5.8, §5.9, §7.8, §12.3.2
- **Requirement IDs:** FR-53, FEAT-ADM-01, SEC-08 (declared), FR-59/FR-60 (enforced S8), NFR-17, NFR-19

## Problem & intent
Once a style has been produced, received and labelled, the catalogue team needs to turn it into something shoppers can buy: a product with a name, description, price, categories and collections, whose size × colour variants are the SKUs already created in Labelling. Staff must be able to do this without a developer, and nothing incomplete may go live.

## Requirements (quoted from the BRD)
> **FR-53:** Admin users shall create/edit products and variants (size × colour), pricing, media, size chart, and care info.

> **§5.8 Admin / Operations.** Catalogue: create product → define variants → set stock → publish.

> **§12.3.2** `Products`: PK Id; SKU (unique); Title; Description; CategoryId FK; IsLocallyMade bit; IsActive bit. `ProductVariants`: PK Id; FK ProductId; ColorId FK; SizeId FK; VariantSKU (unique); Price; CompareAtPrice; StockQuantity. "The actual sellable unit (size × colour) — every stock/cart/order line references this table, never Products directly."

## User flow
1. Staff choose a style and create a product from it (name, description, gender, age group, category, collections). Colourways and the size run come from the style, giving the planned size × colour variants. `(new)` One product per style.
2. The product's variants (size × colour) are listed. A variant gets its SKU and GTIN from Labelling (never minted here) and is sellable from that moment; the others read "not yet available". Staff set the price and optional compare-at ("was") price per variant. Price changes are kept as history.
3. Staff complete the other publish-gate data: images and alt text ([product-media](../product-media/spec.md)), size chart ([size-charts](../size-charts/spec.md)), composition and care ([composition-care](../composition-care/spec.md)), URL slug ([urls](../urls/spec.md)).
4. **Publish** validates completeness and, if it passes, makes the product live and emits an event. A failed publish lists every missing item, not just the first.
5. **Unpublish** takes it off sale without deleting anything. A product that has orders is archived, never hard-deleted.
6. Two editors saving the same product: the second gets a conflict message and must reload.

## Acceptance criteria
| ID | Given / When / Then | Source |
|---|---|---|
| PRD-1 | Given a style with 3 sizes × 2 colours, when staff create its product, then 6 planned variants are listed; each one that Labelling has issued a GTIN for shows its SKU and GTIN, the others show "not yet available" | SCRUM-78 AC-1, D1 |
| PRD-2 | Given a compare-at price that is not higher than the selling price, then validation fails | SCRUM-78 AC-2, D4 |
| PRD-3 | Given a published product, when staff unpublish it, then it is no longer sellable and an unpublished event is emitted, and it remains in order history | SCRUM-78 AC-3 |
| PRD-4 | Given a product missing any publish-gate item (price, age/gender, image, alt text, size chart, composition, care, slug), then publish is refused and every missing item is named | SCRUM-78 (detailed behaviour 4) |
| PRD-5 | Given two editors on one product, then the second save is refused with a conflict message | SCRUM-78 AC-6 |
| PRD-6 | Given a product with orders, then it can be archived but not deleted | SCRUM-78 (behaviour 5) `(order link arrives S12; archived state exists now)` |
| PRD-7 | Given a price change, then the old price, new price, who and when are recorded | SCRUM-78 (behaviour 2) |
| PRD-8 | Given any price, it is `numeric(12,2)` PKR, never negative, never zero when publishing | CLAUDE.md (money), `(new)` |
| PRD-9 | Given a non-catalogue role, then product endpoints return 403 | SCRUM-78 AC-5 `(declared now as a policy; enforced with authentication in S8)` |
| PRD-10 | Given a publish or unpublish, then a `ProductPublished` / `ProductUnpublished` event is written in the same transaction (outbox) | BRD §12.7 `(new)` |
| PRD-11 | Given the product list, then it is paged, filterable by status/category/search, and runs a constant number of queries | CONVENTIONS.md |
| PRD-12 | Given a category or collection placement, then the product appears under them (many collections, one category) | Roadmap S7 |
| PRD-13 | Given a product published while only some variants have a GTIN, then it publishes, the unavailable variants cannot be selected, and each becomes selectable when its GTIN arrives (an event, no manual step) | D1 |
| PRD-14 | Given a GTIN voided in Labelling, then that variant stops being sellable | D1, NFR-SC-03 `(new)` |
| PRD-15 | Given a Labelling event delivered twice or out of order, then the variant list is the same as if delivered once | ADR 0004 `(new)` |

## Non-functional constraints
SEC-08 role check server-side on every request (S8). Optimistic concurrency token on the product. Audit columns on every table. UPPERCASE abbreviated naming per `docs/db/naming.md`. No N+1.

## Out of scope
Fit Finder mapping (descoped), supplier cost screens, try-on assets, bulk price/stock import (SCRUM-78 stretch), the storefront (S9), stock (Warehouse owns it), search index.

## Decisions (owner, 2026-10-14)
- **D1 (was Q-PRD-1)** Catalog owns descriptive content and price and never mints SKUs or GTINs. It receives each variant (size, colour, SKU, GTIN) from Labelling through its contract and events (ADR 0002; `VariantGtinAssignedEvent`, `GtinVoidedEvent`). A product may be published with only some variants available: a variant becomes sellable as soon as Labelling has issued its GTIN; variants still waiting for QC or labelling are listed as "not yet available" and cannot be selected. They do not block the publish of the product.
- **D4 (was Q-PRD-2)** `CompareAtPrice` (optional, the "was" price) must be higher than `Price` (the selling price). The Jira ticket's inverted wording is an error.
- **D3** Age groups follow the business range 1-12Y; see the module README (Q-AGE-2 for the band names).

## Open questions
- [ ] **Q-PRD-7 Minimum to publish (new, follows from D1).** Proposed: at least one variant must be sellable (GTIN issued and not voided) to publish; a product with none is refused with "no variant is available yet". Confirm.
- [ ] **Q-PRD-8 Price before the GTIN exists.** Proposed: price is set per planned size × colour of the style before its GTIN arrives, so staff can finish the product while stock is still in QC. Every planned variant needs a price to publish (even those not yet available), or only the sellable ones? Proposed: only the sellable ones; the others may stay unpriced until they become sellable, and a variant becoming sellable without a price is shown as "not yet available" until priced.
- [ ] **Q-PRD-3 Publish needs stock?** Proposed: no. Stock only governs availability (FR-SC-08); a published product with nothing on the shelf shows sold out.
- [ ] **Q-PRD-4 Product code.** The BRD `Products.SKU` (unique) versus Labelling's per-variant SKU. Proposed: the style code is the product code; no separate product SKU.
- [ ] **Q-PRD-5 Collections.** Lookup rows (New Arrivals, Sale, Essentials…) with a product-to-collection link table. Proposed yes; the launch collection names are for the owner.
- [ ] **Q-PRD-6 Tags.** SCRUM-78 lists tags. Free-text tags conflict with the lookup rule. Proposed: out of scope; use collections.
- [ ] **Q-PRD-9 Where the selling price starts.** The style has a target retail price. Proposed: the product's price is pre-filled from it and can then be changed freely; later changes to the style's target do not touch published prices.
- [ ] **Q-AGE-2 Age band names** (module README).
