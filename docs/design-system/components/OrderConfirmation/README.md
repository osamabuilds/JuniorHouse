The page shown after an order is placed. Calm and complete: a check mark, the order number, what happens next, then the details the shopper will want to keep.

- **Top.** `display-lg` "Thank you for your order", the order number as an eyebrow, and one sentence on what comes next ("We will message you when it ships"; the channel is the mobile number given at checkout).
- **Details box.** Delivering to, Payment, and Your items with their sale prices, then the summary rows (including the red savings rows and the "You are saving" line) and the final total, now the server's confirmed figure.
- **Actions.** Marigold **Continue shopping** and a ghost **Track your order**; for a guest, one line inviting them to create an account to save sizes and track orders.

Rules: all amounts and the order number come from the server; reloading or going Back never places the order again (idempotent, FR-28); the page works without the cart contents still in the browser; `noindex`. Placeholders: order number, address, payment line, fee and total.
