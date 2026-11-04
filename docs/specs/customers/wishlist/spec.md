# Wishlist

- **Status:** Built (2026-11-04), see "As built"
- **Jira:** SCRUM-37 (FR-17) under epic SCRUM-6; SCRUM-125 (SEC-05)
- **BRD sections:** §5.3, §6.2 (TC-PDP-07), §6.4 (TC-ACC-15, 16), FEAT-ACC-08
- **Requirement IDs:** FR-17, SEC-05, FR-10 (display only)

## Problem & intent
A parent browsing for a child wants to save products to come back to. Guests are asked to sign in, and what they tapped must not be lost along the way. The wishlist also tells them when a saved item sold out or went on sale.

## Requirements (quoted from the BRD)
> **FR-17:** Registered users shall be able to add/remove products to a Wishlist; guest users attempting to wishlist shall be prompted to log in/register, with the item preserved through that flow.

> **§5.3 Wishlist.** From PDP or listing tile, user clicks the wishlist/heart icon (requires login; guest is prompted to log in or create an account, with the item held in a temporary session wishlist until then). User views Wishlist in account → can move an item to cart, remove it, or (Phase 2) share the wishlist as a gift registry link. If a wishlisted item goes out of stock or on sale, the system flags this in the Wishlist view.

## User flow
1. Heart on a product page and on listing tiles. Signed in: toggles. Guest: a prompt to sign in or register; the product is held in the browser meanwhile.
2. After sign-in or registration the held item is added to the account wishlist and the customer lands where they were.
3. Wishlist page (design `WishlistPage`): items with price, a "Sale" or "Price dropped" note, "Sold out" or "Low stock" where it applies, **Remove** and **Move to bag**.
4. **Move to bag** needs the cart (S11). This sprint ships the control disabled behind a flag, or links to the product page `(Q-WSL-1)`.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| WSL-1 | Given a signed-in customer, when they tap the heart on a product, then it is added; tapping again removes it; the heart shows the saved state on page load (server-rendered for signed-in users). | FR-17 |
| WSL-2 | Given a guest, when they tap the heart, then they are prompted to sign in or register and the product is remembered in the browser. | TC-PDP-07 |
| WSL-3 | Given a remembered guest item, when the guest signs in or registers, then it is added to the account wishlist exactly once and the browser copy is cleared. | TC-PDP-07, FR-17 |
| WSL-4 | Given a product already on the wishlist, when it is added again, then there is still one entry. | (new) |
| WSL-5 | Given the wishlist page, then each item shows the current price and availability from the storefront read model, and a product that was unpublished is shown as unavailable and can be removed. | TC-ACC-15 |
| WSL-6 | Given a product whose price is lower now than when it was saved, then the item is flagged with the price change; a product that went sold out is flagged. | TC-ACC-16, §5.3 |
| WSL-7 | Given another customer's wishlist item id, when removing it, then the response is 404 and nothing changes. | SEC-05 |
| WSL-8 | Given the list endpoint, then it is paged and reads prices without per-item queries. | CONVENTIONS |
| WSL-9 | Given the wishlist page and heart controls, then they are buttons with accessible names stating the product and state, the page is `noindex`, and product links are plain anchors. | NFR-19, SEO-05 |

## Non-functional constraints
The product and listing pages stay cacheable for anonymous visitors: the saved state is filled in for signed-in users without breaking the shared edge cache (plan decision).

## Out of scope
Shareable registry (P2, FEAT-ACC-11); wishlist per child; price-drop emails; guest wishlist stored on the server.

## Open questions
- [ ] **Q-WSL-1 Move to bag.** Cart arrives in S11. Default: show the action as a link to the product page this sprint and wire it to the cart in S11.
- [ ] **Q-WSL-2 What is saved.** A product, or a product plus the chosen colour/size? The BRD says "products". Default: the product (colour optional) and the price at save time, for the price-change flag.
- [ ] **Q-WSL-3 Edge cache.** Default: the heart state is fetched client-side after load for signed-in customers so cached HTML stays shared.

## As built (2026-11-04)

What was built matches the criteria above, with these differences from the plan and the draft:

- **Query shapes.** `GET /api/customers/me/wishlist` returns for each item the product name, path, current price, struck-through price, the price saved, availability, picture URLs (one bulk call to the storefront image contract) and `notes`: any of `PriceDropped`, `OnSale`, `SoldOut`, `Unavailable`. A product that is no longer published keeps its row with `Unavailable` and no name or price, so it can be removed (WSL-5). `GET .../ids` returns the product ids (at most the list limit) for the hearts.
- **Storefront contract.** `IStorefrontCatalogQueries.GetProductSummariesAsync` returns sold-out products (with `IsAvailable` false) and leaves out only products that are not published. Listing tiles and the product page now carry `productId` so the heart knows which product it saves.
- **Add is not `ON CONFLICT`.** The add handler takes the customer's row lock, then checks and adds; the unique index `UQ_CUST_WSHL_CUST_ID_PRDT_ID` is the backstop. Five simultaneous adds leave one row (`WishlistCommandTests`). Add and remove answer 204; removing a product that is not on the caller's list is 404 (WSL-7).
- **Guest merge.** `POST .../merge` takes up to 50 ids, skips unknown, unpublished and already saved ones, never goes past the list limit, and answers `{ added }`. The storefront runs it once, after the session is known to be signed in, and clears the browser copy only when it succeeded (WSL-3). There is still no server-side sign-in hook (sign-in lives in the storefront), so the plan's `WishlistSignedInHook` was not built.
- **Signed-out wishlist page.** The API lists signed-in customers only, so a signed-out `/wishlist` shows how many products this device remembers and a sign-in link, not the products themselves (the design's "Saved on this device" grid would need a public product-summary endpoint; not built).
- **Q-WSL-1** Move to bag is a **Choose size** link to the product page. **Q-WSL-2** the product and the price at save time are stored. **Q-WSL-3** the hearts are drawn off on the server and filled in after the ids are read in the browser, so cached pages stay shared.
- **Header** has a heart link to `/wishlist` (hidden on small screens, as in the design; the wishlist is reachable from the account on a phone). It carries the count in its accessible name.
- **Not done:** `robots.txt` does not exist yet (the seo feature is deferred), so `/wishlist` is blocked only by `noindex` and `Cache-Control: private, no-store`.
