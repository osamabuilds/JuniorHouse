# Sold items in listings

- **Status:** Approved (owner, 2026-11-06, with all recommendations)
- **Jira:** none yet
- **Module:** storefront (API read side and web)
- **Requirement IDs:** FR-05, SEO-03, SEO-06, NFR-19; amends listings LST-15 and storefront-settings S-4 / ST-6 (owner decisions of 2026-11-05 and 2026-11-06)

## Problem & intent

Today a product with nothing in stock vanishes from every listing, so a shopper opening "Winter" or "T-shirts" when the range has sold out sees an empty page, and a shopper cannot tell that a style existed. The owner wants the shops' usual behaviour: sold-out products stay on **every** listing, marked as sold out, mixed in with the ones that can be bought. This applies to every navigation entry, main and child (an audience, a category, a season such as Winter or Summer, and the categories under a season).

## Decisions already taken (owner)

- ST-6 stays and wins over LST-15. Sold items appear on **every** navigation entry (Q-ST-9) and **always**, not only when nothing is in stock (Q-ST-10). LST-15 is dropped.
- The navigation follows the **shape** of Outfitters and Breakout with our own labels (Q-ST-11): a season at the top level with the categories under it.
- The six rules of Q-ST-12 are accepted as written below (SLD-2 to SLD-7).
- Dummy data for testing is made up, not copied from another shop (see Out of scope).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| SLD-1 | Given a published product with its main picture ready and no purchasable variant, when any listing (audience, category, collection, New in, Sale) is requested, then the product is listed as a sold-out card | ST-6, Q-ST-10 |
| SLD-2 | Given a listing with sold-out and in-stock products, then every in-stock product comes before every sold-out one, and the chosen sort applies inside each group | Q-ST-12 (1) |
| SLD-3 | Given a listing, then the "N products" count, the page count and the filter option counts include sold-out products | Q-ST-12 (2) |
| SLD-4 | Given the filter bar, then it offers an "In stock only" switch, off by default; when on, sold-out products are left out and the counts follow; the URL carries it and the canonical tag follows SEO-06 | Q-ST-12 (3) |
| SLD-5 | Given a sold-out card, then it shows a "Sold out" label as text, the picture is dimmed, the whole card still links to the product page, and there is no quick add and no size buttons that add to the cart | Q-ST-12 (4), NFR-19 |
| SLD-6 | Given a product page whose every variant is sold out, then its structured data `Offer` says `OutOfStock`; given any purchasable variant, then `InStock` | Q-ST-12 (5), SEO-03 |
| SLD-7 | Given a search, then sold-out products are included and come after the in-stock matches | Q-ST-12 (6) |
| SLD-8 | Given a size or colour filter and "In stock only" off, then a product matches when any of its variants has that size or colour, in stock or not; given "In stock only" on, only purchasable variants match, as today (Q-LST-9 same-variant rule kept) | (new) |
| SLD-9 | Given a listing with no products at all in its scope, then the empty state of LST-5 shows (the entry stays 200 and `noindex`); sold-out products count as products | ST-5, (new) |
| SLD-10 | Given a season entry (a collection) with category children, then `/collections/<season>/<category>` lists that category's products in that season, with the same rules and breadcrumb Home > Collections > Season > Category | Q-ST-9, (new) |
| SLD-11 | Given the home page "new arrivals" and the mega menu image tile, then they keep showing in-stock products only | (new, proposed) |
| SLD-12 | Given the `/collections` index, then a collection's piece count includes sold-out pieces and a collection with only sold-out pieces is listed | (new, proposed) |
| SLD-13 | Given the read model is rebuilt, then products that were hidden for having no stock appear without any manual step | (new) |
| SLD-14 | Given the demo environment, then the seeded shop has winter and summer ranges with a mix of in-stock and sold-out products | owner request |

## Non-functional constraints

- Listing queries stay at a fixed number of queries per page (no N+1) and paged (CONVENTIONS.md, LST-13).
- No stock numbers are sent to the browser; a card only says sold out or not (PCT-3).
- Sold-out cards meet WCAG 2.2 AA: the label is text, contrast holds on the dimmed picture, the link has an accessible name that includes "sold out".
- One `h1`, canonical and breadcrumbs unchanged on every page.

