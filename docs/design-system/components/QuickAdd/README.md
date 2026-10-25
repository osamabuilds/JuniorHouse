A way to add to the bag **from the product picture**, and to see **which sizes exist** before opening the product. It replaces the old "Quick view" hover bar on the listing card (Quick view stays as a link inside the product page flow).

**On the picture**
- **Desktop (64rem+).** Hovering or focusing a card slides a bar up from the bottom of the image: "Choose a size" and a row of size chips (`rp-size`, 44px). Pressing an available size adds it at once; the bar then says "Added, size 4Y" with **View bag**. One press, no page change.
- **Phone and tablet.** There is no hover, so a round **+** (44px, ink) sits bottom-right on the image. Tapping it opens the same bar over the picture. The **+** is a button with `aria-expanded`; the bar has a close button.
- **Sold-out sizes** stay in the row, struck through and `aria-disabled`, with "sold out" in the accessible name. Never hidden, never colour alone. If every size is sold out there is no **+**; the bar offers **Notify me** instead.

**Under the name (always visible)**
A compact size line (`rp-avail`): `2Y 3Y 4Y 5Y 6Y 8Y` with sold-out sizes struck through. A shopper sees at a glance whether their child's size is in stock without hovering. If there are too many sizes for one line it wraps; it is never truncated, so a size is never hidden.

**Rules**
- Stock, price and the added line come from the server. The page never decides availability, and the add is checked and decremented atomically on the server (FR-28, FR-29, SEC-09). If a size sells out between page load and the tap, the bar says so and refreshes the chips.
- A product with one size or no size (a hat) shows **Add to bag** directly in the bar, no chips.
- The size line and chips use the same size codes as the product page (the ages configured in the settings).
- Keyboard: the bar opens on focus-within; Tab moves through sizes, Enter or Space adds, Esc closes. Focus returns to the **+** or the card.
- The marigold is not used on cards (one main action per screen); the bar's actions are ink.

**Changes from earlier rules.** `ProductCard` said the card has no bag control and touch cards just open the product page. This amends that. There is no cart spec yet (Sprint 11), so this is a design proposal; the bag, the added state and the notify flow need a spec before build.
