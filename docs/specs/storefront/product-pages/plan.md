# SCRUM-27: Product pages: technical plan

- **Spec:** [spec.md](spec.md) · **Module plan:** [../plan.md](../plan.md)
- Open questions at their proposed default: Q-PDP-1 (availability only, no purchase controls), Q-PDP-2 (colour travels as `?colour=` and canonicalises to the product), Q-PDP-3 (care symbols shown as text until the artwork licence is settled), Q-PDP-4 (cm only).

## Domain model
`ProductPageModel` (read model, no behaviour): product, variants grouped by colour, per-size `AvailabilityClass`, images ordered with colour overrides (`PRDT_IMG.CLR_CODE` set ⇒ shown when that colour is selected, otherwise product-level images), composition (fibre, %), care (name, symbol code), size chart lines for (category, gender, Unisex fallback), displayed price rule: selected variant's price, else "From PKR {lowest available}" (PDP-15), compare-at shown only when the variant has one above its price.

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `GetProductPageQuery` | path (normalised), optional colour, size | `Found(model)`, `Moved(newPath)`, `Gone`, `NotFound` | PDP-1..17 |

Flow: normalise the path (lower case, trailing slash; a change ⇒ `Moved` with the normalised path, URL-5); resolve through Catalog's `IProductPathQueries.ResolveAsync` (single indexed read, redirects already collapsed to one hop, unpublished reads `NotFound`, archived `Gone`); for `Current`, load the page from `STFR` by product id (5 queries, constant: product, variants, images + derivatives, composition + care, chart lines). If Catalog says `Current` but the projection has no row yet (event not yet applied), the answer is `NotFound` with no caching (the next request after the event sees it); the case is logged as a counter so slow projection shows up.

Selected colour defaults to the first colour with an in-stock size; an unknown colour or size in the query is ignored. Highlighting in the size chart uses the selected size.

## API endpoints
| Method | Route | Auth | Notes |
|---|---|---|---|
| GET | `/api/storefront/products/by-path?path=&colour=&size=` | `Storefront.Public` | 200 `Found` with the model; the other outcomes are returned as a small JSON body with the target path so the SSR layer sets 301/404/410 itself (the API's own status is 200 for all four to keep the SSR contract simple; SSR is the only caller) |

Ownership check: n/a.

## Events
None.

## Data
Reads only: `PRDT_MAIN`, `PRDT_VRNT`, `PRDT_IMG` + `IMG_DRV`, `PRDT_CMPS`, `PRDT_CARE`, `CHRT_MAIN`/`CHRT_LINE`, `AVLB_STK`.

## NFR / security design
NFR-01: the HTML contains the gallery's first image with `fetchpriority="high"` and a preload `Link` header for the LCP image; derivatives are fixed-width with `srcset`/`sizes`; fonts limited to what the design needs (system font stack unless the owner chooses a brand font, then `font-display: swap`, preloaded, self-hosted); no third-party scripts. SEC-09: the page prints only server values; no price arithmetic in the browser except picking a price the server already sent. Availability copy follows PDP-17. Colour/size selection works without JavaScript through links (`?colour=navy`), and with JavaScript updates in place without a navigation.

## Frontend (`features/storefront/product-pages`)
- `containers/product-page` (resolver returns the outcome; the server sets status, `Location`, headers), `components/gallery` (main `<figure>` + thumbnail `<ul>` of buttons labelled with the alt text, keyboard operable, no autoplay), `components/colour-swatches` (radio group in a `<fieldset>`; swatch plus text name, selected state not colour-only), `components/size-selector` (radio group; sold-out sizes `aria-disabled` with visible "Sold out"; not-yet-available sizes labelled), `components/price` (current, compare-at as `<s>` with visually hidden "was", saving), `components/availability-note`, `components/size-chart` (`<table>` with `<caption>`, `th scope="col"/"row"`, selected row marked), `components/composition-care` (`<dl>` for fibres, `<ul>` for care with text and, once licensed, symbol images with empty alt because the text carries the meaning), `components/sold-out-state`.
- Structure: breadcrumb, `<main>`, one `<h1>` (product name), `<section>`s with headings that never skip levels (h2 for "Size guide", "Composition", "Care").
- JSON-LD and meta come from `seo` (`SeoService.forProduct(model)`); the page declares nothing itself.
- State in NgRx store (`selectedColour`, `selectedSize`); everything from the server is in the store via TransferState.

## Risks & alternatives considered
Calling Catalog's path contract at request time couples the page to Catalog's availability; the call is in-process (same host) and a single indexed read, and the alternative (duplicating slug history in `STFR`) would copy a rule ADR 0013 says must live in one place. If Catalog is slow, the page is slow; the load test (S18) covers it.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | Anonymous GET, explicit policy; unpublished and draft products read as not found |
| Input validation | Path normalised and length-capped (200); colour and size are looked up among the product's own codes, others ignored |
| Output encoding | Angular encoding; alt text and descriptions as text; structured data serialised safely (see seo) |
| Outbound calls | none (in-process contract call) |
| Resource limits | 5 constant queries; images capped at 10 per product by Catalog; edge cache 60 s; edge and SSR rate limits |
| Concurrency | n/a (read only) |
| Idempotency | n/a (GET) |
| Audit | n/a |
| Data protection | No personal data; exact quantity never in the response (test); no cookies |
| Secrets and dangerous configuration | none |
| Error responses | Outcomes map to real 301/404/410; 5xx never cached; ProblemDetails from the API |
| New dependencies | none |
| Coupling | `Catalog.Contracts.IProductPathQueries` plus `STFR`; no Catalog table read |
| Abuse cases | Path enumeration (rate limits; 404 is cheap and short-cached); long or encoded-traversal paths (normalised, length-capped, no file access from the path); open redirect via `Moved` (target comes from Catalog, never from the request) |
