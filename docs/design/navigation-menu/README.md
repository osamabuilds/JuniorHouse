# Navigation menu: design reference

Static design reference for the opened Girls/Boys drop-down menu. **Design only.** The storefront already ships its own menu (`layout/mega-menu`, `layout/mobile-menu`, `layout/nav-tree`, driven by the storefront settings); this reference shows the intended look and behaviour so the two can be compared.

| Screen | Files |
|---|---|
| Desktop, "Girls" open (1440) | [menu-desktop.html](menu-desktop.html) · [png](menu-desktop.png) |
| Mobile drawer, "Girls" expanded (390) | [menu-mobile.html](menu-mobile.html) · [png](menu-mobile.png) |

The HTML files are standalone and open in a browser. They load Manrope from Google Fonts for the preview only; the storefront self-hosts its fonts.

## What the design shows

- **Desktop:** a full-width white panel hangs from the header, the page behind is dimmed, and the open item stays underlined. Three link columns (shop by category, shop by age, the edit) and two photo tiles that link to featured collections. Opens on click or Enter/Space; Esc, an outside click or choosing a link closes it.
- **Mobile:** a full-screen drawer with 56px rows. Girls, Boys and Sale-style rows expand in place (accordion): "Shop all", category links and age chips. Account, Search and Size guide sit in a bar at the bottom.
- The category, age and featured entries are placeholders. The Baby category is not part of the design.

## Open points

- Age links are drawn as pages such as `/girls/age-4-5`. `docs/design-system/Pages.md` instead describes age as a query parameter (`/girls?age=4-6`); SEO-02 asks for no query strings on indexable pages. Decide before building.
- Menu entries, ages and featured tiles should come from the storefront settings, not constants.
