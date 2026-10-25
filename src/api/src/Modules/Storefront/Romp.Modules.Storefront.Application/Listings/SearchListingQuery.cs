using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Storefront.Application.Listings;

/// <summary>One page of a listing: the scope's products narrowed by the filter, sorted and paged in SQL (LST-1..4, 7..9, 15, 16).</summary>
public sealed record SearchListingQuery(ListingScope Scope, ListingFilter Filter) : IRequest<ListingResult<ListingPageDto>>;

public sealed class SearchListingQueryHandler(
    IStorefrontReadDbContext db,
    ICatalogLookupQueries lookups,
    ICatalogLookupCache cache,
    IListingSettings settings,
    TimeProvider clock) : IRequestHandler<SearchListingQuery, ListingResult<ListingPageDto>>
{
    public async Task<ListingResult<ListingPageDto>> Handle(SearchListingQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        var rows = await cache.GetOrLoadAsync(ct => CatalogLookups.LoadAsync(lookups, ct), cancellationToken);
        if (ResolvedScope.Resolve(request.Scope, rows) is not { } scope)
        {
            return ListingResult.Failed<ListingPageDto>(ListingOutcome.ScopeNotFound);
        }

        // A page number whose offset cannot be a row is past the end without asking the database.
        if ((long)(filter.Page - 1) * filter.PageSize > int.MaxValue)
        {
            return ListingResult.Failed<ListingPageDto>(ListingOutcome.PastTheEnd);
        }

        var cutoff = clock.GetUtcNow().AddDays(-settings.NewArrivalsDays);
        var matching = ListingQueryBuilder.MatchingVariants(db, filter);
        var products = ListingQueryBuilder.Matching(ListingQueryBuilder.InScope(db, scope, cutoff), matching, filter);

        var total = await products.CountAsync(cancellationToken);
        if (ListingRules.IsPastTheEnd(filter.Page, filter.PageSize, total))
        {
            return ListingResult.Failed<ListingPageDto>(ListingOutcome.PastTheEnd);
        }

        var projected = ListingTiles.Project(db, products, matching);

        // Every sort ends in the unique id, so pages never repeat or skip a product.
        var ordered = filter.Sort switch
        {
            ListingSort.PriceAscending => projected.OrderBy(x => x.From).ThenBy(x => x.Id),
            ListingSort.PriceDescending => projected.OrderByDescending(x => x.From).ThenBy(x => x.Id),
            _ => projected.OrderByDescending(x => x.PublishedAt).ThenByDescending(x => x.Id),
        };

        var page = await ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(cancellationToken);
        var tiles = await ListingTiles.BuildAsync(db, page, cancellationToken);

        var maxSaving = tiles.Count == 0 ? 0 : tiles.Max(t => t.SavingPercent);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)filter.PageSize));
        return ListingResult.Of(new ListingPageDto(
            scope.Title,
            scope.Scope.Kind.ToString(),
            scope.Gender?.Name,
            scope.Category?.Name,
            Breadcrumbs(scope),
            tiles,
            total,
            filter.Page,
            filter.PageSize,
            totalPages,
            maxSaving));
    }

    private static List<BreadcrumbDto> Breadcrumbs(ResolvedScope s)
    {
        var gender = s.Gender is null ? null : new MenuAudienceRef(s.Gender.Code, s.Gender.Name);
        var trail = s.Scope.Kind switch
        {
            ListingScopeKind.Audience => BreadcrumbTrail.For(gender, null, null),
            ListingScopeKind.Category => BreadcrumbTrail.For(gender, new MenuCategoryRef(s.Category!.Code, s.Category.Name), null),
            ListingScopeKind.NewIn or ListingScopeKind.Sale => (IReadOnlyList<Crumb>)[new Crumb(BreadcrumbTrail.HomeLabel, "/"), new Crumb(s.Gender!.Name, "/" + CodeSegment.Of(s.Gender.Code)), new Crumb(s.Title, null)],
            _ => BreadcrumbTrail.ForPage(s.Title),
        };
        return trail.Select(c => new BreadcrumbDto(c.Label, c.Path)).ToList();
    }
}
