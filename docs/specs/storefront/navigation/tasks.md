# Tasks: navigation (Sprint 9a)

Jira: SCRUM-209 · Plan: [plan.md](plan.md) · Spec: [spec.md](spec.md). Depends on published-catalog tasks 7-13.

**Scope note.** The header, mega-menu, mobile menu and footer already exist, are data-driven from the admin-managed storefront settings (SCRUM-206) and follow the 2026-10-24 references. This feature adds only what is still missing: which entries are shown (only those with listable products), the home page from the read model, breadcrumbs and the brand configuration. It does not replace the settings-driven menu; see the open question below.

**Q-NAV-6 decided 2026-10-25: the menu is built from lookups (see the spec amendment); task 3 is unblocked and reduced to the age links and shape. Q-NAV-7 (what becomes of the admin navigation editor) is open; nothing is deleted.**

1. [x] Domain: `MenuVisibility` (entry shown only if scope has a listable product) and `BreadcrumbTrail`. Proof: domain tests (NAV-3, 4). *Built as `MenuBuilder` + `MenuAvailability` + `BreadcrumbTrail` in `Storefront.Domain/Navigation`.*
2. [x] `GetMenuQuery`: counts by gender/category/collection/sale in one query, lookups via 60 s single-flight cache. Proof: handler tests incl. new lookup row appears with no code change (NAV-1, 3, 5, 9). *Built: two grouped queries + `MenuLookupCache` (60 s, single flight); `Storefront:MenuHiddenGenderCodes` (default UNISEX).*
3. [x] Menu shape from lookups only (no settings join): per-audience entries and age chips as `?age=<code>` links to the audience page. Proof: unit tests (NAV-3, 8, 9). *Done inside `MenuBuilder` (ages as `?age=<code>`).*
4. [x] `GetHomeQuery`: up to 12 New Arrivals (30-day window, fallback newest), audience tiles. Proof: handler tests, hero omitted when empty (NAV-10).
5. [x] Endpoints `GET /api/storefront/menu` and `/home`, `Storefront.Public`, `Cache-Control: public, max-age=60`. HTTP integration tests still to add (CI).
6. [x] Web `features/storefront/navigation`: models, `navigation-api.service`, NgRx store, menu and brand resolved before render into TransferState (no client fetch). Proof: store and service specs, SSR test (NAV-2).
7. [x] `components/breadcrumbs` (`<nav aria-label="Breadcrumb"><ol>`, last item `aria-current="page"`), used by listing and product routes. Proof: component spec and axe (NAV-4, 7).
8. [ ] `core/brand` `BRAND` token from SSR environment; header, footer, titles, Open Graph `site_name` and JSON-LD read it. Proof: render with two brands, no leftover string (NAV-11).
9. [~] Home page: one `<h1>`, hero (omitted if empty), audience tiles, no carousel; `Organization` + `WebSite` JSON-LD with `SearchAction`. Proof: component spec and no-JS fetch test (NAV-7, 10). *Built: audience tiles and New in from the API. Not yet: JSON-LD, brand token.*
10. [ ] Keyboard and phone checks (disclosure menu, Escape, focus trap, 44 px targets, 320 px). Proof: Playwright + axe in `navigation.e2e.spec.ts` (NAV-2, 6).
11. [ ] Update spec to what was built; README status.
