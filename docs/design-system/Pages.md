# Pages

Blueprints for the storefront. Sections appear in the order listed.

## Home (`/`)

1. Announcement bar and sticky `SiteHeader` (New in, Girls, Boys, Baby, Gifts; search visible).
2. `Hero`: full-bleed photo or muted loop, `<h1>` "Dressed for the day", one primary action and one link.
3. `CategorySplit`: Girls and Boys, two 4:5 tiles.
4. New in: eight 4:5 `ProductCard`s on the tight grid, "View all".
5. `EditorialBand` (espresso): one image, one italic line, link to the story.
6. `GiftsStrip`: four gift entries on a sand band.
7. `FabricBand`: macro textures and Fabric and care.
8. `TrustLine`, then `SiteFooter` (espresso, with the newsletter).

Heading order: one `<h1>`, `<h2>` per section, `<h3>` per card and tile. Landmarks: `<header>`, `<nav aria-label="Primary">`, `<main>`, `<section aria-labelledby>`, `<footer>`.

## Listing (`/new-in`, `/girls`, `/boys`, `/baby`, `/gifts`, `/search?q=`)

Breadcrumb, `<h1>` (`display-lg`), one-line lead, sticky `FilterBar`, active-filter chips, 4:5 grid (2 / 3 / 4 columns) with an editorial banner after the fourth card, "Show more". Filters (including age) and sort are query parameters so pages are shareable and server-rendered. Age pages are reachable from the mega-menu (`/girls?age=4-6`).

## Product (`/p/:slug`)

Breadcrumb, `ProductDetail` (two-column `Gallery` and sticky buy panel on desktop; swipe gallery and sticky buy bar on phone), `Lightbox` on image tap, `FabricBand` for the product, "Complete the look" rail. Add to bag opens the `CartDrawer`. Include `Product` JSON-LD (name, image, price, availability) and a unique title and meta description per CLAUDE.md.

## Bag and checkout (later)

The bag is the `CartDrawer`. Checkout is one page, guest by default: mobile number, address, delivery, payment (Cash on delivery, Card), review. Not part of this story.

## Other

Our story (reuses `EditorialBand` and full-bleed images), size guide, delivery and exchange (plain text pages in `body` with `display-md` headings).

## Admin (`src/web/projects/admin`)

Navigation (sidebar, same routes as today): Setup (Reference data, Styles, Catalog), Sourcing (Vendors, Purchase orders), Making (Production, Receiving and QC), Fulfilment (Labelling, Warehouse with the stock-alert badge). Screen types, all in `AdminShell`, `AdminList`, `AdminProductEditor`, `AdminDetail`, `AdminQuality`:

- **List** (products, vendors, purchase orders, batches, print jobs, stock): header, intro, toolbar, table, pager.
- **Editor** (product, vendor, PO draft): tabs, fieldsets, side checklist, one primary Save.
- **Detail** (purchase order, production run, count sheet): facts, line grid, status timeline, status-driven actions.
- **Quality and warehouse**: KPI tiles, status flags, blocking error banners.
