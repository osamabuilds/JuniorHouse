# Tasks: menu-redesign

Each step names the test that proves it. Ordered so every step leaves the build green.

1. [ ] Glossary check for the new column names (heading, tile, edit, mobile, bar); add missing abbreviations to `docs/db/naming.md`. Proof: the naming test over the EF model.
2. [ ] Domain: `NavItem` new fields, `NavEditLink`, `NavTile`, `MobileBarLink`, limits and invariants. Proof: domain tests (MR-2, MR-3, MR-4, MR-7).
3. [ ] Infrastructure: EF configurations (explicit `ToTable` / `HasColumnName`), migration `AddMenuRedesign`, seed the mobile bar. Proof: migration test on Testcontainers (CI) and the naming test.
4. [ ] Application: extend `SaveNavigationCommand` and its validator; add `SaveMobileBarCommand`; return the new fields from the draft and preview queries; one query, no N+1. Proof: application tests and a query-count test (MR-8, MR-10).
5. [ ] `SiteDocumentBuilder`: write the optional keys and omit empty ones; old snapshots still load. Proof: publish round-trip tests.
6. [ ] Endpoint `PUT /api/storefront/settings/mobile-bar` with the role check. Proof: integration test, 403 for a non-admin (CI).
7. [ ] Storefront model and launch data: optional fields on `NavItem`, `mobileBar`, validation test extended. Proof: settings validation spec (MR-8).
8. [ ] Mega-menu: headings, "Shop all", edit column, tiles, link restyle. Proof: mega-menu spec (MR-1..MR-4).
9. [ ] Header scrim: dim, close on click, Esc and link; reduced-motion rule. Proof: header spec (MR-5, MR-9).
10. [ ] Mobile menu and nav-tree: 56px rows, Shop all, Category and Age labels, age chips, bottom bar, drawer head. Proof: mobile-menu and nav-tree specs (MR-6, MR-7).
11. [ ] Admin: nav-node fields, edit-links list, mobile bar form, NgRx wiring. Tile editing is deferred (see the plan). Proof: admin component and store specs (MR-10).
12. [ ] Playwright + axe at 390, 768 and 1280 px, menu open and closed, desktop and phone. Proof: the e2e spec (MR-9, MR-11), run in CI.
13. [ ] Visual check against `docs/design/navigation-menu/*.png` in Docker (desktop and phone).
14. [ ] Update the spec's Implementation notes (what differs: tiles admin deferred), the storefront README status, and the site-chrome spec cross-reference.
