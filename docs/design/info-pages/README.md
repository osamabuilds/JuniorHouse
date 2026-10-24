# Help and company pages: design reference

Static design reference for the pages the footer links to. **Design only.** The storefront already serves content pages from its settings by slug (`delivery`, `exchanges`, `size-guide`, `our-story`, `contact`, via the page container from SCRUM-207); this folder shows the intended layout of each.

| Screen | Desktop (1440) | Mobile (390) |
|---|---|---|
| Delivery and returns | [delivery-returns.html](delivery-returns.html) · [png](delivery-returns.png) | [delivery-returns-mobile.html](delivery-returns-mobile.html) · [png](delivery-returns-mobile.png) |
| Size guide | [size-guide.html](size-guide.html) · [png](size-guide.png) | n/a |
| Our story | [our-story.html](our-story.html) · [png](our-story.png) | n/a |
| Contact | [contact.html](contact.html) · [png](contact.png) | n/a |

Privacy and Terms are in [`../shop-pages/legal.html`](../shop-pages/legal.html). The HTML files are standalone and open in a browser (the shared header and footer are inlined); they load Manrope from Google Fonts for the preview only.

## What the design shows

- **Delivery and returns:** a help-topics menu beside the content (chips on mobile), a delivery facts table, three numbered return steps, a common-questions accordion and a "Still need help?" box.
- **Size guide:** the age, height, chest, waist and hip table (a sideways-scrolling region on narrow screens, with `<th scope>` headers) and a "how to measure" section. The size chart stays in scope (FR-08).
- **Our story:** a large editorial headline, a wide photo, "Why we make", three values and a black closing band with one marigold button.
- **Contact:** a form (name, email, topic, optional order number, message) beside a panel with email, WhatsApp and hours.

## Placeholders and open points

- Every policy value is a bracketed placeholder: delivery time and fee, return window, refund method, support email, WhatsApp number, hours, response time, and every measurement in the size table (`[cm]`) until merchandising supplies the chart.
- The FAQ answers and the founder story are placeholders. The three values and the line "every piece is inspected before it is packed and sent" are my wording; confirm them with the business.
- The contact form is not wired to anything and has no spam protection or confirmation state drawn.
