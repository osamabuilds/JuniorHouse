# Plan: menu-redesign

- **Spec:** [spec.md](spec.md) · **Builds on:** [storefront-settings plan](../storefront-settings/plan.md), [ADR 0020](../../../adr/0020-storefront-settings-and-published-snapshot.md), [ADR 0019](../../../adr/0019-storefront-web-conventions-and-design-system.md)
- **Modules:** Storefront API (`STFR` schema), admin web app, storefront web. No other module is touched.

## Decisions taken from the spec
Per-year age chips (Q-MR-1); `?age=` kept (Q-MR-2); tiles hidden at launch (Q-MR-3); "The edit" has no launch links, so the column is absent until configured (Q-MR-4); settings and admin work are in this feature (Q-MR-5).

## Domain model (`Romp.Modules.Storefront.Domain/StorefrontSettings`)
No EF or ASP.NET references.
- **`NavItem`** gains `ShowShopAll` (bool, default true for menu entries), `CategoryHeading` (0..40, nullable), `AgeHeading` (0..40, nullable), `EditHeading` (0..40, nullable).
- **`NavEditLink`** (new child of `NavItem`): label (1..40), target (`LinkTarget`), sort, enabled. At most 8 per entry.
- **`NavTile`** (new child of `NavItem`): label (1..40), target, sort, enabled, image reference and alt text (1..140). At most 2 per entry. Invariants: an enabled tile needs an image and alt text.
- **`MobileBarLink`** (new, root level): label (1..24), target, sort, enabled. At most 4. Launch seed: Search (`/search`), Size guide (page `size-guide`).
- A published snapshot stays an immutable JSON document (ADR 0020); the new fields are optional keys in it, so older snapshots still validate.

## Schema (migration `AddMenuRedesign`, schema `STFR`)
Names follow `docs/db/naming.md`; each needs the glossary check in task 1 (new abbreviations are added to the glossary in the same PR).
- `NAV_ITEM`: add `SHOP_ALL_IND`, `CATG_HDNG`, `AGE_HDNG`, `EDIT_HDNG`.
- `NAV_EDIT_LINK`: `ID` (bigint identity), `NAV_ITEM_ID` (FK, indexed), `LBL`, target kind lookup id and `TARG_REF` (same two-column shape as `NAV_ITEM`), `SORT_SEQ`, `ACT_IND`, audit columns, concurrency token.
- `NAV_TILE`: as above plus `IMG_REF`, `ALT_TXT`.
- `MOBL_BAR_LINK`: `ID`, `LBL`, target kind and ref, `SORT_SEQ`, `ACT_IND`, audit columns, concurrency token.
- 3NF, declared FKs with indexes, no denormalisation. The tables are tiny and not partitioned.

## Application layer (feature folder `StorefrontSettings/Navigation`, existing)
- `SaveNavigationCommand` is extended: validates the new fields (lengths, counts, enabled tile has image and alt, targets resolve) with FluentValidation, and saves them with the tree in one transaction.
- New `SaveMobileBarCommand`; `GetDraftSettingsQuery` and `PreviewSettingsQuery` return the new fields.
- `SiteDocumentBuilder` writes the optional keys (`showShopAll`, `columns`, `edit`, `tiles`, `mobileBar`) into the published document, omitting what is empty.
- Row versions on every save (the existing concurrency pattern); no N+1: the tree and its new children load with explicit includes in one query.

## Endpoints
No new route for the tree: it rides on the existing save and get endpoints. New: `PUT /api/storefront/settings/mobile-bar`, role `Storefront.Admin` (checked server-side on every request, SEC-08). Anonymous `GET /api/storefront/settings` returns the new optional keys. No resource ID is taken from the caller beyond the settings singleton, so SEC-05 is not in play; no personal data.

## Tile images: needs an ADR before tasks 7 and 11
Tiles need an image, and the Storefront module has no media library. Options: (a) store an image URL served from the storefront's own asset path, uploaded through a new admin upload endpoint; (b) reference a published Catalog image by id through `Catalog.Contracts`; (c) leave the tile image field accepted by the contract but not editable in admin until one of these exists. **Recommendation: (c) for this feature**, because Q-MR-3 keeps tiles hidden at launch. The contract, validation and storefront rendering are built and tested with fixture images; the admin tile editor and the storage choice become a follow-up with its own ADR. This narrows MR-10 for tiles only; the spec is updated to say so when approved.

## Web: storefront (`src/web/projects/storefront`)
- `shared/models/site-settings.model.ts`: add the optional fields to `NavItem` and `mobileBar` to `SiteSettings`; the validation test covers them (duplicate keys, unresolved links).
- `layout/mega-menu`: column headings, "Shop all {label} →" at the end of the category column (ruled off), optional edit column, optional tile list (`rp-image`, explicit size, lazy), restyle of links; the layout stays a grid of labelled `<nav>`/`<section>` blocks with `aria-labelledby`.
- `layout/site-header`: a scrim element under the panel (`rp-scrim`), closed by click, Esc or link; fade removed under `prefers-reduced-motion`.
- `layout/mobile-menu` and `layout/nav-tree`: 56px rows with chevron, Shop all, "Category" and "Age" labels, age chips (44px pill), bottom bar from `mobileBar`; the wordmark in the drawer head; no Account or Bag.
- `shared/data/launch-settings.ts`: `mobileBar` seed; no tiles and no edit links.
- Colours and sizes come from tokens; no raw values outside the token file (SC-17).

## Web: admin (`src/web/projects/admin`)
`nav-node` editor gains: shop-all toggle, three heading fields, an "edit links" list (reuse the existing list-block row pattern and `row-actions`), with the 8-link limit; a new "Mobile menu bar" form on the navigation page with the 4-link limit. NgRx actions, effects, reducer and selectors follow `docs/CONVENTIONS.md`; the store keeps unsaved-change detection (`unsaved-changes.guard`).

## NFR and SEC
- **NFR-17 / NFR-19:** 390, 768 and 1280 px; axe open and closed (MR-9); 44px targets; focus ring; reduced motion.
- **SEO-01:** headings and links in the server HTML; tiles' alt text required.
- **SEC-08:** admin routes check the role server-side. **SEC-25:** nothing personal is logged.
- **NFR-01:** tile images have explicit dimensions; menu CSS adds no layout shift (the panel is absolutely positioned as now).

## Security & coupling review
| Check | Result |
|---|---|
| Cross-module reference | None. Tiles use no Catalog type under option (c); under (b) they would go through `Catalog.Contracts` only |
| Cross-schema join | None |
| Untrusted input rendered | All labels and headings through Angular bindings, no `innerHTML`; links validated as internal paths, page or collection targets, or `https` externals |
| Authorisation | Admin writes need `Storefront.Admin`, server-side |
| Idempotency, money | Not applicable |
| ADR needed | Yes, for the tile image source, before tile admin work (deferred under option (c)) |

## Tests (tag with `Spec` traits MR-n)
Domain (xUnit): invariants and limits. Application: validation, save and publish round trip, omission of empty keys. Persistence: Testcontainers migration test plus the naming test. Web (Vitest): mega-menu, mobile menu, nav-tree, header scrim, settings validation, admin editor. Playwright + axe at three widths (CI). Integration tests that need PostgreSQL run in Docker/Linux; local Windows policy blocks them.
