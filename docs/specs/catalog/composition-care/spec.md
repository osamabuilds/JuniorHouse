# SCRUM-201: Composition and care

- **Status:** Implemented (Sprint 7, SCRUM-201)
- **Jira:** SCRUM-201 (epic SCRUM-12); source tickets SCRUM-29, SCRUM-78
- **BRD sections:** §7.2 (FEAT-PDP-03)
- **Requirement IDs:** FR-09, FEAT-PDP-03, TC-PDP-18..21, NFR-19, SEO-01, SEO-09

## Problem & intent
Parents of young children check what a garment is made of and how to wash it before buying. Staff must record this as structured data, and a product without it must not be published.

## Requirements (quoted from the BRD)
> **FR-09:** Each product shall display composition and wash-care information.

> **FR-53:** Admin users shall create/edit products and variants (size × colour), pricing, media, size chart, and care info.

## User flow
1. Staff enter the composition as rows of fibre and percentage (for example cotton 95%, elastane 5%). The rows must total 100%.
2. Staff pick the care instructions from a list (each with a symbol and a text label) and may add a free-text note.
3. Publishing is blocked if composition or care is missing.
4. The storefront (S9) shows both without JavaScript and exposes the material in the product's structured data.

## Acceptance criteria
| ID | Given / When / Then | Source |
|---|---|---|
| CMP-1 | Given composition rows that do not total 100%, then they cannot be saved as complete and publish is refused | SCRUM-29 AC-2 |
| CMP-2 | Given a product without composition, or without any care instruction, then publish is refused naming the missing item | SCRUM-29 (behaviour 3) |
| CMP-3 | Given a published product, then its composition and care are returned in the product's published data | SCRUM-29 AC-1 |
| CMP-4 | Given a care instruction, then it has a text label as well as its symbol | SCRUM-29 AC-3 |
| CMP-5 | Given a change to a product's care data, then the product-changed event is written and the next read returns the new data | SCRUM-29 AC-4 |
| CMP-6 | Given the same fibre listed twice, a percentage of zero or below, or above 100, then it is refused | `(new)` |
| CMP-7 | Given a percentage, then it is stored with at most one decimal place and totals are compared exactly (no floating point) | `(new)` |
| CMP-8 | Given fibres and care instructions, then both are lookup rows, added without a schema change | CLAUDE.md database rules |
| CMP-9 | Given the admin form, then each control has a label and totals are announced to assistive technology | NFR-19 |

## Non-functional constraints
NFR-19; SEO-01 (server-rendered, S9); SEO-09 (Merchant Center attributes, S18). Fibres and care are lookups seeded by migration.

## Out of scope
Certifications and test reports, the provenance badge (FEAT-PDP-08), reviews, the structured-data output itself (S9).

## Decisions (owner, 2026-10-14)
- **D6 (was Q-CMP-2)** Care instructions use the standard ISO 3758 care-symbol icon set, not custom artwork. The care text that applies to each garment still has to come from the owner or the vendor, per garment composition.

## Open questions
- [ ] **Q-CMP-1 Per style or per colour.** Printed versus plain fabric can differ by colour (SCRUM-29 Q1). Proposed: per product (style) at MVP. The Style has a single fabric field; proposed to keep it as the main fabric and add the composition rows beside it.
- [ ] **Q-CMP-2 Icon artwork and licence.** Where the ISO 3758 symbol artwork comes from, and whether its licence allows use on our site, has to be checked before the icons are bundled. Until then the instruction list works with text labels (every symbol needs one anyway).
- [ ] **Q-CMP-3 Fibre list.** Cotton, polyester, viscose, elastane, wool, linen, nylon… The Style's fabric lookup has Cotton, Denim, Fleece, Jersey, Linen, Polyester; those are fabrics, not fibres. Confirm the launch fibre list.
- [ ] **Q-CMP-4 Care instruction list.** The standard set is about a dozen (wash temperature, bleach, tumble dry, iron, dry clean). Confirm which ones the business wants available.

## As built (Sprint 7)
- **A composition that does not total 100% can be saved** while a product is a draft. The Composition tab shows how much is missing, and the publish gate refuses the product with the actual total (CMP-1). Shares are compared in integer tenths, so 33.3 + 33.3 + 33.4 totals exactly 100 (CMP-7).
- **Reference lists.** Fibres and care instructions come from `FIBR_LKP` and `CARE_LKP` (migration `AddCatalogLookups`), so a new fibre needs no code change (CMP-8). Q-CMP-2..4 stay open for the owner.
