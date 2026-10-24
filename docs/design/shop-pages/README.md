# Shop pages and states: design reference

Static design reference for storefront pages and states that sit around the main catalogue pages. **Design only.** The storefront already ships the home, listing and product pages, header, mega-menu and footer from SCRUM-207; this folder covers what was still missing.

| Screen | Desktop (1440) | Mobile (390) |
|---|---|---|
| Search results | [search.html](search.html) · [png](search.png) | [search-mobile.html](search-mobile.html) · [png](search-mobile.png) |
| Search, no results | [search-empty.html](search-empty.html) · [png](search-empty.png) | n/a |
| Collections | [collections.html](collections.html) · [png](collections.png) | n/a |
| Wishlist | [wishlist.html](wishlist.html) · [png](wishlist.png) | n/a |
| Product sold out and Notify me | [product-sold-out.html](product-sold-out.html) · [png](product-sold-out.png) | n/a |
| Size guide dialog | [size-guide-dialog.html](size-guide-dialog.html) · [png](size-guide-dialog.png) | n/a |
| Image lightbox | [image-lightbox.html](image-lightbox.html) · [png](image-lightbox.png) | n/a |
| 404 page | [404.html](404.html) · [png](404.png) | [404-mobile.html](404-mobile.html) · [png](404-mobile.png) |
| Product removed (410) | [product-removed.html](product-removed.html) · [png](product-removed.png) | n/a |
| Privacy and Terms | [legal.html](legal.html) · [png](legal.png) | n/a |
| Cookie banner | [cookie-banner.html](cookie-banner.html) · [png](cookie-banner.png) | n/a |

The HTML files are standalone and open in a browser (the shared header and footer are inlined). They load Manrope from Google Fonts for the preview only.

## What the design shows

- **Search:** a search field, a results heading with a count, filter chips and a sort, a product grid and pagination. The no-results page suggests popular searches and categories and shows new arrivals.
- **Wishlist:** hearts to remove, "Choose size" buttons, a sold-out item with "Notify me", and a line inviting sign-in to keep the list across devices.
- **Product sold out:** all sizes struck through, a SOLD OUT label on the image, and a "Notify me when it's back" form (size and mobile number or email) in place of the buy button (FR-11).
- **Dialogs:** the size guide (the selected size's row highlighted) and a full-screen image lightbox with previous/next, close and thumbnails.
- **Error pages:** a 404 with a search field and shop links, and a "this piece has left the collection" page with similar products.
- **Legal:** one layout for Privacy and Terms with a table of contents.
- **Cookie banner:** Manage, Reject optional and Accept all.

## Rules the design must respect

- **Real status codes** (CLAUDE.md): unknown pages return 404 and permanently removed products return 410; the "not found" and "removed" pages must never be served with 200.
- Exactly one `<h1>`, landmarks, labelled form fields, and visible focus on every page (NFR-19).
- Dialogs trap focus, close on Esc and return focus to what opened them.
- Filter, sort and pagination variants carry a canonical tag (SEO-06). Search results pages should be `noindex`.

## Placeholders and open points

- Product names, prices, collection names and piece counts are sample content. Legal text and cookie wording are placeholders to be supplied and reviewed.
- Collections and the cookie banner are not in the BRD requirements I checked; confirm they are wanted. The wishlist is listed among the footer pages in the site-chrome spec.
- Not drawn: a mobile version of every page here, the newsletter confirmation, and the wishlist's empty state.
