using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Application.Urls;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Catalog.Application.Products;

/// <summary>
/// The Catalog side of <see cref="IVariantSalesQueries"/>: the same join the product page uses (planned variant, Labelling's copy, price),
/// for the variants a basket or an order names. A fixed handful of queries however many variants are asked for.
/// </summary>
public sealed class VariantSalesQueries(ICatalogDbContext dbContext, ICatalogLookupQueries lookups, IProductPathBuilder paths) : IVariantSalesQueries
{
    private static readonly short PublishedStatus = (short)ProductStatus.Published;

    public async Task<IReadOnlyList<VariantSale>> GetManyAsync(IReadOnlyCollection<long> variantIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(variantIds);
        if (variantIds.Count > IVariantSalesQueries.MaxIds)
        {
            throw new ArgumentException($"At most {IVariantSalesQueries.MaxIds} variants can be asked for at once.", nameof(variantIds));
        }

        var ids = variantIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var products = await dbContext.Products.AsNoTracking().AsSplitQuery()
            .Where(p => p.StatusId == PublishedStatus && p.Variants.Any(v => ids.Contains(v.Id)))
            .Include(p => p.Variants)
            .ToListAsync(cancellationToken);
        if (products.Count == 0)
        {
            return [];
        }

        var productIds = products.Select(p => p.Id).ToList();
        var styleIds = products.Select(p => p.StyleId).Distinct().ToList();
        var labelled = (await dbContext.LabelledVariants.AsNoTracking().Where(l => styleIds.Contains(l.StyleId)).ToListAsync(cancellationToken))
            .ToLookup(l => l.StyleId);
        var images = (await dbContext.Images.AsNoTracking().Where(i => productIds.Contains(i.ProductId)).ToListAsync(cancellationToken))
            .ToLookup(i => i.ProductId);

        var genderIds = products.Select(p => p.GenderId).Distinct().ToList();
        var categoryIds = products.Select(p => p.CategoryId).Distinct().ToList();
        var colours = await lookups.GetColoursAsync(products.SelectMany(p => p.Variants).Select(v => v.ColourId).Distinct().ToList(), cancellationToken);
        var sizes = await lookups.GetSizesAsync(products.SelectMany(p => p.Variants).Select(v => v.SizeId).Distinct().ToList(), cancellationToken);
        var pathMap = await paths.LoadAsync(genderIds, categoryIds, cancellationToken);

        var result = new List<VariantSale>();
        foreach (var product in products)
        {
            var labelledIds = labelled[product.StyleId]
                .GroupBy(l => (l.ColourId, l.SizeId))
                .ToDictionary(
                    g => g.Key,
                    g => (g.Where(l => !l.GtinVoided).OrderByDescending(l => l.LabellingVariantId).FirstOrDefault()
                        ?? g.OrderByDescending(l => l.LabellingVariantId).First()).LabellingVariantId);
            var productImages = images[product.Id].OrderBy(i => i.SortSeq).ThenBy(i => i.Id).ToList();
            var path = pathMap.PathOf(product.GenderId, product.CategoryId, product.Slug)
                ?? throw new InvalidOperationException($"Product {product.Id}: gender or category is not in the reference data.");

            foreach (var view in SellableVariants.Join(product.Variants, labelled[product.StyleId]).Where(v => ids.Contains(v.VariantId)))
            {
                var image = productImages.FirstOrDefault(i => i.ColourId == view.ColourId) ?? productImages.FirstOrDefault(i => i.ColourId is null) ?? productImages.FirstOrDefault();
                long? labellingId = labelledIds.TryGetValue((view.ColourId, view.SizeId), out var lid) ? lid : null;
                result.Add(new VariantSale(
                    view.VariantId,
                    product.Id,
                    product.Name,
                    path,
                    colours.TryGetValue(view.ColourId, out var colour) ? colour.Name : string.Empty,
                    sizes.TryGetValue(view.SizeId, out var size) ? size.Name : string.Empty,
                    view.IsSellable ? view.SkuCode : null,
                    view.IsSellable ? labellingId : null,
                    view.Price,
                    view.CompareAtPrice,
                    view.IsSellable,
                    product.CategoryId,
                    image?.FileKey,
                    image?.AltText,
                    image?.Width,
                    image?.Height));
            }
        }

        return result;
    }
}
