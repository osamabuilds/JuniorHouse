The saved-items page, opened from the heart in the header. Hearts are the same `rp-wish` control as on cards, now filled (`aria-pressed="true"`); pressing one removes the item.

- **Layout.** Breadcrumb, `display-lg` "Wishlist" with a count, a one-line notice for signed-out shoppers ("Saved on this device. Sign in to keep your wishlist everywhere"), then the product grid.
- **Cards.** Same `ProductCard`, including discounts (badge, sale price, saving). Each has **Choose size** (a ghost button that opens the product page or `QuickView`; adding needs a size). A sold-out item shows "Sold out · Notify me" instead of a button.
- **Empty.** "Nothing saved yet" with a single link to New in (not drawn separately).

Rules: a signed-out wishlist lives in the browser only and is merged into the account on sign-in; a signed-in one belongs to its owner (SEC-05); the page is `noindex`. Placeholders: names and prices.
