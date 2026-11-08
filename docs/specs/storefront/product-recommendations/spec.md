# Product recommendations on the product page

- **Status:** Approved (owner, 2026-11-08)
- **Jira:** SCRUM-218 (owner request 2026-11-05). This picks up the backlog item D-5 / Q-PDP-5 ("cross-sell rail") from [product-pages](../product-pages/spec.md).
- **BRD sections:** none confirmed (owner request). Phase-3-style AI recommendations (NFR-03) were descoped with the Fit Finder; this is a smaller, owner-requested feature, not that one.
- **Requirement IDs:** none from the BRD. SEO-01, SEO-03, SEO-07, NFR-01, NFR-19 apply. Criterion IDs are prefixed `REC-`.

## Problem & intent
When a shopper opens a product, the page should keep them shopping: products that are genuinely similar to the one they are looking at, and what is selling well right now. The owner wants this to be **accurate and dynamic** and **AI powered**. Competitor research (2026-11-05): Outfitters shows a "You may also like" block of 9 same-department cards under the description (picture, name, fit tag, price, sale price and percent off, "Sale" badge, size dropdown, Add to Basket); Breakout has a "You May Also Like" heading too (its contents could not be read). Neither shows a "trending" block.

## Owner decisions (2026-11-05)
1. **Two rows** under the product details: **"You may also like"** (similar products) and **"Trending now"** (hot sellers).
2. **AI means embeddings:** each product is turned into a vector from its name, description, category and colours; similar products are the nearest vectors. Results are precomputed, never fetched while a page loads.
3. **Accuracy:** similar products stay in the same gender and age group; among equally similar items, those that sell better rank higher.
4. **Dynamic:** hot sellers come from real orders and are refreshed on a schedule; similar products are refreshed when a product is published, changed, goes out of stock or is unpublished.
5. **Quick add is needed:** each card has a size choice and an add-to-bag action, using the design system's `QuickAdd`.
6. Hot-seller window **14 days**; **4 + 4** cards in a row each (recommendations accepted).
7. **Blend, with the meaning part off at first (2026-11-08):** similarity is one blended score of field similarity (category, colours, price), meaning similarity (an embeddings provider) and recent sales. Release 1 ships with the meaning part switched off, so it runs on fields and sales alone; a provider can be added later behind the same port without changing the pages (ADR 0029). This still satisfies REC-13.

## User flow
1. A shopper opens a product page. Below the details they see "You may also like" (4 cards) and "Trending now" (4 cards).
2. A card shows the picture, name, price (and sale price/badge when discounted), sizes and a way to add a size to the bag without leaving the page.
3. Choosing a size and adding puts the item in the bag, exactly as on the product page.
4. A card links to its product page; the row is real links in the server-rendered HTML.
5. When there is nothing to show (a brand-new shop), the rows are simply absent, never empty headings `(new)`.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| REC-1 | Given a published product, when its page is opened, then "You may also like" shows up to 4 published, in-stock products, never the product itself | owner decisions 1, 6 |
| REC-2 | Given a girls toddler dress, when similar products are chosen, then they come from the same gender and age group | owner decision 3 |
| REC-3 | Given two products equally close in meaning, when ranking, then the one with more recent sales ranks higher | owner decision 3 |
| REC-4 | Given orders in the last 14 days, when "Trending now" is built, then it lists the top sellers by units sold in the viewer's age group, filling from the whole shop if that group has fewer than 4; excluded: unpublished, sold out, the current product and anything already in "You may also like" | owner decisions 1, 4, 6 |
| REC-5 | Given a product is published, changed, sold out or unpublished, when the next refresh runs, then recommendations stop showing it (an unpublished or sold-out product is removed at once, not at the next refresh) | owner decision 4 |
| REC-6 | Given a card, when it is shown, then it uses the design system's product card and `QuickAdd` (tokens and `rp-` classes only, no new design) | design-system rule |
| REC-7 | Given a card with several sizes, when the shopper picks one and presses add, then that variant is added to the bag with the server-side price and stock check, and the bag count updates | FR-10 (existing), SEC-09 |
| REC-8 | Given a card whose sizes are all sold out, then no add action is offered and the card says so | (new) |
| REC-9 | Given the product page is rendered on the server, then both rows and their links are in the returned HTML, with `<section>` landmarks, headings that do not skip a level, and descriptive link text | SEO-01, NFR-19 |
| REC-10 | Given the embeddings service is unavailable or has never run, then the page renders with "Trending now" only (or the rows absent) and no error, and the product page never waits on it | NFR-FT-08 (new) |
| REC-11 | Given the page, then recommendations add no more than 150 ms to its server time at p95 and cause no layout shift (cards reserve their size) | NFR-01, Lighthouse budgets |
| REC-12 | Given a recommendation card is clicked or a size added from it, then a tracked event records which row it came from (no personal data) so accuracy can be measured later | (new) |
| REC-13 | Given the AI key is missing, then the system falls back to category, price band and colour similarity so the row is still useful | (new) |
| REC-14 | Given keyboard-only use, then every card link, size choice and add action is reachable and operable, with visible focus | NFR-19 |

## Non-functional constraints
- SEO: both rows are server-rendered; no content appears only after hydration (SEO-01).
- Cost and privacy: only catalogue text goes to the embeddings provider, never customer or child data (NFR-27..30). The key is read from secrets, never committed.
- Speed: nothing calls the AI provider while a shopper's page is built.

## Out of scope
- "Bought together" from order history, per-shopper personalisation, "recently viewed" (later).
- Recommendations on the bag, order or category pages (later).
- Any change to the admin app.

## Open questions
- [x] **Q-REC-1 Embeddings provider and key (deferred by owner 2026-11-08; not needed for release 1).** The vendor is the owner's choice, and the key must come from secrets. **Recommendation:** plan it behind an interface with a fake provider first (as payments), so nothing waits for the key, and add the real provider when named.
- [x] **Q-REC-2 Hot-seller basis (owner answered 2026-11-08: units sold in non-cancelled orders over 14 days, no revenue weighting).** Finding (2026-11-08): no module holds units sold per product that Storefront may read, and Storefront must not query Orders tables. Orders publishes integration events (Orders.Contracts, OrderEvents.cs), so Storefront would keep its own daily units-sold read table, fed by those events (placed adds, cancelled subtracts). **Recommendation:** units sold in orders that were placed and not cancelled in the last 14 days (cancelled orders do not count). Alternative: also weight by revenue.
- [x] **Q-REC-3 Shared age group (resolved by code check, 2026-11-08).** The published-product read model already carries gender and age group names (GNDR_NAME, AGE_NAME) and the product page DTO has AgeMinMonths/AgeMaxMonths, so no new catalogue data is needed. "Same group" means the same gender and the same age group.
- [x] **Q-REC-4 Where the embeddings live (deferred 2026-11-08 with Q-REC-1; nothing to store in release 1; decided in a follow-up ADR when a provider is named).** A vector store (the pgvector extension in PostgreSQL 17) is a new infrastructure choice, so it needs an ADR in the plan. Finding (2026-11-08): docker-compose uses `postgres:17-alpine`, which does not include pgvector, so this means changing the database image (e.g. `pgvector/pgvector:pg17`) locally and on the VPS, then `CREATE EXTENSION`. **Recommendation:** pgvector inside the Storefront schema's own read tables, no new service. Alternative with no infrastructure change: store each vector as a column and compute nearest neighbours in the refresh job (a catalogue of a few hundred products is small enough), writing only the top 4 per product to a table; the page then never does vector maths. Choose before the ADR.

## As built
(filled in when implemented)
