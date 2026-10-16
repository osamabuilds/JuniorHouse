# SCRUM-203: Design foundation, tasks

Ordered, test-first. Tests are Vitest (`npx ng test storefront --watch=false`) unless noted; Playwright for the smoke. Tag tests with `NFR-01`, `NFR-19`, `SEO-07` and the DSF ids in the test name.

- [ ] 1. Housekeeping: Jira task under SCRUM-203, merge `design/romp-design-system` to `main`, branch `SCRUM-xxx-storefront-design-foundation` from `main`. Test: `git ls-files docs/design-system` lists the handoff files on the branch.
- [ ] 2. Add `@fontsource/instrument-serif` and `@fontsource-variable/dm-sans`; add the three CSS files to the storefront `styles` array in `angular.json`. Test: `StylesConfig.fonts_areLocal_noExternalRequests` reads the built `index.html`/CSS and finds no `fonts.googleapis`/CDN URL, `font-display: swap` present (DSF-3).
- [ ] 3. Copy `_tokens.scss`; lift the raw values of `_components.scss` into tokens (Q-DF-8); wire `styles.scss` with the reset and `scroll-padding`. Test: `StyleRules.noRawColoursOrShadows_outsideTokens` and `StyleRules.noDarkModeRules` scanning `src/styles` and component SCSS (DSF-1, DSF-2).
- [ ] 4. `ImageAsset` model and `rp-icon` (13 icons). Test: `RpIcon.renders_eachIcon_decorativeByDefault` (DSF-13).
- [ ] 5. `index.html`: `lang="en"`, viewport, preload links for the two latin font files; `app.ts` host `class="rp-root"`; remove scaffold placeholder; server routes to `RenderMode.Server`. Test: `App.rendersShell_withRootClassAndNoPlaceholder` (DSF-14); build check that preload hrefs exist in output (DSF-3).
- [ ] 6. Button, link-CTA, icon-button directives. Test: `RpBtn.variants_applyClasses`, `RpBtn.disabled_hasReasonViaDescribedBy`, `RpBtn.targets_atLeast44px` via computed style (DSF-5, DSF-6).
- [ ] 7. `rp-field`. Test: `RpField.error_linksDescribedByAndSetsInvalid`, `RpField.label_isAboveInput` (DSF-7).
- [ ] 8. Image loader token and default loader. Test: `ImageLoader.srcset_hasSixWidths` (DSF-8).
- [ ] 9. `rp-image`: 4:5 box, explicit size, alt required, lazy by default, blur-up, placeholder/skeleton, `blurDataUrl` scheme check. Tests: `RpImage.box_isFourByFive_withWidthHeight`, `RpImage.noSrc_showsPlaceholderInSameBox`, `RpImage.blurDataUrl_rejectsNonImageScheme`, `RpImage.missingAlt_notAllowedUnlessDecorative` (DSF-8, DSF-10).
- [ ] 10. `rp-image` priority and SSR. Tests: `RpImage.priority_isEagerHighFetchPriority`, SSR render test asserting `fetchpriority="high"` and the preload link (DSF-9).
- [ ] 11. `rp-image` second view and reduced motion. Tests: `RpImage.second_notRequestedUntilPointerEnter`, `RpImage.second_notOnTouch`, `RpImage.reducedMotion_swapsWithoutTransition` (DSF-11, DSF-12).
- [ ] 12. Foundations page and dev-only route, `noindex`. Test: `Foundations.hasOneH1AndLandmarks`; Playwright `foundations.e2e` at 390/768/1280 with axe: no violations, no horizontal scroll, margins 16/32/56 (DSF-13, DSF-15).
- [ ] 13. Visual comparison of the foundations page against the design previews at 390/768/1280 (manual screenshot check recorded in the PR). Lighthouse mobile run on the foundations page: CLS 0 (DSF-8).
- [ ] 14. Diff audit: only storefront, `angular.json` storefront entry, package files and docs changed. Test: `git diff --stat main` reviewed; admin build and tests still pass (DSF-16).
- [ ] 15. Add the storefront section to `docs/CONVENTIONS.md`, set WCAG 2.2 AA in CLAUDE.md, set ADR 0019 Accepted; update spec to **Implemented** with deviations. PR links this folder and lists DSF ids.
