# SCRUM-203: Design foundation, technical plan

- **Spec:** [spec.md](spec.md)
- **Module(s):** storefront (web only; no API, schema or event changes)
- **ADR:** [0019](../../../adr/0019-storefront-web-conventions-and-design-system.md) (new, Proposed)

## Domain model
None. This story is presentation only. The one shared type is `ImageAsset { src, width, height, alt, blurDataUrl }` in `shared/models`; the published-catalog read model (Sprint 9) will map its image rows to it.

## Application layer
None. No store is added in this story (NgRx arrives with cart and wishlist in story 2 and with listing and product in stories 4 and 5).

## API endpoints
None.

## Events
None.

## Data
None. No database, no API calls. Placeholder images are static files in `public/placeholders/` (generated tonal grounds, no brand assets).

## NFR / security design
| Constraint | How |
|---|---|
| CLS 0 (NFR-01, DSF-8) | `rp-image` always sits in a box with `aspect-ratio: var(--image-ratio)` and sets `width`/`height`; skeleton and placeholder share the box. |
| LCP (DSF-9) | `priority` input maps to `NgOptimizedImage priority` (eager, `fetchpriority="high"`, preload link emitted by `NgOptimizedImage` during SSR with `ngSrcset` + `sizes`). Fonts: two preloads only, `font-display: swap`. |
| Fonts local (DSF-3) | `@fontsource` CSS in the `angular.json` `styles` array; the build copies the woff2 files, so no third-party request. Preload `<link>` for the two latin files is added in `index.html` using the hashed output names via a build step check (see task 5). |
| Tokens only (DSF-1) | Raw values in the handoff SCSS are lifted into tokens (Q-DF-8); a Vitest check scans `src/styles` and component SCSS for hex colours and `box-shadow` outside `_tokens.scss`. |
| One theme (DSF-2) | No dark-mode rules; check scans for `prefers-color-scheme`. |
| Reduced motion (DSF-12) | The handoff `prefers-reduced-motion` block is kept; `rp-image` reads the media query once in the browser to skip blur-up and crossfade. |
| Second image (DSF-11) | Hover path guarded by `(hover: hover)`; the second `src` is bound only after the first `pointerenter`/`focusin`. |
| Accessibility (DSF-4..7, 13) | Native `<button>`/`<a>`/`<input>` with attribute directives; 44px via min-height and hit-area pseudo-elements; labels above inputs; `aria-describedby` for hints and errors. |
| Dependencies | `@fontsource/instrument-serif`, `@fontsource-variable/dm-sans` (both SIL OFL fonts, MIT-licensed packages), pinned in `package.json` by npm. |

## Frontend
Under `src/web/projects/storefront`:

```
public/placeholders/                  static placeholder images (4:5, three tones)
src/styles.scss                       @use styles/tokens, styles/components, small reset
src/styles/_tokens.scss               copied from handoff (generated; raw values lifted into tokens)
src/styles/_components.scss           copied from handoff (only classes needed so far are exercised)
src/app/app.html                      <rp-root> shell: router-outlet only (header comes in story 2)
src/app/app.ts, app.config.ts         class="rp-root" host binding, image loader provider
src/app/app.routes.ts                 '_foundations' route (dev/test only, see below)
src/app/app.routes.server.ts          RenderMode.Server (P-2)
src/app/shared/models/image-asset.model.ts
src/app/shared/ui/image/              rp-image (component, spec, scss for local layout only)
src/app/shared/ui/image/image-loader.ts   IMAGE_LOADER token + default provider (Q-DF-4)
src/app/shared/ui/button/             button[rpBtn] directive (variant, size, block)
src/app/shared/ui/link-cta/           a[rpLink] directive
src/app/shared/ui/icon-button/        button[rpIconBtn] directive
src/app/shared/ui/field/              rp-field component (label, hint, error, control projection)
src/app/shared/ui/icon/               rp-icon component with the 13 icons
src/app/foundations/foundations.page.ts   the '_foundations' page
e2e/storefront/foundations.e2e.spec.ts    Playwright smoke + axe (folder name follows existing e2e layout)
```

`rp-image` inputs: `image` (`ImageAsset`, optional), `second` (`ImageAsset`, optional), `sizes` (string, presets exported as constants for card, gallery, hero), `priority` (boolean), `decorative` (boolean). It never exposes `NgOptimizedImage` internals to callers.

Routes: only `_foundations`, registered when `isDevMode()` or a build-time flag is on, with `noindex` and excluded from sitemap and robots by absence. Production builds omit it, so there is no public page yet; story 3 adds `''`.

SSR and SEO: the shell is already server-rendered; `<html lang="en">`, viewport and `class="rp-root"` are in `index.html` / host binding. Title and meta per route come in the SEO feature; the foundations page sets a `noindex` meta only.

## Risks & alternatives considered
- Copying the handoff SCSS verbatim keeps fidelity to the previews but carries raw values; lifting them to tokens risks a visual diff. Mitigation: compare 390/768/1280 screenshots of the foundations page against the preview before and after.
- `NgOptimizedImage` with a custom loader needs `ngSrcset` widths supplied; a mis-set loader would silently serve one size. A unit test asserts the six-width `srcset`.
- Tailwind/Material/other UI kits rejected: the design is hand-drawn tokens and `rp-*` classes; a kit would fight it.
- Convention split (storefront `rp-` / `shared/ui` vs admin `app-` / `features`) recorded in ADR 0019.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | n/a: no endpoints, no data. |
| Input validation | n/a: `rp-field` is a presentational wrapper; validation belongs to the forms that use it (checkout, newsletter) in later stories. |
| Output encoding | Angular template binding only; no `innerHTML`/`bypassSecurityTrust*`. SVG icons are authored in component templates, not injected strings. `blurDataUrl` is bound as a style value through a sanitised `data:image/*` check (rejects any other scheme). |
| Outbound calls | None. Fonts are local; no CDN. The image loader builds URLs from configuration only. |
| Resource limits | n/a. `srcset` limited to six fixed widths; blur image is about 20px. |
| Concurrency | n/a. |
| Idempotency | n/a. |
| Audit | n/a. |
| Data protection | No personal data, no child data, no logging. Placeholders contain no brand assets. |
| Secrets and dangerous configuration | None. The foundations route is off in production builds. |
| Error responses | n/a. A failed image load falls back to the placeholder; no broken-image icon. |
| New dependencies | `@fontsource/instrument-serif`, `@fontsource-variable/dm-sans`: static font files and CSS, no runtime code, no scripts. |
| Coupling | No module or API contact. `shared/ui` imports nothing from features; later features import `shared/ui` only. Admin project untouched (DSF-16). |
| Abuse cases | (1) A catalogue image with a hostile `blurDataUrl` such as `javascript:` or an SVG with script: rejected by the `data:image/(png\|jpeg\|webp)` check, otherwise placeholder. (2) Missing or empty `alt` on a content image: compile-time required input and a unit test; decorative must be explicit. (3) Enormous or missing `width`/`height` causing layout shift: inputs required and clamped; box ratio fixed at 4:5 regardless. |
