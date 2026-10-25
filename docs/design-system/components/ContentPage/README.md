The layout for every page the footer links to: Delivery, Exchanges, Size guide, Made in Pakistan, Contact, Privacy and Terms. The pages are **content from the settings** (blocks: heading, paragraph, list, table, callout and fact), so the layout is fixed and the words are data. Every fact (delivery time and fee, exchange window, WhatsApp, hours) comes from a settings record and is never written into a template.

- **Layout.** Breadcrumb, an eyebrow ("Help"), `display-lg` title, then a left Help menu beside the article (`rp-sidenav`; on a phone it moves above the article). The article is `rp-prose`, 44rem wide: `display` headings, 17px body.
- **Blocks.** `rp-fact` rows (term and value), the `rp-callout`, a `rp-table` with `<th scope>` (the size chart, one row per configured age), ordered and unordered lists, and `rp-acc` accordions for questions.
- **Size guide.** The table is horizontally scrollable on a phone; measurements show `[cm]` until the business enters them (FR-08).

Rules: one `<h1>`, no skipped heading, a unique title and description, a self-referencing canonical and `BreadcrumbList` JSON-LD in the server HTML (SC-13); a draft page is never linked or served; legal text (Privacy, Terms) is supplied and reviewed by the owner before publishing.
