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

        var projected = products.Select(p => new
        {
            p.Id,
            p.Path,
            p.Name,
            p.PublishedAt,
            From = matching.Where(v => v.ProductId == p.Id).Min(v => v.Price),
            CompareAt = matching.Where(v => v.ProductId == p.Id && v.CompareAtPrice > v.Price).OrderBy(v => v.Price).Select(v => v.CompareAtPrice).FirstOrDefault(),
            Colours = matching.Where(v => v.ProductId == p.Id).Select(v => v.ColourCode).Distinct().Count(),
            SoldOut = db.Variants.Any(v => v.ProductId == p.Id && v.IsSellable && v.AvailableQuantity <= 0),
            p.MaxDiscountPercent,
        });

        // Every sort ends in the unique id, so pages never repeat or skip a product.
        var ordered = filter.Sort switch
        {
            ListingSort.PriceAscending => projected.OrderBy(x => x.From).ThenBy(x => x.Id),
            ListingSort.PriceDescending => projected.OrderByDescending(x => x.From).ThenBy(x => x.Id),
            _ => projected.OrderByDescending(x => x.PublishedAt).ThenByDescending(x => x.Id),
        };

        var page = await ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(cancellationToken);
        var pageIds = page.Select(x => x.Id).ToList();
        var images = await ImagesAsync(pageIds, cancellationToken);
        var sizes = await SizesAsync(pageIds, cancellationToken);

        var tiles = page.Select(x => new ListingTileDto(
            x.Path,
            x.Name,
            x.From,
            x.CompareAt,
            x.CompareAt is > 0 && x.From is { } now && x.CompareAt > now ? (int)Math.Round((x.CompareAt.Value - now) / x.CompareAt.Value * 100m) : 0,
            x.CompareAt is > 0 && x.From is { } was && x.CompareAt > was ? x.CompareAt.Value - was : 0m,
            x.Colours,
            x.SoldOut,
            sizes.GetValueOrDefault(x.Id) ?? [],
            images.GetValueOrDefault(x.Id))).ToList();

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

    /// <summary>The main image of each product with the derivative files that exist: two queries however many products.</summary>
    /// <summary>Every size of each product on the page, in size order, in one query. A size is sold out when no sellable variant of it has stock.</summary>
    private async Task<Dictionary<long, IReadOnlyList<ListingTileSizeDto>>> SizesAsync(List<long> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        var variants = await db.Variants.AsNoTracking()
            .Where(v => productIds.Contains(v.ProductId) && v.IsSellable)
            .Select(v => new { v.ProductId, v.SizeCode, v.SizeName, v.SizeSeq, InStock = v.AvailableQuantity == null || v.AvailableQuantity > 0 })
            .ToListAsync(cancellationToken);

        return variants.GroupBy(v => v.ProductId).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<ListingTileSizeDto>)[.. g.GroupBy(v => new { v.SizeCode, v.SizeName, v.SizeSeq })
                .OrderBy(s => s.Key.SizeSeq).ThenBy(s => s.Key.SizeCode, StringComparer.Ordinal)
                .Select(s => new ListingTileSizeDto(s.Key.SizeCode, s.Key.SizeName, !s.Any(v => v.InStock)))]);
    }

    private async Task<Dictionary<long, ListingImageDto>> ImagesAsync(List<long> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        var mains = await db.Images.AsNoTracking().Where(i => productIds.Contains(i.ProductId) && i.IsMain).ToListAsync(cancellationToken);
        var imageIds = mains.Select(i => i.Id).ToList();
        var files = await db.Derivatives.AsNoTracking()
            .Where(d => imageIds.Contains(d.ImageId) && d.Status == DerivativeStatus.Done && d.FileKey != null)
            .ToListAsync(cancellationToken);

        return mains.ToDictionary(
            i => i.ProductId,
            i => new ListingImageDto(
                i.AltText,
                i.Width,
                i.Height,
                files.Where(d => d.ImageId == i.Id).GroupBy(d => d.Width).OrderBy(g => g.Key)
                    .Select(g => new ListingImageSourceDto(
                        g.Key,
                        "/media/" + (g.FirstOrDefault(d => d.Format == DerivativeFormat.Webp)?.FileKey ?? string.Empty),
                        g.FirstOrDefault(d => d.Format == DerivativeFormat.Jpeg) is { } jpeg ? "/media/" + jpeg.FileKey : null))
                    .Where(s => s.Webp != "/media/")
                    .ToList()));
    }
}
