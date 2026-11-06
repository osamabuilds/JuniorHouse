# Sold items in listings: tasks

Small, ordered, test-first. Spec and plan approved 2026-11-06. Start at task 1.

## API

1. [ ] `ProductVisibility`: listable means published with the picture ready, in stock is its own flag. *Test: `ProductVisibilityTests` (SLD-1).*
2. [ ] `ListingQueryBuilder.InScope` drops the stock condition; sort puts in-stock first. *Test: `SoldItemsOrderTests` (SLD-1, SLD-2).*
3. [ ] `ListingFilter.InStockOnly` (`stock=in`) parsed, validated, canonical query. *Test: `ListingFilterTests` (SLD-4).*
4. [ ] `MatchingVariants`: any variant unless `InStockOnly`. *Test: `SoldItemsFilterTests` (SLD-8).*
5. [ ] Tile `SoldOut`; no quantity in any DTO. *Test: `ListingTileSoldOutTests` (SLD-5, PCT-3).*
6. [ ] Counts, page count and option counts include sold items. *Test: `SoldItemsCountTests` (SLD-3).*
7. [ ] Empty state only when nothing is in scope. *Test: update `SearchListingTests` LST-5 cases (SLD-9).*
8. [ ] Search includes sold items after in-stock ones; suggestions unchanged. *Test: `SearchSoldItemsTests` (SLD-7).*
9. [ ] Scope `CollectionCategory` and its resolver. *Test: `CollectionCategoryScopeTests` (SLD-10).*
10. [ ] Home, menu tile and menu flags keep in-stock only. *Test: `MenuTileTests`, `GetHomeQueryTests` with a sold-out newest product (SLD-11).*
11. [ ] Collections index counts sold pieces. *Test: `GetCollectionsQueryTests` (SLD-12).*
12. [ ] Query-count test with a sold-out mix. *Test: `ListingPerformanceTests` (NFR).*
13. [ ] Rebuild brings hidden products back. *Test: `RebuildSoldItemsTests` (SLD-13).*
14. [ ] Endpoint tests for `stock=in` and the new scope, hostile input. *Test: `StorefrontPublicEndpointsTests` (CI/Docker).*

## Web

15. [ ] Product card: sold-out state (label, dim, no quick add, accessible name). *Test: card component spec (SLD-5).*
16. [ ] Filter bar "In stock only" switch, in the URL. *Test: filter bar spec and Playwright (SLD-4).*
17. [ ] Route `collections/:collection/:category`, breadcrumb, SEO. *Test: container spec (SLD-10).*
18. [ ] Product page structured data availability. *Test: seo builder spec (SLD-6).*
19. [ ] axe and 44px target check on a listing with sold-out cards. *Test: Playwright a11y (NFR-19).*

## Data and navigation

20. [ ] Development demo seeder: winter and summer ranges with sold-out products and placeholder pictures; idempotent. *Test: `DemoStorefrontSeederTests` (SLD-14).*
21. [ ] Launch navigation set: Season > categories with our own labels. *Test: navigation spec (SLD-10).*

## Close the loop

22. [ ] Update listings LST-15, storefront-settings ST-6 and S-4 "As built"; feature README card and the module index. *Proof: `StructureSyncTests`.*
