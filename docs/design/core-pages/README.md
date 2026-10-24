# Core storefront pages: design reference

Static design reference for the home, category and product pages, from the first redesign. **Design only.** The storefront now ships its own versions of these pages (SCRUM-207: `home`, `listing` and `product` containers built on the Manrope and marigold tokens), so this folder records the original monochrome concept for comparison, not a build target.

| Screen | Desktop (1440) | Mobile (390) |
|---|---|---|
| Home | [home.html](home.html) · [png](home.png) | [home-mobile.html](home-mobile.html) · [png](home-mobile.png) |
| Category (Girls) | [category.html](category.html) · [png](category.png) | n/a |
| Product | [product.html](product.html) · [png](product.png) | [product-mobile.html](product-mobile.html) · [png](product-mobile.png) |

The HTML files are standalone and open in a browser. They load Manrope from Google Fonts for the preview only; the storefront self-hosts its fonts.

## What the design shows

- **Look:** pure white page, black text, one sans family (Manrope), square corners with 1px hairlines, and marigold (`#D9A62E`) as the only accent, used for the single main action on each screen.
- **Home:** announcement bar, header, a split hero (headline, lead, two buttons, photo), "Find their fit" tiles (Girls, Boys, Essentials), "The autumn edit" product row, a black story band, and four promise lines. On mobile the photo comes first and the tiles stack in two columns.
- **Category:** breadcrumb, large title with a one-line lead, filter chips with an item count and sort, a four-column grid and pagination.
- **Product:** breadcrumb, a two-column gallery and sticky buy panel, size buttons with a disabled sold-out size, marigold "Add to bag", stock line, collapsible details (fabric, fit, delivery and returns, care) and a "Wear it with" row. On mobile the thumbnails sit under the image and the details collapse.
- The Baby category is not part of the design.

## Points to check against what shipped

- Product names, prices and images are sample content or placeholders.
- URLs in the BRD and CLAUDE.md are clean paths such as `/girls/dresses/floral-summer-dress`; age filters appear in `docs/design-system/Pages.md` as a query parameter, which SEO-02 discourages on indexable pages.
- Structured data (`Product`, `BreadcrumbList`), canonical tags and the performance budgets from CLAUDE.md are implementation concerns and are not shown here.
