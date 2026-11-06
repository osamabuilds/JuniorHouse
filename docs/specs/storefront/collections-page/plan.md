# Collections page: technical plan

- **Spec:** [spec.md](spec.md) (approved by the owner on 2026-11-05 with the recommendations for Q-COL-1..3)
- **Module(s):** Storefront (API read side and web). No new module, no schema change.

## Decisions taken from the open questions

- **Q-COL-1:** a small paged endpoint `GET /api/storefront/collections` served from the read model.
- **Q-COL-2:** tiles follow the collection lookup order (`SORT_SEQ`, then name), which the owner already controls in the admin reference data.
- **Q-COL-3:** the tile image is the first live product's main image in that collection; if none has a processed image, the garment placeholder.

## Domain model

No new aggregate. The read model already holds `CollectionRow(ProductId, CollectionCode, CollectionName)` and `ProductRow.IsListable`. A collection is listed when at least one listable product belongs to it and its lookup row is enabled (the lookup cache already filters disabled rows).

## Application layer

| Query | Input | Output | Covers |
|---|---|---|---|
| `GetCollectionsQuery` | `page` (default 1), `pageSize` (default 24, max 48) | `CollectionsPageDto`: `items[]` (`code`, `name`, `path` = `/collections/<code>`, `pieces`, `image?`), `total`, `page`, `pageSize`, `totalPages` | COL-1, COL-2, COL-3, COL-6 |

Handler steps (two queries plus the lookup cache, so no N+1):
1. Load enabled collection lookups from `ICatalogLookupCache` (code, name, sort).
2. One grouped query: `Collections` joined to listable `Products`, grouped by `CollectionCode`, returning the count and the lowest `ProductId` order key of the product to use for the image (the same "first product" rule the listing uses: newest first).
3. Keep collections that are enabled in the lookup and have a count of at least one; order by lookup sort then name; page in memory (a shop has tens of collections, the cap is 48 per page).
4. One `IStorefrontImageQueries.GetThumbnailsAsync` call for all chosen main-image ids, mapped through the same helper `ListingTiles` uses, so the image shape (`alt`, `width`, `height`, sources) is the one the web already renders with `rp-image`.

## API endpoints

| Method | Route | Auth / role | Ownership check (SEC-05) |
|---|---|---|---|
| GET | `/api/storefront/collections?page=&pageSize=` | anonymous (`Policies.Public`), `Cache-Control: public, max-age=60` like `/menu` | n/a, public catalogue data only |

Unknown or out-of-range `page`/`pageSize` are clamped, not errors. The SSR server already forwards `GET /api/storefront/*`.

## Events

None. The read model is already kept current by the existing catalogue events.

## Data

No schema change and no migration. Index check (task C1): the collection read table needs an index on the collection code column and on the product id foreign key (the menu query already joins on them); the plan adds a migration only if the check finds one missing, with names following `docs/db/naming.md`.

## NFR / security design

- **NFR-01:** server-rendered; tile images use `rp-image` with explicit width and height; the first row of tiles is `eager`, the rest lazy. CLS stays at or under 0.1 (browser test).
- **SEC / data:** public data only; no cookies read; no customer data in the response (SEC-25).
- **SEO-03/04:** unique title "Collections", description from the brand, self-referencing canonical, Open Graph, `BreadcrumbList` (Home, Collections). Indexable. An empty result is a 200 with a message (COL-6), not a 404.
- **State transfer:** the new `collections` result slice joins the ADR 0028 whitelist (one line plus its test), so hydration shows no loading flash.
- **Resource limits:** page size capped at 48; at most one image query of 48 ids (`IStorefrontImageQueries.MaxIds` already enforces the ceiling).

## Frontend

`features/storefront/collections/` following `docs/CONVENTIONS.md`:
- `containers/collections-page/collections-page.container.{ts,html}`: reads the store, sets SEO, sets the response status, renders the heading, breadcrumb, `<ul>` of tiles and the empty message.
- `components/collection-tile/`: presentational `rp-tile` (image, scrim, eyebrow "Collection", name in `display-md`, "N pieces" text), one link per tile; reuses the home page tile classes.
- `models/collection.model.ts`, `services/collections-api.service.ts`, `store/` (actions, reducer keyed like the listings slice, selectors, effects with `switchMap`), `collections.providers.ts`, `index.ts`.
- Route `collections` registered before `collections/:code` in `app.routes.ts` (and the server routes file).
- Pagination reuses `PaginationComponent` (page in the query string, canonical per page).

## Risks & alternatives considered

- **Extend `/menu` instead:** would put counts and images into every page's menu payload. Rejected; the menu is cached site-wide and stays small.
- **Compute counts in the browser:** needs all listings. Rejected.
- Precedent: none beyond ADR 0028 (one new whitelisted slice), so no new ADR.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | Public read, anonymous policy; returns only listable catalogue data |
| Input validation | `page` and `pageSize` are integers, clamped to 1.. and 1..48; anything else falls back to defaults |
| Output encoding | Names and counts rendered by Angular text bindings; JSON-LD built with the existing escaping in `SeoService` |
| Outbound calls | none |
| Resource limits | page size cap 48; one grouped query plus one image query per request; 60 s public cache |
| Concurrency | read only |
| Idempotency | GET |
| Audit | n/a, read only |
| Data protection | no personal or child data involved |
| Secrets and dangerous configuration | none |
| Error responses | problem details from the existing host; the web shows the shared error page with status 503 on failure |
| New dependencies | none |
| Coupling | uses only Storefront's own read model, the Reference lookup cache and `IStorefrontImageQueries`; no cross-module internals |
| Abuse cases | (1) `pageSize=100000`: clamped to 48; (2) negative or text `page`: defaults to 1; (3) a collection whose products were all unpublished: dropped from the list by the count, never shown with zero pieces |
