# Design references

Static HTML and PNG references for storefront screens that are **designed but not built**. Each folder has a README with the screens, the rules they must respect and the open points. They are references for the specs and builds that follow; none of them replaces a spec (`CLAUDE.md`: no production code until the spec is approved).

| Folder | Covers |
|---|---|
| [`customer-account/`](customer-account/README.md) | Sign in, create account, forgot password, check your email, set a new password, sign-in error |
| [`account/`](account/README.md) | Account overview, orders, order tracking, return or exchange, addresses, Child Profiles |
| [`cart-checkout/`](cart-checkout/README.md) | Bag page and drawer, empty bag, checkout with payment, order confirmed |
| [`shop-pages/`](shop-pages/README.md) | Search, collections, wishlist, sold-out product, size guide dialog, lightbox, 404, removed product, privacy and terms, cookie banner |
| [`navigation-menu/`](navigation-menu/README.md) | The opened Girls menu (desktop dropdown and mobile drawer) |

The storefront's own design system is in [`../design-system/`](../design-system/README.md); the shipped header, mega-menu, footer, home, listing and product pages come from SCRUM-207.

**Conventions.** Files are `kebab-case.html` with a same-named `.png`; desktop is 1440 wide and mobile 390. HTML is standalone (header and footer inlined) and loads Manrope from Google Fonts for the preview only. Anything the business must supply is a bracketed placeholder such as `[X–Y] working days`; prices and product names are sample content.
