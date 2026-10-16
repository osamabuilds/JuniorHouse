The hero is one full-bleed image, or a short muted loop, with minimal text bottom-left. It is the one big moment on the home page.

- Height: 600px phone, 680px tablet, 780px desktop (use `svh`-safe values in the app). `scrim-image` gradient (bottom third only) guarantees 4.5:1 for `on-image` text over any photograph (checked on the lightest and darkest placeholder).
- Copy: `eyebrow`, `<h1>` in `display-xl` with the last words in italic, one `rp-btn--light` and one `rp-link--light`. No lead paragraph, no badges, no second headline.
- **Photo**: `<picture>` art-directed (4:5 crop on phones, 16:9 on desktop), `NgOptimizedImage` with `priority` and `fetchpriority="high"`, preloaded; AVIF with WebP fallback. This is the LCP element: keep it under 150 KB on mobile.
- **Loop** (optional): `<video muted autoplay loop playsinline preload="metadata" poster="...">`, 6 to 8 seconds, under 1.5 MB, no audio track. The poster is the LCP image. Under `prefers-reduced-motion` (and when Save-Data is on) do not play it: show the poster only. Add a visually hidden pause button if the loop is longer than 5 seconds (WCAG 2.2.2).
- Until photography exists a generated warm still life (`rp-ph--sun`, an arch of window light) holds the slot.
