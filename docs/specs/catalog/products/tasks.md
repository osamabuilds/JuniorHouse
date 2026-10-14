# Products: tasks

- [x] **P1** `Product` aggregate (status machine Draft → Published → Unpublished → Archived, one per style) and `PlannedVariant.SetPrice` (price > 0, compare-at higher, two decimals). *Proof:* `ProductTests`, `PlannedVariantPriceTests` (PRD-2, PRD-6, PRD-8; money is `decimal`).
- [x] **P2** `SellableVariants` domain service (planned × labelled by colour and size; sellable = issued, not voided, priced). *Proof:* `SellableVariantsTests` (PRD-1, PRD-13, PRD-14).
- [x] **P3** `PublishGate` evaluator that returns every failure with code, message and tab. *Proof:* `PublishGateTests` (PRD-4: each missing item named, several at once, none when complete).
- [x] **P4** EF configuration and tables `PRDT_MAIN`, `PRDT_STS_LKP`, `PRDT_VRNT`, `PRDT_PRIC_HIST`, `VRNT_LBL`, `PRDT_COLN_MAP`; price-history immutability trigger. *Proof:* `ProductSchemaTests` (unique style, unique variant triple, check constraints, history refuses update/delete: PRD-7).
- [x] **P5** `CreateProduct` (style must exist and be active; planned variants from the style; backfill via `IVariantQueries`; price pre-filled from the target retail price). *Proof:* `CreateProductTests` (PRD-1: 3 sizes × 2 colours = 6 planned variants; a second product for a style is a 409 even in a race).
- [x] **P6** `UpdateProductContent`, `SetVariantPrices`, `SetCollections` with the concurrency token. *Proof:* `ProductEditTests` (PRD-2, PRD-5 second editor refused, PRD-7 history rows, PRD-12).
- [x] **P7** `LabellingVariantConsumer` for `VariantGtinAssignedEvent` and `GtinVoidedEvent` (inbox, idempotent upsert into `VRNT_LBL`, `ProductChangedEvent` when published). *Proof:* `LabellingVariantConsumerTests` (PRD-13..15: delivered twice, out of order, void then reissue).
- [x] **P8** `RefreshVariantsFromLabelling`. *Proof:* `RefreshVariantsTests` (same result as the events; idempotent).
- [x] **P9** `PublishProduct`, `UnpublishProduct`, `ArchiveProduct` with events in the same transaction. *Proof:* `PublishProductTests` (PRD-3, PRD-4, PRD-10, PRD-13: publishes with one sellable variant and others not yet available; refuses with none).
- [x] **P10** `ListProducts` (paged, filters, sellable count) and `GetProduct` (variants, gate preview). *Proof:* `ProductQueryTests` (PRD-11: constant number of queries, page size clamped).
- [x] **P11** Endpoints behind `Policies.Catalogue`; strict rate limit on publish. *Proof:* `ProductEndpointsTests` over HTTP (PRD-1..5, PRD-9 policy declared), `EndpointSecurityTests`, `CataloguePolicyTests`.
- [x] **P12** Wire the publish gate to the media, size-chart, composition-care and URL gates. *Proof:* `PublishProductAllGatesTests` (a fully prepared product publishes; removing each item blocks publishing).
- [x] **P13** Web `catalog/products`: NgRx store, list container (filters, paging), editor container with tabs, variants table ("Not yet available"), publish tab with failures as links. *Proof:* component and reducer specs (labels, `th scope`, one `h1`, failures listed, conflict message shown).
- [x] **P14** e2e: create a product from a partly labelled style, see the gate fail, fix it, publish, unpublish. *Proof:* `products.e2e.spec.ts`; screens added to `responsive-layout.e2e.spec.ts`.
- [x] **P15** Card → Implemented; traceability rows PRD-1..15, FR-53, TC-PDP none. *Proof:* `StructureSyncTests`.

Built as planned unless noted in the spec's "As built" section. The test classes named in the Proof column were refined during the build; the tests that actually prove each criterion are listed in [../traceability.md](../traceability.md).
