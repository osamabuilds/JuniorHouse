# Mega menu image tile

- **Status:** Implemented (2026-11-05)
- **Jira:** none yet
- **BRD sections:** FR-01 (navigation); design source `docs/design-system/components/MegaMenu/README.md`
- **Requirement IDs:** FR-01, NFR-01, NFR-17, NFR-19, SEO-07

## Problem & intent

The design's mega menu has three link columns plus one 5:4 image tile with a small eyebrow and a short line, which gives the panel a visual anchor (design audit G5). The live menu has the link columns (including shop by age) but no tile.

## Requirements (from the design system)

> **MegaMenu:** "Panel: `surface-raised`, 1px `line` bottom border, `shadow-float`. Three columns (Shop by age, Clothing, The edit) plus one image tile (5:4) with an `eyebrow` and a `display-xs` line. Behaviour: opens in `dur-base`, closes on Esc, on leaving the panel, or on route change; focus moves to the first link, Tab stays inside until closed, the trigger keeps `aria-expanded`. Under 64rem it is replaced by the mobile menu drawer."

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| MGT-1 | Given an audience whose panel has a tile, when the panel opens on desktop, then a 5:4 picture tile shows with an eyebrow and a short line, as one link | MegaMenu |
| MGT-2 | Given the tile, then it links to that audience's New in page and its accessible name states where it goes (for example "Girls, new in") | MegaMenu, NFR-19 |
| MGT-3 | Given an audience with no usable picture (no listable product with a processed image), then no tile is drawn and the columns fill the panel | (new) |
| MGT-4 | Given the server HTML, then the panel's links are present as today for crawlers; the tile's picture is lazy and carries alt text, width and height | SEO-01, SEO-07 |
| MGT-5 | Given the panel opens, then no layout shift occurs (the tile box has a fixed 5:4 ratio) and nothing new is requested until the panel opens (the picture is lazy) | NFR-01 |
| MGT-6 | Given the existing open, close, focus and keyboard behaviour (Esc, outside click, route change, focus to the first link), then it is unchanged | MegaMenu, existing SC-6 |
| MGT-7 | Given under 64rem, then the mobile menu is used and shows no tile | MegaMenu |
| MGT-8 | Given a tile, then it has no horizontal overflow and its text meets contrast on the picture (a scrim, as on the listing tiles) | NFR-19 |

## Non-functional constraints

NFR-01 (fixed ratio, lazy picture), NFR-19, SEO-07. The menu response is cached for 60 seconds site-wide, so the tile data must stay small (one picture reference per audience).

## Out of scope

A separate admin editor for tile images and text (see Q-MGT-1), tiles for collections, video.

## Open questions

- [x] **Q-MGT-1** Where do the tile picture and line come from? Options: derive them (the newest listable product's first picture for that audience, eyebrow "New in", line "Just arrived") or add an admin field per audience. Recommendation: derive them now, with no admin change; if the business wants chosen pictures and copy, add the admin field in a later spec. **Resolved: recommendation accepted.**
- [x] **Q-MGT-2** The design also lists a column "The edit". The live menu shows collections for it. Recommendation: unchanged (collections are "the edit"). **Resolved: recommendation accepted.**
- [x] **Q-MGT-3** Colour contrast: the design's tile line is `display-xs` serif; the live site uses Manrope only. Recommendation: use the live heading style. **Resolved: recommendation accepted.**

## As built

**API:** `MenuAudienceDto` gained an optional `Tile` (`MenuTileDto`: eyebrow "New in", line "Just arrived", the New in path of the audience and its newest listable product's main picture). `GetMenuQueryHandler` adds three queries to the menu (one grouped query for the newest listable product of each audience, then the two picture queries for all of them), so the menu is five queries however many audiences there are (the existing constant-query test now expects 5). An audience with no listable product, or none with a processed picture, has no tile. Every audience entry in the menu already has a New in entry, so the tile always links to `/<audience>/new-in`; that page handles its own empty state. 6 new server tests (Docker).

**Web:** `NavTile` on `NavItem` (set by `toNavItems` from the audience), rendered by `MegaMenuComponent` as a fourth-or-fifth column: one link named "Girls, new in", a lazy 5:4 picture with alt, size and srcset, a scrim and the text. The panel's columns now need 11rem each (was 14rem) so the four link columns and the tile fit on one row at 1280 px. The mobile menu uses the nav tree and never shows the tile (tested). The picture is not requested until the panel opens (browser test on a page with no product pictures).
