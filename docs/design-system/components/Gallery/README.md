The product gallery makes the images the page.

- **Desktop (64rem+)**: a two-column grid of 4:5 images, 8px apart, all visible as the page scrolls; the buy panel is sticky beside it (seven columns of twelve for images). Each image is a `<button class="rp-gimg">` with `cursor: zoom-in` that opens the `Lightbox` at that image.
- **Phone**: one full-bleed 4:5 image at a time in a `scroll-snap` swipe strip, edge to edge, with a small `1 / 6` counter (`aria-live="polite"`). Tapping opens the `Lightbox`.
- Order: front, back, fabric close-up, detail, side, on-body or flat lay. The first image is the LCP element on this page (`priority`); the rest lazy-load. Alt text describes the view ("Linen shirt dress, back view").
