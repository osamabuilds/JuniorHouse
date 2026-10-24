# Customer account area: design reference

Static design reference for the signed-in customer area. **Design only.** Customer accounts are not specified yet (`docs/specs/identity` covers staff only and places customer accounts in Sprint 10). Sign-in, sign-up and password reset are in [`../customer-account/`](../customer-account/README.md).

| Screen | Desktop (1440) | Mobile (390) |
|---|---|---|
| Account overview | [account-home.html](account-home.html) · [png](account-home.png) | [account-mobile.html](account-mobile.html) · [png](account-mobile.png) |
| Orders | [orders.html](orders.html) · [png](orders.png) | n/a |
| Order detail and tracking | [order-detail.html](order-detail.html) · [png](order-detail.png) | n/a |
| Return or exchange request | [returns.html](returns.html) · [png](returns.png) | n/a |
| Addresses | [addresses.html](addresses.html) · [png](addresses.png) | n/a |
| Child profiles | [child-profiles.html](child-profiles.html) · [png](child-profiles.png) | [child-profiles-mobile.html](child-profiles-mobile.html) · [png](child-profiles-mobile.png) |
| Add or edit a child (dialog) | [child-profile-form.html](child-profile-form.html) · [png](child-profile-form.png) | n/a |

The HTML files are standalone and open in a browser (the shared header and footer are inlined). They load Manrope from Google Fonts for the preview only.

## What the design shows

- **Layout:** a left account menu on desktop (Overview, Orders, Addresses, Child profiles, Wishlist, Sign out); on mobile the overview is the menu, as large rows.
- **Orders:** a table with status tags (Shipped, Delivered, Returned) and a View link; the order detail adds a four-step timeline (Placed, Packed, Shipped, Delivered), courier and tracking number, items, address, payment and totals, and a "Return or exchange" button.
- **Return or exchange:** choose items, give a reason, and pick an exchange for a different size or a refund. A side panel restates the returns policy.
- **Addresses:** cards with a Default tag, Edit, Make default and Remove.
- **Child profiles:** basic profiles only (FR-18 create, edit, delete). Name, birth month and year, and an optional usual size. The pages state that only the owner can see them and that deleting a profile keeps past orders (FR-21).

## Rules the design must respect

- **Every account endpoint checks that the resource belongs to the caller** (SEC-05), most of all for Child Profile data.
- **Child data is personal data about minors:** owner-only, and exportable or deletable on request (NFR-27, NFR-29). The BRD's export route is not drawn.
- **Never log full Child Profile records** (SEC-25).
- **robots.txt blocks account pages** (SEO-05); these pages carry `noindex`.

## Placeholders and open points

- Names, dates, order numbers, tracking numbers and couriers are placeholders. Return window, refund method and timing are placeholders.
- The profile fields (birth month and year, usual size) are my proposal; the BRD wording for FR-18 should be checked before building.
- Not drawn: edit-details page, delete confirmation, export-my-data, the empty states (no orders, no addresses, no children), and mobile versions of orders, order detail, returns and addresses.
