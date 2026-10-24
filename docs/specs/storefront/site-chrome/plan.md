# SCRUM-205: Storefront site chrome: technical plan

- **Spec:** [spec.md](spec.md) · **Settings contract:** [../storefront-settings/plan.md](../storefront-settings/plan.md) · **Module plan:** [../plan.md](../plan.md) · **Conventions:** ADR 0019, CONVENTIONS §1b
- **Module(s):** storefront web only (`src/web/projects/storefront`, plus the e2e spec and docs)

## Domain model (frontend types, `app/shared/models/`)
`SiteSettings` is the public snapshot of SCRUM-206 and the contract both stories build against: `brand` (name, tagline, whatsapp, email, hours), `announcement` (text, enabled), `navigation: NavItem[]` (a tree: key, label, kind `link` | `group` | `collection-list`, target, `children: NavItem[]` (two levels: parent and child), enabled, sort), `ages: AgeStep[]` (code, label), `footer: FooterColumn[]`, `delivery: DeliveryRule[]`, `exchange`, `pages: PageSummary[]`. `ContentPage` = slug, title, description, status, `blocks: PageBlock[]` (discriminated union: heading, paragraph, list, table, callout, fact). A `LinkTarget` is a typed union (page slug, listing path, collection code, external https), resolved to an `href` in one pure function (`resolveHref`), so a template never builds a URL.
Pure helpers, unit-tested: `resolveHref`, `visibleEntries`, `validateSettings` (duplicate keys, dead internal links, empty menus, ages without code) used by tests over the launch set and by dev-mode assertions.

## Application layer
| Piece | Responsibility | Covers |
|---|---|---|
| `SiteSettingsApi` (service) | `GET /api/storefront/settings` and `/pages/{slug}`; HTTP only | SC-3 |
| `LaunchSettingsProvider` | returns the built-in launch set (`shared/data/launch-settings.ts`) when the API is not reachable or not yet built; same shape as the API | SC-3, ST-13 |
| NgRx feature `siteSettings` (state, actions, reducer, selectors, effects) | loads settings once per request on the server, puts them in `TransferState`, hydrates on the client; selectors for header entries, ages, footer, brand | ADR 0019 §2 |
| NgRx feature `searchSuggest` | 200 ms debounced suggestions (`debounceTime` + `switchMap`), mock-backed service now | SC-9 |
| `PageEffects` / `page` feature store | loads one page by slug; maps API 404/410 to the real HTTP status on the server | SC-13, ST-8 |

## API endpoints consumed
`GET /api/storefront/settings`, `GET /api/storefront/pages/{slug}` (SCRUM-206). Suggestions: a mock service now; the real `GET /api/storefront/suggest?q=` comes with the search feature. Nothing is sent by the chrome.

## Events
None.

## Data
No database change. Files added: `shared/data/launch-settings.ts` (also the seed source for SCRUM-206, kept in sync by a test that compares the two once 206 lands), `shared/data/mock-products.ts` (four placeholder suggestion products, images through `rp-image`).

## NFR / security design
- **Palette (Q-SC-1):** `_tokens.scss` values change only; no component or class changes. Proposed mapping: `--surface` #ffffff, `--surface-raised` #ffffff, `--surface-sunken` #f5f5f5, `--surface-dark` #111111 (with `--surface-dark-raised` #1c1c1c, `--surface-dark-sunken` #0a0a0a), `--ink`/`--ink-on-light` #111111, `--ink-muted` #5f5f5f, `--line` #e5e5e5, `--line-strong` #767676, `--line-dark` #3a3a3a, `--line-strong-dark` #8a8a8a, `--accent` #111111, `--accent-deep` #000000, `--focus` #111111. Success and danger text keep their hues. Error and success tints stay as is. `tokens.json` gets the same values. The contrast test of Story 1 is extended to assert 4.5:1 for text and 3:1 for control borders on all four surfaces. Placeholder art in `_placeholders.scss` follows the new neutrals.
- **Server rendering:** header, menu panel markup, footer and pages are rendered in the server HTML from settings resolved before render and transferred with `TransferState` (no client fetch). Menus are real links; the mega-menu panel is present in the DOM and shown by state, so crawlers see the links.
- **Dialogs:** one small `DialogController` service/directive on native `<dialog>`: `showModal()`, scrim click, Esc, body scroll lock, focus return to the opener, with unit tests; mobile menu and search overlay use it.
- **Mega-menu:** a disclosure with `aria-expanded` that opens on click/Enter only; it lists the parent's children as links in columns through one `rp-nav-tree` template (depth is one constant, two today); Esc and outside click close it and return focus; closing on route change; below 64rem it is not rendered and the same tree is shown as accordions in the mobile menu.
- **Input safety:** the search text is only ever bound as text; the search form is a GET to `/search` that works without scripting; the query is URL-encoded by the router.
- **Resilience (ST-13):** if the API errors, the effect falls back to the last good copy held in `TransferState`/memory, then to the launch set, and logs once.
- **Motion:** only the dialog slide, mega-menu fade and nav underline; all behind `prefers-reduced-motion: no-preference`. No reveal directive, no view transitions (Q-SC-5).
- **Budgets:** the chrome adds no images except the one menu tile placeholder (`rp-image`, lazy); no client fetch before first paint; component SCSS stays inside the existing budget.

