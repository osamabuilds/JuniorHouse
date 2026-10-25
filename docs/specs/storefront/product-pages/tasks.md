# Tasks: product-pages (Sprint 9b)

Jira: SCRUM-27 under umbrella SCRUM-203 · Plan: [plan.md](plan.md) · Spec: [spec.md](spec.md). Depends on the S9a read model (published-catalog) and Catalog's `IProductPathQueries`. Defaults followed: Q-PDP-1 (availability only, no purchase controls), Q-PDP-2 (`?colour=` hint, canonical is the product), Q-PDP-3 (care symbols as text), Q-PDP-4 (cm only). Tests carry `[Trait("Spec", "PDP-n")]`.

Meta and JSON-LD for the page come from `seo`; until that feature lands, task 13 sets a plain title and description through the existing `shared/seo` service and the structured data waits for the seo tasks (PDP-14 is closed there).

## API

1. [x] Domain `Storefront.Domain/ProductPages`: path normalisation (lower case, trailing slash, 200-char cap), default colour (first with an in-stock size), unknown colour or size ignored, displayed price rule ("From" lowest available before a choice, the variant's price after; compare-at only when above price), image choice (colour images override product-level), size availability class. Proof: domain unit tests incl. hostile paths (PDP-2, 3, 9, 15).
2. [x] Response models in `Storefront.Application/ProductPages` (`ProductPageModel`, the outcome envelope) implementing `IPublicStorefrontResponse`; no quantity, no read-model entity. Proof: `PublicResponseTests` picks them up and passes (PDP-17, PCT-3).
3. [x] `GetProductPageQuery`: normalise, resolve through `IProductPathQueries`, then load the page by product id in a constant number of queries (product, variants, images + ready derivatives, composition + care, chart lines). Outcomes `Found`, `Moved`, `Gone`, `NotFound`; `Current` with no projected row answers `NotFound`, uncached, and bumps a counter. Proof: Testcontainers tests for each outcome, old slug in one hop, unpublished and archived (PDP-1, 13).
4. [x] Variants in the model: sold-out sizes, sizes with no GTIN yet as "not yet available", per-colour images and price. Proof: tests with a partly sold-out product, a variant without SKU, a colour-specific image (PDP-2, 4, 5).
5. [x] Size chart lines for (category, gender) with the Unisex fallback; none gives an empty chart and a flag. Proof: tests for exact, fallback and absent (PDP-6, 7).
6. [x] Composition and care lines in order. Proof: test (PDP-8).
7. [x] Product with no in-stock variant: found (200 contract), `IsAvailable = false`, no purchase state. Proof: test; availability word list never says reserved (PDP-16, 17).
8. [x] Endpoint `GET /api/storefront/products/by-path?path=&colour=&size=` with the `Storefront.Public` policy; the four outcomes in one JSON shape with the target path from Catalog only; 400 on a missing or over-long path. Proof: HTTP integration tests (CI): traversal and encoded paths, 201-char path, markup, no stack trace, no exact quantity in the JSON (PDP-13, 17).
9. [x] Query-count and speed test: a product with 8 variants and 10 images costs the same number of queries as one with 1, under 200 ms. Proof: integration test; closes published-catalog task 23 (PCT-8).

## Web (`features/storefront/product-pages`, CONVENTIONS.md §1)

10. [x] Skeleton: models, `product-pages-api.service`, NgRx store (`selectedColour`, `selectedSize`, outcome), providers, barrel. Proof: store and service specs.
11. [x] Route: replace the mock `productMatcher` and the mock container with the three-segment route and a resolver; `Moved` sends 301, `Gone` 410, `NotFound` 404 through `RESPONSE_INIT`; `Location` comes from the API. Proof: resolver and route specs, SSR status test (PDP-13).
12. [x] Components: `gallery`, `colour-swatches`, `size-selector`, `price`, `availability-note`, `size-chart`, `composition-care`, `sold-out-state`. Colour and size work as links without JavaScript and update in place with it. Proof: component specs and axe (PDP-4, 6, 9, 11, 16).
13. [x] Container: breadcrumb, one `<h1>`, headings that never skip a level, main image `fetchpriority="high"` with explicit width and height, preload `Link` header for the LCP image, plain title and description. Proof: container spec; SSR HTML test that the content is in the server response (PDP-1, 10).
14. [x] Retire `features/catalog/product` and the product part of `shared/data/mock-catalog`; keep the mock only where a spec still uses it. Proof: existing specs updated and passing.
15. [ ] Playwright e2e at seven viewports: pick colour, pick size, sold-out size not selectable, size chart, composition and care, keyboard path, 404/410/301, LCP budget on the Docker stack. Proof: `product-pages.e2e.spec.ts` (PDP-1..13, 16).

## Close

16. [ ] Traceability file (`PDP-n` to tests), spec "As built" section, README status.
