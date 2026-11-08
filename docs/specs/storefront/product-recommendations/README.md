# Product recommendations

"You may also like" (AI similarity) and "Trending now" (recent best sellers) rows on the product page, with quick add.

**Status:** Implemented 2026-11-09, awaiting merge (approved 2026-11-08, SCRUM-218) · **Spec:** [spec.md](spec.md) · **Plan:** [plan.md](plan.md) · **Tasks:** [tasks.md](tasks.md) · **Requirements:** owner request (no BRD ID); picks up backlog D-5 · **Code / tests:** `Storefront.Domain/ProductRecommendations`, `Storefront.Application/ProductRecommendations`, `Storefront.Infrastructure/ProductRecommendations`, `Orders.Contracts/IProductSalesQueries`, web `product-pages` (`tests/Romp.Modules.Storefront.Tests/ProductRecommendations`, `Romp.Api.IntegrationTests/Storefront/RecommendationEndpointsTests`, `e2e/journeys/storefront-recommendations`) · **Module:** [storefront](../README.md)
