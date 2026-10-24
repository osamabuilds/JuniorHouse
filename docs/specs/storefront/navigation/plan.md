# SCRUM-21: Navigation, breadcrumbs and home page: technical plan

- **Spec:** [spec.md](spec.md) · **Module plan:** [../plan.md](../plan.md)

## Domain model
`MenuTree` (pure): audiences (genders, ordered by `SORT_SEQ`) → entries New In, Sale, categories, collections; age bands as filter links. A node is **shown** only if it has at least one listable product (Q-NAV-3 default). `BreadcrumbTrail` builds the trail from a route description (home › audience › category › product).

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `GetMenuQuery` | none | menu tree with the lookup codes, names, paths and which entries have listable products (one query for counts by gender/category/collection, lookups via the 60 s cache) | NAV-1, 3, 5, 8, 9 |
| `GetHomeQuery` | none | up to 12 New Arrivals tiles (listable, newest first within `NewArrivalsDays`, falling back to the newest listable) and the audience tiles | NAV-10 |

## API endpoints
| Method | Route | Auth | Notes |
|---|---|---|---|
| GET | `/api/storefront/menu` | `Storefront.Public` | cached in memory 60 s |
| GET | `/api/storefront/home` | `Storefront.Public` | cached 60 s |

Ownership check: n/a (no user data).

## Events
None published or consumed beyond the projection.

## Data
No new tables. Menu counts are derived from `PRDT_MAIN` (`GNDR_CODE`, `CATG_CODE`, `LSTB_IND`, `SALE_IND`, collection map). Age bands come from the Reference list query through the cache; filter links use the lookup **code**.

## NFR / security design
The menu is part of every page, so it comes with the server render (resolved before render, put in TransferState; no client fetch). Cached per node for 60 s; cache size is one entry. Items are `<a href>` built from codes validated against the lookup (nothing from the request reaches the markup).

## Frontend (`features/storefront/navigation`)
- `containers/site-header`, `containers/home-page`; `components/mega-menu` (disclosure pattern with real links; opens on click/focus, closes on Escape, no hover-only behaviour), `components/mobile-menu` (full-screen dialog from a `<button aria-expanded>`, focus trap, 44 px targets), `components/breadcrumbs` (`<nav aria-label="Breadcrumb"><ol>`; last item `aria-current="page"`, not a link), `components/audience-tiles`, `components/product-tile` is shared from `listings`.
- `store/` holds the menu and site config (brand) once per request; `models/` DTOs; `services/navigation-api.service.ts`.
- **Brand:** `core/brand` provides `BRAND` (name, tagline, logo path, site name) injected from the SSR server's configuration; header, footer, titles and JSON-LD read it. A unit test renders with two brands and asserts no leftover string.
- **Design:** black-and-white base, premium and fashion-forward (D-6): tokens (CSS custom properties) for ink, paper, one muted accent, type scale and spacing live in `styles.scss`; no colour carries meaning alone; focus ring visible on both backgrounds; contrast checked against WCAG 2.1 AA. Final visuals are reviewed with the owner on staging; the layout and tokens are structured so the look can change without touching components.
- Home: one `<h1>` (brand tagline), hero tile from New Arrivals (omitted if empty), audience tiles as links, no carousel.

## Risks & alternatives considered
A hover-driven mega-menu fails touch and keyboard users; a disclosure menu with links is chosen. Age landing pages are deferred (D-1). Colour/typeface choices are not final until the brand name is (B-1), so they are tokens.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | Anonymous GET only, explicit policy |
| Input validation | No request input; codes come from lookups |
| Output encoding | Angular encodes; links built from validated codes |
| Outbound calls | none |
| Resource limits | One cached tree; tile counts capped at 12 |
| Concurrency | n/a (read only) |
| Idempotency | n/a (read only) |
| Audit | n/a (no change of state) |
| Data protection | No personal data; no cookies set |
| Secrets and dangerous configuration | Brand config validated at start-up (non-empty name, logo path inside the allowed folder) |
| Error responses | ProblemDetails; the SSR page shows a safe error page with a 5xx, never cached |
| New dependencies | none |
| Coupling | Reads Reference lookups through its contract and the `STFR` model only |
| Abuse cases | Menu cache stampede (single flight); a category code containing markup (codes validated on write in Reference, and encoded on render); keyboard trap in the mobile menu (tested) |
