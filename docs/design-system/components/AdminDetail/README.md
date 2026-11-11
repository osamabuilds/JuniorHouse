Detail screens (Purchase order shown; also Vendor, Production run, Count sheet, Order).

- **Header.** A back link, the number as the `<h1>`, the status as a badge, one line of context, then actions: **one primary** (the next step the status allows) and a **More** menu for the rest. The current purchase order page can show up to seven buttons at once, two of them primary; this collapses to one primary, one More menu and nothing else. Destructive actions (Close order) sit at the bottom of the menu in red and always open a confirm dialog (see `AdminFoundations`). A status the user may not act on shows no primary at all.
- **Status help.** The sentence that explains the status stays, as an info banner under the header.
- **Progress.** A stepper (done, current, upcoming) replaces reading the history to learn where an order is; the current step is marigold, done steps are ink. It scrolls sideways inside its card on narrow screens.
- **Body.** The lines table with a totals footer on the left; facts below it. On 64rem and up the **History** timeline is a 360px right column; on phones it follows the content. Quantities and money are tabular and right-aligned.
- **Errors** go in a banner at the top of the page with `role="alert"`, never in a dialog.

Permissions still decide which actions exist (`hasPermission`); the design only changes how they are grouped. Status names and steps are sample data.
