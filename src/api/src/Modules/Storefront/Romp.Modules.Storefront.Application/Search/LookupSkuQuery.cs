using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain;
using Romp.Modules.Storefront.Domain.Search;

#pragma warning disable CA1304, CA1311, CA1862 // ToLower() is translated to SQL lower(); the database decides, not the app culture.

namespace Romp.Modules.Storefront.Application.Search;

/// <summary>
/// Exact SKU or style-code lookup (FR-06, SRC-4, 5, 6, 14). A SKU opens its product with that colour and size selected; a style code opens
/// the product. Only products in the read model can be found, and the read model holds only published products, so an unpublished or
/// archived product cannot be reached this way (SRC-6). A sold-out product is still found, so its page can say so.
/// </summary>
public sealed record LookupSkuQuery(string? Code) : IRequest<SkuTargetDto?>;

/// <summary>Where a code leads. <c>Colour</c> and <c>Size</c> are URL-segment codes, set only for a SKU.</summary>
public sealed record SkuTargetDto(string Path, string? Colour, string? Size) : IPublicStorefrontResponse;

public sealed class LookupSkuQueryHandler(IStorefrontReadDbContext db) : IRequestHandler<LookupSkuQuery, SkuTargetDto?>
{
    public async Task<SkuTargetDto?> Handle(LookupSkuQuery request, CancellationToken cancellationToken)
    {
        if (SkuQuery.TryParse(request.Code) is not { } code)
        {
            return null;
        }

        // One indexed read: SKU_NORM carries a unique index (published-catalog migration).
        var bySku = await (
            from v in db.Variants.AsNoTracking()
            join p in db.Products.AsNoTracking() on v.ProductId equals p.Id
            where v.SkuNormalised == code
            select new { p.Path, v.ColourCode, v.SizeCode }).FirstOrDefaultAsync(cancellationToken);
        if (bySku is not null)
        {
            return new SkuTargetDto(bySku.Path, CodeSegment.Of(bySku.ColourCode), CodeSegment.Of(bySku.SizeCode));
        }

        var path = await db.Products.AsNoTracking()
            .Where(p => p.StyleCode.ToLower() == code)
            .OrderBy(p => p.Id)
            .Select(p => p.Path)
            .FirstOrDefaultAsync(cancellationToken);
        return path is null ? null : new SkuTargetDto(path, null, null);
    }
}
