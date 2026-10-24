# Plan: chrome-redesign

Presentation only. No domain model, endpoints, events, schema or ADR change. No new security surface (all text still bound through Angular, no `innerHTML`, nothing collected). Frontend only, `src/web/projects/storefront`.

- **Header (CR-1, CR-2):** in `styles/_components.scss`, at 64rem and up, `.rp-left` becomes `display: contents`, the bar is a three-track grid (`auto 1fr auto`), the logo is ordered first, the nav is centred. Template unchanged, so phones and the server HTML are unchanged. Announcement bar text goes to 12px.
- **Footer (CR-3, CR-4):** `site-footer.component.html` drops `rp-dark` and the decorative wordmark and gains a brand heading (`<h2>` with the marigold dot, brand name from configuration). Styles lose the `rp-footer__big` rules; the tagline becomes body text; a top hairline is added.
- **Tests (CR-5, CR-6):** update the footer spec (light surface, brand heading, no decorative wordmark); add a header spec that asserts there is no account, bag or newsletter control. Existing Playwright axe specs cover contrast and layout; they run in CI (Docker/Linux).
- **Security & coupling review:** no change; no new dependency, no cross-module reference.
