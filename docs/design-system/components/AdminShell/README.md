The back-office frame. It replaces the single wrapping row of nine links with a **sidebar grouped by the supply-chain flow**: Setup (Reference data, Styles, Catalog), Sourcing (Vendors, Purchase orders), Making (Production, Receiving and QC), Fulfilment (Labelling, Warehouse, with the stock-alert count). Same routes as today, no new pages.

- Sidebar: `surface-dark` (espresso), 248px, sticky on 64rem and up; links 40px high, `nav` text, active link has a 2px clay edge and a lighter ground. Brand is the lowercase "romp." wordmark with an "Admin" tag.
- Phones: a 56px top bar (brand, menu button) and the same nav in a left drawer (native `<dialog>`, focus trapped, Esc closes). Markup order: header, nav, main, footer; `aria-current="page"` on the active link.
- Content: `ad-main`, max 76rem, padding 16 / 40px. Footer shows the outbox health badge. Keep `<meta name="robots" content="noindex, nofollow">`.
