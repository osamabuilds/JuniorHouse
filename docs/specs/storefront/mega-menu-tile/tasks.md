# Mega menu image tile: tasks

Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md). Each step is test-first; the test named is written and seen failing before the code. Server tests run on CI (Linux) because Application Control blocks local DLLs.

## API (Storefront, .NET)

- [ ] **M1** `MenuTileDto` and the optional `Tile` on `MenuAudienceDto`. *Test: serialisation keeps existing fields and omits a null tile.*
- [ ] **M2** Pick the newest listable product per gender and load its main image through `ListingTiles.ImagesAsync`. *Tests (Testcontainers, tagged `[Trait("Spec", "MGT-x")]`): `Menu_AudienceTileUsesTheNewestListableProduct`, `Menu_SkipsUnlistableProducts` (an unpublished newest gives the next), `Menu_NoTileWhenNoProcessedImage` (MGT-3), `Menu_TileLinksToTheAudienceNewIn` (MGT-2), `Menu_RunsAFixedNumberOfExtraQueries` (a query counter with one and with three audiences proves no N+1).*
- [ ] **M3** Endpoint test: `/api/storefront/menu` still has `Cache-Control: public, max-age=60` and the existing shape. *Test: `Menu_Endpoint_KeepsCachingAndShape`.*
- [ ] **M4** `StructureSyncTests` and `Romp.ArchitectureTests` stay green.

## Web (Angular storefront)

- [ ] **W1** Optional `tile` in `menu.model.ts`. *Test: a menu without a tile parses; the sample menu in `testing/sample-menu` gains a tile case.*
- [ ] **W2** `MegaTileComponent`: one link, picture lazy with alt, width and height, eyebrow, line, scrim. *Tests: one `<a>` with the accessible name "Girls, new in" (MGT-2); image lazy with explicit size (MGT-4); renders nothing without a tile (MGT-3).*
- [ ] **W3** Render the tile inside the open panel on desktop and not in the mobile menu. *Tests (`site-header` and `mobile-menu` specs): the tile is in the panel DOM only for an audience that has one (MGT-1); the mobile menu has none (MGT-7); the existing panel tests (SC-2, SC-3a, SC-6) pass unchanged (MGT-6).*
- [ ] **W4** Styles: `rp-mega__tile` with a fixed 5:4 box and a scrim, tokens only. *Proves: raw-colour audit and e2e MGT-5, MGT-8.*

## End to end and close-out

- [ ] **E1** Browser tests in `e2e/journeys/storefront-mega-tile.e2e.spec.ts` (rebuilt container): at 1280 px the panel shows the tile and it links to the audience's New in page; the tile picture is not requested before the panel opens (network watch); open and Escape behaviour unchanged (MGT-1, 5, 6); at 390 px the mobile menu has no tile (MGT-7).
- [ ] **E2** Same file: no layout shift when the panel opens, the line has at least 4.5:1 contrast over the scrim, axe clean (MGT-5, MGT-8).
- [ ] **E3** Rebuild the docker API and storefront, check by eye, then update `spec.md` ("As built"), the storefront README row, the design audit (G5 fixed) and the memory hand-off.
- [ ] **E4** PR description links this folder and lists MGT-1..8, FR-01, SEO-07, NFR-01, NFR-17, NFR-19.