## Frontend structure
```
app/
  layout/
    site-header/       rp-site-header (container: reads store)       + components: nav-list, search-field
    mega-menu/         rp-mega-menu (presentational)
    mobile-menu/       rp-mobile-menu (dialog)
    search-overlay/    rp-search-overlay (dialog)
    site-footer/       rp-site-footer (presentational columns, contact line)
    announcement-bar/  rp-announcement-bar
    dialog/            dialog controller (shared by menu + search)
  features/storefront/
    site-settings/     store/ models/ services/ index.ts
    page/              containers/page-page.container, components/block-*, store/, services/, page.routes.ts (':slug')
    search-suggest/    store/ services/
  shared/models/ (SiteSettings, ContentPage, LinkTarget)  shared/data/ (launch-settings, mock-products)
```
Routes: `{ path: ':slug', loadComponent: page }` registered last, plus the dev `_foundations` route; reserved listing/search/product routes come from later stories (the reserved slug list in SCRUM-206 prevents clashes). `app.html` becomes header + `<main>` + footer with a skip link; `app.routes.server.ts` stays `RenderMode.Server`. Status codes: the page container sets the response status (404/410) through the SSR response token.

## Tests (every acceptance criterion has one)
Unit: settings selectors and `visibleEntries`/`resolveHref`/`validateSettings`; launch-set validation (SC-3, 4, 12, 14); each component (nav list, mega-menu keyboard and Esc, mobile menu dialog focus return, search debounce with fake timers, footer, page blocks); dialog controller; token contrast and style-rules (SC-16, 17, 18). E2E (Playwright + axe, `src/web/e2e/storefront/chrome.e2e.spec.ts`): server HTML landmarks and links, mega-menu by keyboard, mobile menu at 390 px, search GET without scripting, footer pages status/SEO tags, 404 and 410, no horizontal scroll, axe clean with dialogs open (SC-1, 2, 5..11, 13, 19). Lighthouse (SC-20) runs on the first public page that exists once Story 3 adds the home; until then the chrome is measured on a footer page.

## Risks & alternatives considered
- **Contract drift between 205 and 206.** Mitigated by a single typed `SiteSettings` model, owned by 206's plan, and one shared fixture (launch set) tested against both sides.
- **NgRx for settings.** ADR 0019 puts API-fetching features on NgRx; the spec's earlier "signal store" wording is corrected to NgRx.
- **Palette change touches Story 1.** Token values only; Story 1 tests (contrast, style rules, axe) re-run; Story 1's notes updated.
- **Open follow-up tasks:** Catalog age lookup to 1Y to 8Y; Catalog `ICollectionQueries` for the collections menu; storefront read model counts for "empty vs sold" (published-catalog feature).

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | n/a: anonymous, read-only public pages; no user data |
| Input validation | Only the search text; length-capped (100), treated as text; encoded in the URL |
| Output encoding | Angular bindings only; no `innerHTML`; hrefs built by `resolveHref` from typed targets; JSON-LD serialised by the framework |
| Outbound calls | None from the browser except the same-origin SSR routes; external link targets come from the allow-listed settings |
| Resource limits | Suggestions: 1 request per pause, results capped at 4; settings size is capped by 206 |
| Concurrency | n/a (read only); the last-write-wins issue does not exist client-side |
| Idempotency | n/a |
| Audit | n/a (no state change) |
| Data protection | No personal data collected; no cookies; WhatsApp/contact details are public business data |
| Secrets and dangerous configuration | None |
| Error responses | Real 404/410 for pages; a safe 5xx page; never a soft 404 |
| New dependencies | None |
| Coupling | Talks to the API only through `SiteSettingsApi`; does not import admin code; `shared` never imports `features` (architecture test) |
| Abuse cases considered | (1) Markup in the search box: shown as text. (2) A hostile or broken settings document: validated shape on receipt, falls back to the last good copy. (3) Keyboard trap in dialogs: tested. (4) Slow/failed API: last good copy, no empty header. |
