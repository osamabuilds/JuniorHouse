Romp is a premium kidswear boutique for ages 1 to 12, made in Pakistan. The design is a **quiet gallery frame**: the interface never competes with the clothes. Product photography is the hero; fabric, fit and quality should be felt through the images. Quiet luxury: neither dull and flat, nor colourful. Build mobile-first: most visitors arrive on a phone.

This pass covers the **storefront only** (the admin keeps its current look). Read `Implementation.md` before writing code.

## Content fundamentals

- Voice: calm, specific, understated. Short sentences, concrete nouns, no hype ("amazing", "must-have"). Say what the garment is made of and how it feels.
- Casing: sentence case in source; eyebrows, nav, buttons and tags are uppercase through their styles, so never type them in capitals. Italics are for one phrase in a headline ("Dressed for *the day*"), never body copy.
- No emoji, no exclamation marks. Prices read `Rs. 4,990`. Ages read `2–10Y` in tags and "2 to 10 years" in prose, with an en dash.
- Provenance is quiet pride: "Made in Pakistan", never a flag. Errors say what to do next.

## Visual foundations

**Colour.** Layers, not hues: warm off-white `surface` (#F7F3EE), white `surface-raised` for cards and inputs, light sand `surface-sunken` for bands, and espresso `surface-dark` for two dark moments (editorial band, footer). Text is warm charcoal `ink` with `ink-muted` for secondary. **Clay is the only accent and stays under 5% of the screen**: the brand clay `accent` (#B5694A) for small marks (logo dot, bag count, nav underline, free-delivery bar), and `accent-deep` (#9C5537) wherever clay is text or a button fill (links, sale price, Add to bag), because the brand clay alone does not reach 4.5:1 on white. No gradients except the photography scrim and placeholder light; no saturated colours; product colour is shown only by the photo and round swatch dots. One theme only (no dark mode); dark sections use the `rp-dark` scope.

**Type.** Headlines are **Instrument Serif** (regular and italic), large and tight (-0.02em), fluid with `clamp()` over the container width: `display-xl` 52 to 112px, `display-lg` 40 to 72, `display-md` 30 to 48, `display-sm` 24 to 30, `display-xs` 20 (product names). Everything else is **DM Sans**: `body-lg`, `body`, `body-sm`, `caption`, uppercase tracked `eyebrow`, `nav` and `button`, and `price`. Few sizes, strong hierarchy. One `<h1>` per page.

**Shape and line.** Barely rounded: `radius-sm` is 2px; circles only for colour dots, the heart and the bag count. Structure is a 1px hairline (`line` for dividers, `line-strong` for controls, `ink` for ghost buttons). No shadows, except `shadow-float` under the mega-menu, drawers and quick view.

**Layout.** Tight grid so images dominate: 2 columns on phones with an 8px gap, 3 then 4 columns with a 16px gap; page margin 16 / 32 / 56px, max width 1600px. Sections are separated by tonal layers (off-white, sand, espresso) and 64px (phone) / 112px (desktop) of space. Every product image is **4:5** with one consistent crop.

**Imagery.** Full-bleed, soft natural light, warm and unretouched. Each product has at least a front and a second view (back or fabric close-up), plus macro fabric shots. Nothing is laid on top of an image except the hero text (on the scrim), a tiny `New` / `Low stock` tag and the heart. Until photography exists the generated placeholders (tonal grounds, flat garment silhouettes, CSS macro textures) hold the slots with identical aspect ratios.

**Motion.** 200 to 400ms ease-out: scroll fade-in, second-image crossfade, 3% tile zoom, drawer slides, route cross-fade. Nothing bounces. Reduced motion removes it all.

**States.** Hover changes colour, underline or the second image, never the layout. Focus is a 2px charcoal outline with 3px offset (cream inside dark sections). Selected is an ink fill. Disabled is transparent with `ink-muted` text and a reason nearby.

**Accessibility (WCAG 2.2 AA).** Text pairs 4.5:1, control borders 3:1; every target at least 44px (2.5.8); focus never hidden behind the sticky header or buy bar (2.4.11, use `scroll-padding`); swipe and pinch always have button and key equivalents (2.5.7); semantic landmarks, `aria-pressed`/`aria-current`/`aria-expanded`, focus trapped in dialogs, decorative shapes `aria-hidden`, `prefers-reduced-motion` respected.

## Iconography

Thin outline icons on a 24px grid, 1.25px stroke, round caps and joins, `currentColor` (search, heart, bag, menu, close, arrow, check, alert, truck, swap, chat, cash, pin). Drawn to match Lucide; use Lucide for anything new and flag it as a stand-in. The wordmark is lowercase "romp" in Instrument Serif with a clay full stop; a placeholder until a real logo exists.

## Pieces

`SiteHeader`, `MegaMenu`, `SearchOverlay`, `FilterBar`, `Hero`, `CategorySplit`, `ProductCard`, `Skeleton`, `ProductRail`, `GiftsStrip`, `EditorialBand`, `FabricBand`, `TrustLine`, `SiteFooter`, `Gallery`, `Lightbox`, `SizeSelector`, `CartDrawer`, `QuickView`, `ProductDetail`, plus `Button`, `Field` and `Motion`. Pages: `HomePage`, `HomeResponsive`, `ListingPage`, `ProductPage`. Class names begin `rp-`; `components/bundle.css` is the single stylesheet. Drop-in SCSS for the repo is in `handoff/storefront/`.

## Admin (back office)

The admin shares the storefront's tokens and voice but is **quiet and dense**: data, not photography, is the hero. Same palette (warm off-white ground, white cards, sand table headers, espresso sidebar), clay-deep for the one primary action, Instrument Serif for page titles and KPI numbers, DM Sans for everything else, 2px radius, 1px hairlines, no shadows. Light theme only.

It keeps the admin app's existing class names (`.button`, `.badge`, `.card`, `.toolbar`, `.tabs`, `table.responsive`, `.facts`, `.panel`, `.timeline`, `.banner`, `.pager`, `.qc-flag`, `.form-field`, `.form-grid`), restyled under one scope class `.ad`, so existing templates keep working. New: the sidebar shell (`ad-shell`, `ad-side`, `ad-top`, `ad-nav`), page header (`ad-head`, `ad-crumb`), KPI tiles (`ad-stats`), thumbnails and 4:5 image slots (`ad-thumb`, `ad-slot`), the two-column detail layout (`ad-two`). Status is always a word with a dot (`badge`, `qc-flag`), never colour alone. Pieces: `AdminShell`, `AdminList`, `AdminProductEditor`, `AdminDetail`, `AdminQuality`, `AdminFoundations`. Drop-in SCSS: `handoff/admin/_admin.scss`.
