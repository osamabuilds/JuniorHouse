# 0029 — Product recommendations: a precomputed, blended similarity score with an optional meaning provider

- Status: Accepted (owner, 2026-11-08)
- Date: 2026-11-08

## Context

The product page is to show "You may also like" (similar products) and "Trending now" (recent best sellers) (spec: `docs/specs/storefront/product-recommendations`). The owner wants the result to be accurate, dynamic and, eventually, AI powered. The catalogue is a few hundred products. No module may read another module's tables (ADR 0002), and a shopper's page must never wait on a third party (NFR-FT-08).

## Decision

1. **Similar products are precomputed.** A background worker in the Storefront module scores candidates for each product and stores the best 12 per product in a Storefront read table. A page request reads those rows; it never scores anything.
2. **One blended score.** Candidates are first limited by hard rules (same gender and age group, published, not the product itself). Each remaining candidate gets `score = wFields × fields + wMeaning × meaning + wSales × sales`, all parts in 0..1.
   - `fields`: category match, colour overlap and price closeness, computed from the read model.
   - `meaning`: closeness of product text, from an `ITextSimilarityProvider` port (below).
   - `sales`: recent units sold (non-cancelled orders, 14 days), log-scaled and normalised within the group; it also breaks ties (REC-3).
   - Weights are configuration (default 0.50 / 0.35 / 0.15). When a part is unavailable (no meaning provider), its weight is shared out among the others in proportion, so the score is still 0..1 and the row is still useful (REC-10, REC-13).
3. **Meaning is a port, off by default.** `ITextSimilarityProvider` lives in Storefront.Application. Two implementations exist at first: `NoTextSimilarityProvider` (reports "unavailable"; the default in every environment) and a fake used by tests. A real provider (an embeddings service) is added later behind the same port with its key read from secrets; nothing else changes.
4. **Where vectors would be stored is deferred.** No vector store, no pgvector, no database image change now. When a provider is named, a follow-up ADR chooses between a vector column computed in the worker and the pgvector extension (it needs the `pgvector/pgvector:pg17` image in place of `postgres:17-alpine`), and records the cost control (embed a product only when its text changes).
5. **Units sold come from Orders through its contract**, a new query interface in `Romp.Modules.Orders.Contracts` that returns units per product per day for non-cancelled orders. Storefront pulls the last 14 days on a schedule into its own table. It does not use order events: they carry no lines (ADR 0011), and a cancellation changes past days.
6. **Sold-out and unpublished products drop out at read time**, by joining the precomputed ids to the live read model, so a stale precomputed row can never show a product that cannot be bought (REC-5).

## Consequences

- The first release has no third-party dependency, no new infrastructure and no cost per request, and it still gives an accurate row.
- Adding meaning later is a new class behind the port plus a configuration change.
- Recommendations can be up to one worker interval stale for what is *similar* (minutes), but never stale for what is *available*.
- Orders gains one small read-only contract; Storefront gains three tables.

## Alternatives rejected

- **Score at request time:** slower and puts the vendor in the page's critical path.
- **Feed units sold from order events:** the events deliberately carry no lines; adding them would widen what Orders publishes.
- **pgvector now:** infrastructure change with nothing to put in it until a provider exists.
