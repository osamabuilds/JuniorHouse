# Sold items in listings

Sold-out products stay on every listing (audience, category, season, New in, Sale), mixed in with in-stock ones and marked as sold out, with an "In stock only" filter.

**Status:** Implemented (spec, plan and tasks approved by the owner 2026-11-06; built 2026-11-06) · **Spec:** [spec.md](spec.md) · **Plan:** [plan.md](plan.md) · **Tasks:** [tasks.md](tasks.md) · **Requirements:** FR-05, SEO-03, SEO-06; amends listings LST-15 · **Code / tests:** `src/api/src/Modules/Storefront/` (Listings, Search, Navigation, CollectionsPage), `src/api/src/Romp.Api/Seeding/DemoStorefrontSeeder.cs`, `src/web/projects/storefront/src/app/features/storefront/listings/` · **Built in:** listings, search, navigation · **Module:** [storefront](../README.md)

API changes land in the existing `Listings` feature (no new feature folder).
