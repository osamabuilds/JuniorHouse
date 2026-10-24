Loading never moves the layout, because every image and card keeps its 4:5 box.

- **Skeleton** (`rp-skel`): `skeleton` ground with a slow shimmer (`skeleton-hi`, 1.6s). Use for product grids while data loads and for gallery images before the blur preview exists; text lines are 18px and 14px bars at 70% and 45% width.
- **Blur-up** (`rp-blur`): ship a ~20px, base64 preview inline (`NgOptimizedImage` `placeholder` with `placeholderConfig`); show it blurred and scaled 6%, then remove the blur over `dur-slow` when the full image has loaded (`.is-loaded`).
- Reduced motion: the shimmer stops and the skeleton stays flat. Skeleton cards are `aria-hidden` with a single `aria-busy="true"` on the list.
