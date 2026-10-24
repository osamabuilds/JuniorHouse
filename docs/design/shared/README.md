# Shared header and footer: design reference

The header and footer used by every page in `docs/design/`, kept here as their own references. **Design only.** The shipped storefront has its own header, mega-menu, mobile menu and footer from SCRUM-207 (`layout/`).

| Part | File |
|---|---|
| Announcement bar and header | [header.html](header.html) · [png](header.png) |
| Footer | [footer.html](footer.html) · [png](footer.png) |

In the page files these two parts are inlined, so every page opens standalone. The open Girls menu is in [`../navigation-menu/`](../navigation-menu/README.md).

- **Header:** a black announcement bar ("Nationwide delivery · Size guide on every piece"), the `romp kids` wordmark, a primary nav (New in, Girls, Boys, Sale) and search, account and bag icons.
- **Footer:** the wordmark with a newsletter field and Subscribe button, then Shop, Help and Romp link columns and a copyright line.
- The announcement text and the newsletter promise ("never more than twice a month") are my wording; confirm them. The Baby category is not part of the design.
