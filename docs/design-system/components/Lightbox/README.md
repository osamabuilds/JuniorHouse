Full-screen viewing with real zoom.

- Native `<dialog>` with `showModal()`; ground `lightbox` (95% charcoal). Top bar: `1 / 6` and a close button. The image is shown at its full 4:5 ratio, fitted to the viewport. Bottom hint line ("Pinch or double-tap to zoom").
- **Zoom**: click, double-tap or the `+` key toggles 2x about the pointer; pinch (two pointers) zooms freely up to 4x with Pointer Events (`touch-action: none`); drag pans when zoomed. Use a larger source (`sizes` 100vw, 2x DPR) only when the lightbox opens.
- **Moving**: arrow buttons on desktop, Left/Right keys, swipe on touch (swipe is disabled while zoomed). Esc closes and returns focus to the thumbnail; focus is trapped; background scroll locked. Every gesture has a button or key equivalent (WCAG 2.5.7).
