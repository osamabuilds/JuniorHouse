The mega-menu opens under the header from Girls, Boys or Baby (hover or focus-in on desktop, click and Enter anywhere). **Age filters live here**: the first column is Shop by age (Baby 0–2, Toddler 2–4, Little 4–6, Junior 6–9, Big kids 9–12), each linking to the category filtered by age (`/girls?age=4-6`).

- Panel: `surface-raised`, 1px `line` bottom border, `shadow-float`. Three columns (Shop by age, Clothing, The edit) plus one image tile (5:4) with an `eyebrow` and a `display-xs` line.
- Links are `display-xs` serif with a 4px nudge and `accent-deep` on hover; 38px rows. Column titles are `eyebrow`.
- Behaviour: opens in `dur-base`, closes on Esc, on leaving the panel, or on route change; focus moves to the first link, Tab stays inside until closed, the trigger keeps `aria-expanded`. Under 64rem it is replaced by the mobile menu drawer.
