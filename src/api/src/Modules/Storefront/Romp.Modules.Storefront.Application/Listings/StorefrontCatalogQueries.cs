using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Contracts;

namespace Romp.Modules.Storefront.Application.Listings;

/// <summary>The Storefront side of <see cref="IStorefrontCatalogQueries"/>: three queries however many products are asked for (products, sale prices, main images).</summary>
public sealed class StorefrontCatalogQueries(IStorefrontReadDbContext db) : IStorefrontCatalogQueries
{
    public async Task<IReadOnlyList<ProductSummary>> GetProductSummariesAsync(IReadOnlyCollection<long> productIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        if (productIds.Count > IStorefrontCatalogQueries.MaxIds)
        {
            throw new ArgumentException($"At most {IStorefrontCatalogQueries.MaxIds} products can be asked for at once.", nameof(productIds));
        }

        var ids = productIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var products = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.MinPrice != null)
            .Select(p => new { p.Id, p.Name, p.Path, MinPrice = p.MinPrice!.Value, p.IsAvailable, p.IsOnSale })
            .ToListAsync(cancellationToken);
        if (products.Count == 0)
        {
            return [];
        }

        var found = products.Select(p => p.Id).ToList();
        var onSale = products.Where(p => p.IsOnSale).Select(p => p.Id).ToList();

        var compare = onSale.Count == 0
            ? []
            : await db.Variants.AsNoTracking()
                .Where(v => onSale.Contains(v.ProductId) && v.IsSellable && v.CompareAtPrice != null && v.Price != null && v.CompareAtPrice > v.Price)
                .Select(v => new { v.ProductId, v.Price, CompareAt = v.CompareAtPrice!.Value })
                .ToListAsync(cancellationToken);
        var images = await db.Images.AsNoTracking()
            .Where(i => found.Contains(i.ProductId) && i.IsMain)
            .Select(i => new { i.ProductId, i.Id })
            .ToListAsync(cancellationToken);

        return products.Select(p =>
        {
            var was = compare.Where(v => v.ProductId == p.Id && v.Price == p.MinPrice).Select(v => (decimal?)v.CompareAt).Min();
            var image = images.Where(i => i.ProductId == p.Id).Select(i => (long?)i.Id).Min();
            return new ProductSummary(p.Id, p.Name, p.Path, p.MinPrice, was, p.IsAvailable, image);
        }).ToList();
    }

    public async Task<IReadOnlyList<VariantSummary>> GetVariantSummariesAsync(IReadOnlyCollection<long> variantIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(variantIds);
        if (variantIds.Count > IStorefrontCatalogQueries.MaxIds)
        {
            throw new ArgumentException($"At most {IStorefrontCatalogQueries.MaxIds} variants can be asked for at once.", nameof(variantIds));
        }

        var ids = variantIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        // One joined query: a variant of a product that is in the read model (published and priced) and that can be sold at all.
        return await (
            from v in db.Variants.AsNoTracking()
            join p in db.Products.AsNoTracking() on v.ProductId equals p.Id
            where ids.Contains(v.Id) && v.IsSellable && p.MinPrice != null
            orderby v.Id
            select new VariantSummary(v.Id, p.Id, p.Name, p.Path, v.ColourName, v.SizeName, v.SkuCode, v.AvailableQuantity > 0))
            .ToListAsync(cancellationToken);
    }
}
