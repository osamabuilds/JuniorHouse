The form a signed-in shopper uses to return or exchange items from an order, opened from the order detail page.

- **Steps.** 1 choose the items (option cards with a checkbox, image, size and price), 2 the reason (a select) and an optional note, 3 the outcome: **Exchange for a different size** or **Refund** (`rp-option` radios). One marigold **Submit request**.
- **Aside.** The returns policy in three lines (window, collection, confirmation by email) and a Contact link. The window and the refund method and timing are placeholders: they are business settings, not design.

Rules: only items of the caller's own order can be chosen (SEC-05); the request is idempotent (a double click never creates two); the confirmation says what happens next and when; the page is `noindex`. Not drawn: the confirmation screen and the "not eligible" state (item outside the window).
