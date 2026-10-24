Motion is subtle and short: 200 to 400ms, ease-out, driven by `timing` tokens.

- **Reveal**: sections and cards fade in and rise 16px (`dur-slow`, `ease-out`), once, staggered 60ms. An `IntersectionObserver` directive adds `.is-in`; content is visible without JavaScript (`.rp-js` is set by the directive).
- **Second image**: cards crossfade to the second view in `dur-slow`. Tiles scale their image 3% in `dur-slow`.
- **Blur-up and skeleton**: blur removed over `dur-slow`; shimmer 1.6s.
- **Hover**: colour, underline and arrow changes in `dur-fast`/`dur-base`. Drawers slide in `dur-base` with `ease-inout`; route changes cross-fade (`withViewTransitions`, `dur-slow`).
- `prefers-reduced-motion: reduce` removes all of it: elements simply appear, images swap without fading, the shimmer stops, the hero loop shows its poster.
