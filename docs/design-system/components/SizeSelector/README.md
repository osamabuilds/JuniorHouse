Colour swatches, size radios and the size guide.

- Swatches: 28px round dots with a 1px border (target padded to 44px); selected adds a 3px gap ring and a 1px `ink` ring. Each has an `aria-label` with the colour name, repeated in the `eyebrow` above. Dots only; no colour names on the swatch itself.
- Sizes (`rp-size`): 56px minimum width, 48px high, 1px `line-strong` border; selected is an ink fill. `role="radiogroup"`/`role="radio"` with arrow-key navigation. Sold out is `aria-disabled` with a diagonal strike and a "Notify me" link below.
- "Size guide" (a `rp-link`) opens a drawer with a table (age, height cm, chest cm) and one line of fitting advice. With no size chosen, Add to bag scrolls to the sizes and shows "Select a size".
