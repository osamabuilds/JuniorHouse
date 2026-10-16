The product card is image-first: nothing competes with the garment, and the page reads as a wall of photography.

- **Image**: always 4:5 (`--image-ratio`), `radius-sm`, no border, no shadow, no overlay. Two images per product: the front and a second view (back or fabric close-up).
- **Hover (desktop, 64rem+)**: the second image crossfades in over `dur-slow` (ease-out) and a Quick view bar slides up from the bottom; no zoom on cards. Keyboard focus inside the card does the same. Fetch the second image on first `pointerenter`/focus, not on load.
- **Touch (under 64rem)**: the two images are a swipeable `scroll-snap` carousel with two quiet dots (`rp-pagedots`); there is no hover and no Quick view bar (the bag-less card opens the product page).
- **Tag**: one tiny text tag at most, top-left: `New` or `Low stock` (clay-deep text). No Sale, Bestseller or percentage badges on images. The wishlist heart (44px target, `aria-pressed`) is top-right on a soft translucent disc.
- **Text**: name in `display-xs` serif (the only link, stretched over the card), `body-sm` meta in `ink-muted`, then the price (`price`). Optional colour dots (12px).
- **Grid**: 2 / 3 / 4 columns at 0 / 40rem / 64rem, **8px** column gap on phones and **16px** from 64rem, 24 to 32px row gap.
- **Loading**: see `Skeleton`.
