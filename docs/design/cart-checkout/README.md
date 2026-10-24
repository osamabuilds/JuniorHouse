# Bag, checkout and payment: design reference

Static design reference for the bag, checkout and order confirmation. **Design only.** Cart, add-to-cart and checkout are not built or specified yet (storefront README: cart arrives in Sprint 11).

| Screen | Desktop (1440) | Mobile (390) |
|---|---|---|
| Bag | [cart.html](cart.html) · [png](cart.png) | [cart-mobile.html](cart-mobile.html) · [png](cart-mobile.png) |
| Bag, empty | [cart-empty.html](cart-empty.html) · [png](cart-empty.png) | n/a |
| Bag drawer (slide-in) | [cart-drawer.html](cart-drawer.html) · [png](cart-drawer.png) | n/a |
| Checkout and payment | [checkout.html](checkout.html) · [png](checkout.png) | [checkout-mobile.html](checkout-mobile.html) · [png](checkout-mobile.png) |
| Order confirmed | [order-confirmed.html](order-confirmed.html) · [png](order-confirmed.png) | [order-confirmed-mobile.html](order-confirmed-mobile.html) · [png](order-confirmed-mobile.png) |

The HTML files are standalone and open in a browser (the shared header and footer are inlined). They load Manrope from Google Fonts for the preview only; the storefront self-hosts its fonts. In the mobile PNGs the sticky bottom bar appears mid-page because the whole page is captured at once.

## What the design shows

- **Bag page:** item rows (image, name, size, quantity buttons, Remove), an order summary with a promo code field, subtotal, "Delivery calculated at checkout", an estimated total and one marigold Checkout button, plus a "You may also like" row. On mobile the total and Checkout sit in a sticky bar.
- **Drawer:** the bag as a slide-in panel over the page with a dimmed backdrop (`docs/design-system/Pages.md` says the bag is a `CartDrawer`). "View full bag" leads to the bag page. Decide whether the shop uses the drawer, the page, or both.
- **Checkout:** one page, guest by default: contact (mobile number first), delivery address, delivery method, payment (Card or Cash on delivery), and an order summary beside it (collapsed at the top on mobile).
- **Order confirmed:** thank-you message, order number, delivery address, payment, items and totals, with an invitation to create an account.

## Rules the design must respect

- **Totals are shown as estimates.** The server recalculates every price and total (SEC-09); the page says the final total is calculated when the order is placed.
- **Card details never touch our servers.** The card form assumes a hosted field from the payment provider (SEC-25: never log card data). The payment provider is not chosen.
- **Order placement is idempotent and stock decrement atomic** (FR-28, FR-29, NFR-08), which affects the "Place order" button's loading and retry states, not drawn yet.
- **robots.txt blocks cart, checkout and account pages** (SEO-05); these pages carry `noindex`.

## Placeholders and open points

- Prices and products are sample content. Delivery fee, delivery time, any cash-on-delivery fee, the order number and the courier are bracketed placeholders.
- Payment methods beyond Card and Cash on delivery are not in the design; none were added.
- Not drawn: payment failure and card-declined states, the stock-conflict message at checkout, address autocomplete, and an order-review step.
