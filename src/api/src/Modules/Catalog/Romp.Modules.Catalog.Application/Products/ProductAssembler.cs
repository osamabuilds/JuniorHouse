using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Application.CompositionCare;
using Romp.Modules.Catalog.Application.ProductMedia;
using Romp.Modules.Catalog.Application.SizeCharts;
using Romp.Modules.Catalog.Application.Urls;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Labelling.Contracts;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Catalog.Application.Products;

/// <summary>
/// Builds the product the editor shows (PRD-1) and evaluates the publish gate (PRD-4). A constant number of queries however many
/// variants, images or chart lines exist: the product with its variants and collections, the style code, the Labelling copies,
/// two lookup reads, the path lookups and one read per gate.
/// </summary>
public sealed class ProductAssembler(
    ICatalogDbContext dbContext,
    ICatalogLookupQueries lookups,
    IProductPathBuilder paths,
    IMediaGate mediaGate,
    ICompositionGate compositionGate,
    IChartGate chartGate,
    IOptions<CatalogOptions> options)
{
    /// <summary>The tracked product with its variants and collections, or a 404.</summary>
    public async Task<Product> LoadAsync(long productId, CancellationToken cancellationToken) =>
        await dbContext.Products.AsSplitQuery()
            .Include(p => p.Variants)
            .Include(p => p.Collections)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
        ?? throw new KeyNotFoundException($"Product {productId} was not found.");

    public async Task<IReadOnlyList<VariantView>> JoinVariantsAsync(Product product, CancellationToken cancellationToken)
    {
        var labelled = await dbContext.LabelledVariants.AsNoTracking()
            .Where(l => l.StyleId == product.StyleId)
            .ToListAsync(cancellationToken);
        return SellableVariants.Join(product.Variants, labelled);
    }

    public async Task<PublishGateResult> EvaluateGateAsync(Product product, IReadOnlyList<VariantView> views, CancellationToken cancellationToken)
    {
        var media = await mediaGate.ReadAsync(product.Id, cancellationToken);
        var composition = await compositionGate.ReadAsync(product.Id, cancellationToken);
        var hasChart = await chartGate.HasChartAsync(product.CategoryId, product.GenderId, cancellationToken);
        var catalog = options.Value;

        var live = views.Count(v => v.Availability is VariantAvailability.Sellable or VariantAvailability.Unpriced);
        var unpriced = views.Count(v => v.Availability == VariantAvailability.Unpriced);
        var input = new PublishGateInput(
            product.Name, product.Description, product.GenderId, product.CategoryId, product.AgeBracketId, product.Slug,
            live, unpriced, media.ImageCount, media.ImagesNeedingAltText,
            composition.Rows, composition.TotalPercent, composition.CareInstructions, hasChart);
        return PublishGate.Evaluate(input, catalog.ReservedSlugs, catalog.MaxSlugLength);
    }

    public async Task<ProductDto> BuildAsync(Product product, CancellationToken cancellationToken)
    {
        var views = await JoinVariantsAsync(product, cancellationToken);
        var gate = await EvaluateGateAsync(product, views, cancellationToken);
        var colours = await lookups.GetColoursAsync(views.Select(v => v.ColourId).Distinct().ToList(), cancellationToken);
        var sizes = await lookups.GetSizesAsync(views.Select(v => v.SizeId).Distinct().ToList(), cancellationToken);
        var styleCode = await dbContext.Styles.AsNoTracking().Where(s => s.Id == product.StyleId).Select(s => s.Code).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        var path = await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken);

        var variants = views
            .Select(v => new VariantDto(
                v.VariantId, v.ColourId, colours.TryGetValue(v.ColourId, out var c) ? c.Name : v.ColourId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                v.SizeId, sizes.TryGetValue(v.SizeId, out var s) ? s.Name : v.SizeId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                v.Price, v.CompareAtPrice, v.SkuCode, v.Gtin, v.Availability.ToString(), v.IsSellable))
            .ToList();

        return new ProductDto(
            product.Id, product.StyleId, styleCode, product.Name, product.Description, product.GenderId, product.CategoryId, product.AgeBracketId,
            (short)product.Status, product.Status.ToString(), product.Slug, path, product.CareNote, product.PublishedAt, product.PublishedBy,
            dbContext.VersionOf(product), product.Collections.Select(c => c.CollectionId).OrderBy(id => id).ToList(), variants,
            views.Count(v => v.IsSellable),
            new PublishGatePreviewDto(gate.Failures.Select(f => new GateFailureDto(f.Code, f.Message, f.Tab)).ToList(), gate.Warnings));
    }

    public async Task<ProductDto> BuildByIdAsync(long productId, CancellationToken cancellationToken) =>
        await BuildAsync(await LoadAsync(productId, cancellationToken), cancellationToken);
}

/// <summary>
/// Keeps <c>VRNT_LBL</c> in step with Labelling (ADR 0014): an upsert on the Labelling variant id, so applying the same facts twice,
/// or in any order, leaves the same rows (PRD-15). Returns the styles whose rows changed so the caller can tell the storefront.
/// </summary>
public sealed class LabelledVariantWriter(ICatalogDbContext dbContext)
{
    public async Task<IReadOnlySet<long>> UpsertAsync(IReadOnlyCollection<LabelledVariantInfo> infos, CancellationToken cancellationToken)
    {
        var changedStyles = new HashSet<long>();
        if (infos.Count == 0)
        {
            return changedStyles;
        }

        var ids = infos.Select(i => i.VariantId).Distinct().ToList();
        var existing = await dbContext.LabelledVariants.Where(l => ids.Contains(l.LabellingVariantId)).ToDictionaryAsync(l => l.LabellingVariantId, cancellationToken);
        foreach (var info in infos)
        {
            if (existing.TryGetValue(info.VariantId, out var row))
            {
                if (row.Refresh(info.StyleId, info.ColourId, info.SizeId, info.SkuCode, info.Gtin, info.GtinVoided))
                {
                    changedStyles.Add(info.StyleId);
                }
            }
            else
            {
                dbContext.LabelledVariants.Add(new LabelledVariant(info.VariantId, info.StyleId, info.ColourId, info.SizeId, info.SkuCode, info.Gtin, info.GtinVoided));
                changedStyles.Add(info.StyleId);
            }
        }

        return changedStyles;
    }
}
