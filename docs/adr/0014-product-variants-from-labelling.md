# 0014 — Products take their variants from Labelling

- Status: Accepted (Sprint 7, SCRUM-201; plan approved by the owner 2026-10-14)
- Date: 2026-10-14

## Context

The BRD data model shows `Products` and `ProductVariants` with a unique `VariantSKU`. Sprint 5 already created the sellable variant, its SKU code and its GTIN-13 in the Labelling module (`SKU.VRNT_MAIN`, `SKU.VRNT_GTIN`). The owner decided (2026-10-14) that Catalog owns descriptive content and price and never mints SKUs or GTINs, receives the variant list from Labelling through its contract and events, and may publish a product while only some variants are available. Modules never read each other's tables (ADR 0002).

## Decision

- **Planned versus issued.** A product's *planned* variants are the style's colourways × size run (Catalog's own data). Labelling's facts (variant id, SKU, GTIN, GTIN status) arrive separately and are kept in a Catalog table `CTLG.VRNT_LBL`, one row per Labelling variant, written only by the Catalog consumers of `VariantGtinAssignedEvent` and `GtinVoidedEvent` (upsert by Labelling variant id: idempotent and order-independent; ADR 0004 inbox). A planned variant is **sellable** when its colour × size has an issued, not voided GTIN in `VRNT_LBL` and it has a price. Sellability is derived at read time, never stored, so there is no flag to drift.
- **Backfill.** Events sent before Catalog existed as a consumer are lost, so Labelling adds a read contract `IVariantQueries.GetByStyleAsync(styleId)` (one query, no N+1). Catalog calls it when a product is created and from an explicit "refresh from Labelling" action; both write through the same upsert.
- **Dependency direction.** `Catalog.Infrastructure` references `Labelling.Contracts` only (events, `IVariantQueries`). Labelling already references `Catalog.Contracts`; the two Contracts projects do not reference each other, so there is no cycle. The architecture tests allow exactly this pair.
- **Publishing.** A product publishes when at least one variant is sellable (Q-PRD-7). A variant that becomes sellable later (a new GTIN event) is selectable on the storefront without a manual step; a voided GTIN makes it not sellable again. Catalog emits `ProductChangedEvent` in each case so the storefront read model (S9) refreshes.

## Consequences

- A second copy of SKU and GTIN exists in `CTLG.VRNT_LBL`. It is a read-side copy owned by the consumer and rebuildable from `IVariantQueries`, which is the only way to keep modules from sharing tables.
- Stock availability (Warehouse) stays separate: sellable means "has an identity and a price"; in stock is a different question answered in S9 and S11.
- Catalog will not accept SKU or GTIN input from the admin UI or API.
