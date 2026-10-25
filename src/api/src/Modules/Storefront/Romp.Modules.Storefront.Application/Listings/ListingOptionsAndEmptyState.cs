using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Application.Listings;

/// <summary>The filter choices that exist in a scope. Sizes and colours are those with stock; ages are those with a listable product (LST-1, 2, 16).</summary>
public sealed record GetListingOptionsQuery(ListingScope Scope) : IRequest<ListingResult<ListingOptionsDto>>;

public sealed class GetListingOptionsQueryHandler(
    IStorefrontReadDbContext db,
    ICatalogLookupQueries lookups,
    ICatalogLookupCache cache,
    IListingSettings settings,
    TimeProvider clock) : IRequestHandler<GetListingOptionsQuery, ListingResult<ListingOptionsDto>>
{
    public async Task<ListingResult<ListingOptionsDto>> Handle(GetListingOptionsQuery request, CancellationToken cancellationToken)
    {
        var rows = await cache.GetOrLoadAsync(ct => CatalogLookups.LoadAsync(lookups, ct), cancellationToken);
        if (ResolvedScope.Resolve(request.Scope, rows) is not { } scope)
        {
            return ListingResult.Failed<ListingOptionsDto>(ListingOutcome.ScopeNotFound);
        }

        var products = ListingQueryBuilder.InScope(db, scope, clock.GetUtcNow().AddDays(-settings.NewArrivalsDays));
        var variants = ListingQueryBuilder.MatchingVariants(db, new ListingFilter()).Where(v => products.Any(p => p.Id == v.ProductId));

        return ListingResult.Of(await ListingOptionsBuilder.BuildAsync(variants, products, rows, cancellationToken));
    }
}

/// <summary>The filter choices present in a set of products and their in-stock variants. Shared by listings and search.</summary>
internal static class ListingOptionsBuilder
{
    public static async Task<ListingOptionsDto> BuildAsync(
        IQueryable<VariantRow> variants, IQueryable<PublishedProduct> products, CatalogLookups rows, CancellationToken cancellationToken)
    {
        // Three queries however many values there are: sizes, colours with the price range, ages.
        var sizes = await variants.Select(v => new { v.SizeCode, v.SizeName, v.SizeSeq }).Distinct().ToListAsync(cancellationToken);
        var colours = await variants.Select(v => new { v.ColourCode, v.ColourName }).Distinct().ToListAsync(cancellationToken);
        var range = await variants.GroupBy(_ => 1).Select(g => new { Min = g.Min(v => v.Price), Max = g.Max(v => v.Price) }).FirstOrDefaultAsync(cancellationToken);
        var ageCodes = await products.Select(p => p.AgeCode).Distinct().ToListAsync(cancellationToken);

        var ages = rows.Ages.Where(a => a.IsActive && ageCodes.Contains(a.Code)).OrderBy(a => a.SortSeq).ThenBy(a => a.Code, StringComparer.Ordinal)
            .Select(a => new ListingOptionDto(CodeSegment.Of(a.Code), a.Name)).ToList();

        return new ListingOptionsDto(
            [.. sizes.OrderBy(s => s.SizeSeq).ThenBy(s => s.SizeCode, StringComparer.Ordinal).Select(s => new ListingOptionDto(CodeSegment.Of(s.SizeCode), s.SizeName))],
            [.. colours.OrderBy(c => c.ColourName, StringComparer.Ordinal).Select(c => new ListingOptionDto(CodeSegment.Of(c.ColourCode), c.ColourName))],
            range?.Min,
            range?.Max,
            ages);
    }
}

/// <summary>Links for an empty listing (LST-5, 6): other categories of the audience, or the audiences themselves, and New In.</summary>
public sealed record GetEmptyStateQuery(ListingScope Scope) : IRequest<ListingResult<EmptyStateDto>>;

public sealed class GetEmptyStateQueryHandler(
    IStorefrontReadDbContext db,
    ICatalogLookupQueries lookups,
    ICatalogLookupCache cache) : IRequestHandler<GetEmptyStateQuery, ListingResult<EmptyStateDto>>
{
    private const int MaxSuggestions = 8;

    public async Task<ListingResult<EmptyStateDto>> Handle(GetEmptyStateQuery request, CancellationToken cancellationToken)
    {
        var rows = await cache.GetOrLoadAsync(ct => CatalogLookups.LoadAsync(lookups, ct), cancellationToken);
        if (ResolvedScope.Resolve(request.Scope, rows) is not { } scope)
        {
            return ListingResult.Failed<EmptyStateDto>(ListingOutcome.ScopeNotFound);
        }

        var suggestions = new List<SuggestionDto> { new("New in", scope.Gender is null ? "/new-in" : $"/{CodeSegment.Of(scope.Gender.Code)}/new-in") };

        var listable = db.Products.AsNoTracking().Where(p => p.IsListable);
        if (scope.Gender is { } gender)
        {
            var genderCode = gender.Code;
            var categoryCodes = await listable.Where(p => p.GenderCode == genderCode).Select(p => p.CategoryCode).Distinct().ToListAsync(cancellationToken);
            suggestions.AddRange(rows.Categories
                .Where(c => c.IsActive && categoryCodes.Contains(c.Code) && c.Code != scope.Category?.Code)
                .OrderBy(c => c.SortSeq).ThenBy(c => c.Code, StringComparer.Ordinal)
                .Select(c => new SuggestionDto(c.Name, $"/{CodeSegment.Of(genderCode)}/{CodeSegment.Of(c.Code)}")));
        }
        else
        {
            var genderCodes = await listable.Select(p => p.GenderCode).Distinct().ToListAsync(cancellationToken);
            suggestions.AddRange(rows.Genders
                .Where(g => g.IsActive && genderCodes.Contains(g.Code))
                .OrderBy(g => g.SortSeq).ThenBy(g => g.Code, StringComparer.Ordinal)
                .Select(g => new SuggestionDto(g.Name, "/" + CodeSegment.Of(g.Code))));
        }

        return ListingResult.Of(new EmptyStateDto([.. suggestions.Take(MaxSuggestions)]));
    }
}