## Out of scope

- Downloading products, pictures or text from Outfitters or Breakout. Their catalogue and photographs are theirs; the dummy data is generated for this project.
- Back-in-stock alerts from a sold-out card (Notify Me exists on the product page).
- Hiding sold-out items automatically after a period.

## Open questions

- [x] **Q-SLD-1 Season URL (SLD-10).** Proposed `/collections/<season>/<category>`. A listing scope for "collection + category" does not exist yet. Confirm the URL, or say another. **Resolved: recommendation accepted.**
- [x] **Q-SLD-2 Home and menu tile (SLD-11).** Proposed: in-stock only, because those are shop windows. Confirm. **Resolved: recommendation accepted.**
- [x] **Q-SLD-3 Collections index (SLD-12).** Proposed: counts include sold-out pieces. Confirm. **Resolved: recommendation accepted.**
- [x] **Q-SLD-4 Navigation labels.** The shape is Season (Winter, Summer) > categories (T-shirts, Sweaters, Jackets, Trousers, Shorts, Dresses...). Confirm the season names and which categories sit under each, or say it is entered in the admin settings by you. **Resolved: the proposed shape and labels accepted (Winter and Summer, each with T-shirts, Sweaters, Jackets, Trousers, Shorts, Dresses).**

## As built

**API** (existing `Listings`, `Search`, `Navigation` and `CollectionsPage` features; no new folder, no schema change): `ProductVisibility` says a product is listable when it is published with its picture ready, and in stock is its own flag; the listing sort puts in-stock products first and applies the chosen sort inside each group; `stock=in` (`ListingFilter.InStockOnly`) leaves sold-out products out and the counts follow; size, colour and price match any variant unless `stock=in`; tiles carry `SoldOut` and no quantity; a new scope `CollectionCategory` serves `/collections/<season>/<category>` with the four-level breadcrumb; search includes sold-out products after the in-stock matches (suggestions stay in-stock only); home, the menu tile and menu flags keep in-stock only; the collections index counts every listable piece. A rebuild brings back products that were hidden (nothing else to run).

**Web** (storefront `listings` and `product-pages`): the product card says "Sold out" in text, dims the picture, names itself "<name>, sold out" and has no quick add; the filter bar has an "Availability" group with an "In stock only" box (a plain GET form field, `stock=in`, an active-filter chip, the canonical stays the clean listing); the route `collections/:code/:category` sits before the three-part product matcher; the product page now emits `Product` + `Offer` JSON-LD whose availability is `InStock` when any size can be bought and `OutOfStock` otherwise (`productStructuredData`, no stock count).

**Data**: `DemoStorefrontSeeder` (Development, behind `Demo:SeedSampleData`) makes a winter and a summer range of six categories, six pieces each, through the real commands (style, product, two generated placeholder pictures, composition, care, collection, publish) and posts warehouse stock, so a third of the pieces are fully stocked, a third have one size gone and a low size, and a third are sold out. The migration `SeedSeasonNavigation` replaces the launch navigation with Winter and Summer (each: T-shirts, Sweaters, Jackets, Trousers, Shorts, Dresses), Girls and Boys with the same categories (boys without Dresses), New in and Sale; it only runs while the navigation is still exactly the original launch tree, so an owner-edited menu is never overwritten, and the live menu changes when the owner publishes.

**Deviations from the spec, for the owner:**
- **Six pieces per category and season, not a dozen** (72 pieces): the seeder runs inside start-up and each piece is about forty commands. The number is one parameter.
- **No design-system sold-out card exists.** The card uses the existing `rp-tag` and `rp-card` classes with a dimmed picture (`.rp-card.is-sold-out`); a design-system entry should be added if the design changes.
- **The "Collections" menu entry is gone from the launch navigation**, because Winter and Summer are collections and would be listed twice; `/collections` is still in the footer.
- **Product `Product` JSON-LD did not exist before this feature**, so it was added whole (name, description, sku, image, one `Offer`); `seo` still owns the rest (aggregate fields, brand).
- The demo variants are written to Catalog's and Warehouse's copies directly, as the test fixtures do, because Labelling's own tables are not part of the demo.
