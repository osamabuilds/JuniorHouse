# Tasks: pages-redesign

Test first for every task: write the failing test, then make it pass. Backend tests run in the Linux SDK container (see the docker-dotnet-test note); web tests run natively (`cd src/web; npx ng test storefront --watch=false --include=...`). Playwright runs in CI.

- [x] 1. Token `--sale` (#a8321f) in `_tokens.scss`; contrast test for white on `--sale` and `--sale` on the three light surfaces (proves PR-6; `tokens.spec.ts`)
- [x] 2. API: `ListingTileSizeDto` and `Sizes` on `ListingTileDto`, filled by one batch query per page; sold out when no sellable variant of that size has stock (proves PR-8; `SearchListingTests`: sizes in order, size sold out only when every colour is out, one query regardless of tile count)
- [x] 3. API: `SavingAmount` (decimal, server-computed) on `ListingTileDto` and on the product page price DTO (proves PR-2; `SearchListingTests`, product page query test)
- [x] 4. PR-4 on the product page: when the whole product is sold out the discount treatment is not shown and the sold-out state stays (listings never list a product with nothing in stock, LST-15) (proves PR-4; `product-page.container.spec.ts`)
- [x] 5. Styles from `bundle.css` into `_components.scss`: `rp-tag--sale`, `rp-price--sale` (on `--sale`), `rp-price--lg`, `rp-save`, `rp-off`, `rp-avail`, `rp-notfound*` (proves PR-1, PR-7; `style-rules.spec.ts` no raw colours)
- [x] 6. Web models: `ListingTile.sizes` and `savingAmount`; `ProductPrice.savingAmount`; contract fixtures updated (proves PR-2, PR-8)
- [x] 7. `product-tile`: sale badge `rp-tag--sale` with minus sign, `Now`/`Was` text, "Save Rs. X", size line with struck sold-out sizes and "sold out" in the name, no add-to-bag control (proves PR-1, 3, 7, 8, 9; `product-tile` spec)
- [x] 8. `product-price`: `rp-price--lg`, `rp-off`, "You save Rs. X", "From" kept (proves PR-1, 2, 3, 5; `product-price` spec)
- [x] 9. 404 / 503 / removed page layout in `page.container` with decorative `404`, links, `noindex` (proves PR-10, 12; `page.container.spec.ts`)
- [x] 10. 410 state in the product page container: "This piece has left the collection" and shop link; 301 unchanged (proves PR-11; `product-page.container.spec.ts`)
- [x] 11. Content page layout: eyebrow, `rp-sidenav`, `rp-prose`; blocks as `<dl>`, callout, `<table>` with `<th scope>`, lists (no accordion: the settings have no accordion block, see As built) (proves PR-13, 14, 15; `page-block` and `page.container` specs)
- [x] 12. Playwright: axe, no horizontal scroll and 44px targets at 390/768/1280 on a sale listing, sale and non-sale product, 404, 410 and a help page (proves PR-16; `e2e/storefront/pages-redesign.e2e.spec.ts`; 12 passed against the Docker stack 2026-10-25, also runs in CI)
- [x] 13. Full web unit suite, `ng build storefront`, Storefront backend tests green; diff touches only the storefront project and the listing tile (proves PR-17)
- [ ] 14. Lighthouse budgets unchanged on home, category, product (proves PR-18; CI)
- [x] 15. Close the loop: spec "As built", traceability, README status, memory note
