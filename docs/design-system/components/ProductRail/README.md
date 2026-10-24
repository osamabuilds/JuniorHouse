A horizontal scroller for "The edit", "Complete the look" and cross-sells.

- Cards are 58% wide on phones (the next card peeks), 32% from 40rem, four across from 64rem, with the same 8/16px gaps as the grid. `scroll-snap-type: x mandatory`, scrollbar hidden, scroll-padding matches the page margin.
- Desktop shows two 48px arrow buttons that scroll by one card (`aria-label` Previous and Next); the list has `tabindex="0"` and an `aria-label`, so keyboard users can scroll it without a pointer (dragging is never the only way).
