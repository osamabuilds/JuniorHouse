# SCRUM-205: Storefront site chrome: tasks

- **Spec:** [spec.md](spec.md) · **Plan:** [plan.md](plan.md) (approved by the owner, 2026-10-24)
- Order matters. Each step is test-first: write the named failing test, then make it pass. Tag tests with the IDs they cover. All work is in `src/web/projects/storefront`, its e2e spec and docs.

## A. Palette
- [x] 1. Change the token values to black and white in `src/styles/_tokens.scss` (plan mapping) and mirror them in `docs/design-system/tokens.json`. Test: extend the contrast spec to assert 4.5:1 text and 3:1 control borders on all four surfaces, and that no clay hex remains (SC-17).
- [x] 2. Re-run Story 1 unit tests, `style-rules.spec.ts` and the Playwright/axe checks; fix only token-driven failures. Test: the existing suites pass (SC-19).
- [x] 3. Update Story 1's `design-foundation` spec notes (palette is now black and white). Test: n/a (docs).

## B. Contract and data
- [x] 4. `shared/models`: `SiteSettings`, `NavItem` (tree), `AgeStep`, `FooterColumn`, `FooterLink`, `LinkTarget`, `ContentPage`, `PageBlock` union, `DeliveryRule`. Test: type-level spec that a sample document satisfies the types.
- [x] 5. `shared/utils/resolve-href.ts`: pure `resolveHref(target)`. Test: each target kind gives the expected clean URL; unsafe values (`javascript:`) are refused (SC-3, abuse case 2).
- [x] 6. `shared/utils/validate-settings.ts`: duplicate keys, dead internal links, group with no members, depth over 3, age without code. Test: one failing sample per rule (SC-4).
- [x] 7. `shared/data/launch-settings.ts`: Girls, Boys, New in, Collections, Sale; Girls and Boys with member categories and sub-members as `Path` targets; ages 1Y to 8Y; footer columns; brand and WhatsApp `+923315613217`; six page summaries. Test: `validateSettings(launchSettings)` is clean (SC-3, SC-12, SC-14).
- [ ] 8. `features/storefront/site-settings`: NgRx state, actions, reducer, selectors (`selectHeaderEntries`, `selectAges`, `selectFooter`, `selectBrand`), `SiteSettingsApi`, effects with fallback (API error to last good copy to launch set), `TransferState` hand-off. Test: reducer and selector specs; effect spec with a failing API (ST-13).

## C. Dialog and shared pieces
- [ ] 9. `layout/dialog` controller on native `<dialog>`: `showModal()`, scrim click, Esc, scroll lock, focus return. Test: opens, traps, closes on Esc and scrim, returns focus (SC-8).
- [ ] 10. `rp-nav-tree` recursive template (depth constant 3). Test: renders members and sub-members as links; no fourth level (SC-3a).

## D. Layout components
- [ ] 11. `rp-announcement-bar` (from settings, hidden when disabled). Test: shows/hides by setting.
- [ ] 12. `rp-site-header` and nav list: landmark, entries from the store, wordmark, desktop search field as a GET form. Test: server-rendered HTML has `<header>`, `<nav aria-label="Primary">`, the enabled entries only; works without scripting (SC-1, SC-2, SC-10).
- [ ] 13. `rp-mega-menu`: opens on click/Enter only, Esc and outside click close, focus to first link, route change closes, hidden under 64rem. Test: keyboard and click specs; hover does not open (SC-3a, SC-6).
- [ ] 14. `rp-mobile-menu` on the dialog controller: nested accordions, search field, 44 px targets. Test: opens, nested expand, closes with focus return (SC-7, SC-8).
- [ ] 15. `rp-search-overlay` and `searchSuggest` feature: 200 ms debounce, `switchMap`, results capped at 4, text-only rendering, empty state, Enter to `/search?q=`. Test: fake-timer spec for debounce; markup typed into the box is shown as text; empty-state links (SC-9, SC-10).
- [ ] 16. `rp-site-footer`: columns from settings, contact and WhatsApp link, decorative wordmark, legal row, no newsletter. Test: footer renders from a settings fixture and follows a changed fixture (SC-11, SC-3).
- [ ] 17. App shell (`app.html`): skip link, header, `<main>`, footer. Test: `app.spec.ts` asserts one `<main>`, skip link target, landmarks (SC-1).

## E. Pages
- [ ] 18. `features/storefront/page`: `:slug` route, container, block components (heading, paragraph, list, table, callout, fact), `page` store and `PageApi`. Test: each block renders as text; a `fact` block shows the settings value; markup in text is not executed (ST-10).
- [ ] 19. SSR status and SEO: 404 for unknown slug, 410 for archived, title, description, canonical, Open Graph and Twitter tags, breadcrumb and `BreadcrumbList` JSON-LD. Test: unit spec on the meta service; e2e on server HTML and status codes (SC-13).
- [ ] 20. Launch-set page drafts and the not-publishable rule are SCRUM-206; here only: a page whose `status` is not published renders 404. Test: container spec.

## F. Verification
- [ ] 21. Playwright + axe `chrome.e2e.spec.ts` at 390, 768 and 1280 px: landmarks, mega-menu by keyboard, mobile menu, search GET without scripting, a page, 404 and 410, no horizontal scroll, axe clean with dialogs open (SC-1..13, 16, 19).
- [ ] 22. Lighthouse on a page (SEO 100, Accessibility ≥ 95) in the storefront container (SC-20).
- [ ] 23. Diff check: only `src/web/projects/storefront`, its e2e spec and docs changed; no formatting-only edits (SC-21).
- [ ] 24. Close the loop: update the spec with any deviations, mark Implemented, update READMEs and the memory note.
