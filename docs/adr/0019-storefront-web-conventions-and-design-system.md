# 0019: Storefront web conventions and design system

- **Status:** Proposed
- **Date:** 2026-10-17

## Context
`docs/CONVENTIONS.md` section 1 describes the admin app: `app-` selectors, `features/<module>/<feature>/`, NgRx in every feature. The storefront has a hand-off design system (`docs/design-system/`) with `rp-` class names, attribute-directive UI primitives and its own folder map (`layout/`, `shared/ui/`, `shared/…`, `features/<page>/`). The two projects are separate Angular apps with separate tokens.

## Decision
1. The storefront follows the design handoff for presentation: `rp-` selectors and CSS classes for design-system pieces, attribute directives on native elements for buttons, links and icon buttons, and the folders `layout/`, `shared/ui/`, `shared/models/`.
2. Features that fetch API data (listing, product, search, cart, wishlist) keep the conventions that matter for correctness: standalone OnPush components, containers read an NgRx store, presentational components use `input()`/`output()` only, services do HTTP only, paged lists. They live in `features/storefront/<feature>/` with the `app-`-free `rp-` prefix for their selectors.
3. All visual values come from `src/styles/_tokens.scss` (generated from `tokens.json`); component SCSS holds layout only. No raw colours, radii, shadows or durations elsewhere.
4. One theme only. Accessibility target is WCAG 2.2 AA.
5. Images go through `rp-image` and one image loader token; formats follow decision P-5 (WebP and JPEG in Sprint 9, AVIF later by configuration).
6. The admin project keeps its own conventions until its own re-skin story.

## Consequences
- CONVENTIONS.md gets a short storefront section pointing here; CLAUDE.md WCAG line moves to 2.2 AA.
- Architecture tests for the web (import direction: `shared` never imports `features`) apply to both projects.
- Two selector prefixes in the repo (`app-` admin, `rp-` storefront), deliberate.
