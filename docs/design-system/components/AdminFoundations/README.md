The shared parts. All use the storefront tokens (Manrope, ink on white, marigold `--action`); nothing new except one proposal: the amber status dot uses the existing `--action-deep`.

- **Colour.** Surfaces white on a `#f5f5f5` app ground; text ink; muted text `#5f5f5f` (4.5:1). Marigold is **only** the single primary button and the "now" marker in steppers and the current pick row. Status colours appear as a small dot (success green, amber, danger brick) next to a word; the word is ink.
- **Type.** Manrope. Page title 24 / 28px 700; card title 15px 700; body 14px; table header 12px caps; numbers tabular. Inputs 16px on touch, 14px from 40rem.
- **Shape.** 8px radius cards and buttons, 6px inputs, 1px hairlines (`--line`), no shadows except menus and dialogs.
- **Targets and focus.** 44px on touch (40px on desktop), 2px ink focus ring with 2px offset everywhere.
- **Destructive actions** open a confirm dialog (`role="alertdialog"`): says exactly what happens and that it cannot be undone, with the safe button first and focused.
- **Feedback.** Success is a toast with Undo where the action can be undone, errors are banners that stay, and loading is skeletons.

Admin remains light theme only.
