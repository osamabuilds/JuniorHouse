# Mega menu image tile: technical plan

- **Spec:** [spec.md](spec.md) (approved by the owner on 2026-11-05 with the recommendations for Q-MGT-1..3)
- **Module(s):** Storefront (API read side and web). No schema change.

## Decisions taken from the open questions

- **Q-MGT-1:** the tile is derived, with no admin change: the audience's newest listable product supplies the picture; the eyebrow is "New in" and the line "Just arrived"; the link is the audience's New in page.
- **Q-MGT-2:** the "The edit" column stays as it is (the collections).
- **Q-MGT-3:** the tile line uses the live heading style (Manrope), not the design's serif.

## Domain model

Read side only. `MenuAudienceDto` gains an optional `Tile: MenuTileDto?`:
`MenuTileDto(string Eyebrow, string Line, string Path, ListingImageDto Image)`. `Path` is the audience's New in path already in its entries (`/girls/new-in`); if the audience has no New in entry the tile links to the audience page.

## Application layer

| Query change | Input | Output | Covers |
|---|---|---|---|
| `GetMenuQueryHandler` | existing | after the tree is built, one grouped query picks, per gender code, the id of the newest listable product (`IsListable`, ordered by `PublishedAt` descending); one call to `ListingTiles.ImagesAsync(db, ids)` (two queries) returns their main images with derivative files; audiences whose product has no processed image get no tile | MGT-1, MGT-3 |

Three extra queries in total on an endpoint already cached for 60 seconds (`Cache-Control: public, max-age=60`), independent of the number of audiences (no N+1). The existing availability queries are unchanged.

## API endpoints

No new endpoint; `GET /api/storefront/menu` returns the extra optional field. Older cached responses without it keep working (the web treats a missing tile as none).

## Events / Data

None. No schema change, no migration.

## NFR / security design

- **NFR-01 / MGT-5:** the tile box has a fixed 5:4 ratio from CSS; the picture is `loading="lazy"` with width and height, and the panel is closed by default, so nothing is fetched until the visitor opens it.
- **SEO-01 / MGT-4:** the panel's links already exist in the server HTML (SC-2). The tile link and its alt text are in the HTML as well; only the image request is deferred.
- **NFR-19 / MGT-2, 8:** one `<a>` for the whole tile with an accessible name such as "Girls, new in"; the line sits on a scrim so contrast meets 4.5:1 over any picture.
- **MGT-6, 7:** open, close, focus and keyboard behaviour (SC-6) are untouched; under 64rem the mobile menu has no tile.
- **Payload:** one image reference per audience (a few audiences); the menu stays small.
- **ADR 0028:** the menu is already transferred through `TransferState` by its effect, so the tile arrives with it and nothing new is whitelisted.

## Frontend

- `features/storefront/navigation/models/menu.model.ts`: optional `tile` on an audience.
- `layout/mega-menu/` (and the nav-tree component that renders a panel): renders the tile when present, as the fourth column on desktop only (hidden under 64rem), using `rp-image`-style markup (`picture`, lazy, sized).
- `layout/mega-menu/mega-tile.component.{ts,html}`: presentational (`input()` tile, audience name).
- Styles: `rp-mega__tile` ported from the design's `_components.scss`, tokens only, scrim for contrast.

## Risks & alternatives considered

- **An admin field per audience for picture and copy:** more control but a settings model change, an editor and validation. Deferred to a later spec if the business wants it.
- **Computing the tile in the browser from listing calls:** extra requests on every page load. Rejected.
- No architectural choice that sets precedent, so no ADR.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | n/a, public menu |
| Input validation | no input; the tile is built from the read model only |
| Output encoding | alt text, eyebrow and line via Angular bindings |
| Outbound calls | none; `/media/` is same origin |
| Resource limits | three extra queries per cache miss; one picture per audience; 60 s public cache |
| Concurrency | read only |
| Idempotency | GET |
| Audit | n/a |
| Data protection | no personal or child data |
| Secrets and dangerous configuration | none |
| Error responses | a failure to find a picture means no tile, never an error response |
| New dependencies | none |
| Coupling | uses Storefront's own read model and `ListingTiles.ImagesAsync` (same module); web uses the navigation barrel |
| Abuse cases | (1) many requests for the menu: served from the 60 s cache; (2) an audience whose newest product is unpublished: the next newest is chosen, and with none there is no tile; (3) very long product text: not used (the line is fixed copy) |
