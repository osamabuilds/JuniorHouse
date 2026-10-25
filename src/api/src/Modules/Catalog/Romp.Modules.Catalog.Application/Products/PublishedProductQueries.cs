using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Application.Urls;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Catalog.Application.Products;

/// <summary>
/// The Catalog side of <see cref="IPublishedProductQueries"/> (published-catalog task 4): the storefront read model's only source. Every
/// call, for one product or a page of two hundred, costs the same few queries: the products with their variants and collections, the
/// images, composition and care rows, the Labelling copies and style codes of the page, and one lookup read per kind (PCT-8).
/// </summary>
public sealed class PublishedProductQueries(ICatalogDbContext dbContext, ICatalogLookupQueries lookups, IProductPathBuilder paths) : IPublishedProductQueries
{
    /// <summary>The most products one call returns; a rebuild asks for pages of this size.</summary>
    public const int MaxPageSize = 200;

    private static readonly short PublishedStatus = (short)ProductStatus.Published;

    public async Task<PublishedProductSnapshot?> GetSnapshotAsync(long productId, CancellationToken cancellationToken)
    {
        var page = await BuildAsync(dbContext.Products.Where(p => p.Id == productId), 1, cancellationToken);
        return page.Count == 0 ? null : page[0];
    }

    public async Task<IReadOnlyList<PublishedProductVersion>> ListVersionsAsync(long afterProductId, int take, CancellationToken cancellationToken) =>
        await dbContext.Products.AsNoTracking()
            .Where(p => p.Id > afterProductId && p.StatusId == PublishedStatus)
            .OrderBy(p => p.Id)
            .Take(Clamp(take))
            .Select(p => new PublishedProductVersion(p.Id, p.ContentVersion))
            .ToListAsync(cancellationToken);

    public Task<IReadOnlyList<PublishedProductSnapshot>> ListSnapshotsAsync(long afterProductId, int take, CancellationToken cancellationToken) =>
        BuildAsync(dbContext.Products.Where(p => p.Id > afterProductId), take, cancellationToken);

    private static int Clamp(int take) => Math.Clamp(take, 1, MaxPageSize);

    private async Task<IReadOnlyList<PublishedProductSnapshot>> BuildAsync(IQueryable<Product> source, int take, CancellationToken cancellationToken)
    {
        var products = await source.AsNoTracking().AsSplitQuery()
            .Where(p => p.StatusId == PublishedStatus)
            .Include(p => p.Variants)
            .Include(p => p.Collections)
            .OrderBy(p => p.Id)
            .Take(Clamp(take))
            .ToListAsync(cancellationToken);
        if (products.Count == 0)
        {
            return [];
        }

        var ids = products.Select(p => p.Id).ToList();
        var styleIds = products.Select(p => p.StyleId).Distinct().ToList();

        var images = (await dbContext.Images.AsNoTracking().Where(i => ids.Contains(i.ProductId)).ToListAsync(cancellationToken))
            .ToLookup(i => i.ProductId);
        var composition = (await dbContext.CompositionLines.AsNoTracking().Where(c => ids.Contains(c.ProductId)).ToListAsync(cancellationToken))
            .ToLookup(c => c.ProductId);
        var care = (await dbContext.CareInstructions.AsNoTracking().Where(c => ids.Contains(c.ProductId)).ToListAsync(cancellationToken))
            .ToLookup(c => c.ProductId);
        var labelled = (await dbContext.LabelledVariants.AsNoTracking().Where(l => styleIds.Contains(l.StyleId)).ToListAsync(cancellationToken))
            .ToLookup(l => l.StyleId);
        var styleCodes = await dbContext.Styles.AsNoTracking().Where(s => styleIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Code, cancellationToken);

        var genderIds = products.Select(p => p.GenderId).Distinct().ToList();
        var categoryIds = products.Select(p => p.CategoryId).Distinct().ToList();
        var genders = await lookups.GetGendersAsync(genderIds, cancellationToken);
        var categories = await lookups.GetCategoriesAsync(categoryIds, cancellationToken);
        var ages = await lookups.GetAgeBracketsAsync(products.Select(p => p.AgeBracketId).Distinct().ToList(), cancellationToken);
        var collections = await lookups.GetCollectionsAsync(products.SelectMany(p => p.Collections).Select(c => c.CollectionId).Distinct().ToList(), cancellationToken);
        var colours = await lookups.GetColoursAsync(products.SelectMany(p => p.Variants).Select(v => v.ColourId).Distinct().ToList(), cancellationToken);
        var sizes = await lookups.GetSizesAsync(products.SelectMany(p => p.Variants).Select(v => v.SizeId).Distinct().ToList(), cancellationToken);
        var fibres = await lookups.GetFibresAsync(composition.SelectMany(g => g).Select(c => c.FibreId).Distinct().ToList(), cancellationToken);
        var careCodes = await lookups.GetCareInstructionsAsync(care.SelectMany(g => g).Select(c => c.CareId).Distinct().ToList(), cancellationToken);
        var pathMap = await paths.LoadAsync(genderIds, categoryIds, cancellationToken);

        return products.Select(p =>
        {
            var views = SellableVariants.Join(p.Variants, labelled[p.StyleId]);
            return new PublishedProductSnapshot(
                p.Id,
                p.ContentVersion,
                pathMap.PathOf(p.GenderId, p.CategoryId, p.Slug)
                    ?? throw new InvalidOperationException($"Product {p.Id}: gender {p.GenderId} or category {p.CategoryId} is not in the reference data."),
                p.Name,
                p.Description,
                styleCodes.GetValueOrDefault(p.StyleId, string.Empty),
                Code(genders, p.GenderId, "gender"),
                Code(categories, p.CategoryId, "category"),
                Code(ages, p.AgeBracketId, "age bracket"),
                p.Collections.OrderBy(c => c.CollectionId).Select(c => Code(collections, c.CollectionId, "collection")).ToList(),
                p.PublishedAt ?? throw new InvalidOperationException($"Product {p.Id} is published without a publish date."),
                p.CareNote,
                views.Select(v => new PublishedVariant(
                    v.VariantId,
                    Code(colours, v.ColourId, "colour"),
                    Code(sizes, v.SizeId, "size"),
                    v.IsSellable ? v.SkuCode : null,
                    v.Price,
                    v.CompareAtPrice,
                    v.IsSellable)).ToList(),
                images[p.Id].OrderBy(i => i.SortSeq).ThenBy(i => i.Id)
                    .Select(i => new PublishedImage(i.Id, i.ColourId, i.FileKey, i.Width, i.Height, i.AltText ?? string.Empty, i.SortSeq)).ToList(),
                composition[p.Id].OrderByDescending(c => c.Percent).ThenBy(c => c.FibreId)
                    .Select(c => new PublishedCompositionLine(Code(fibres, c.FibreId, "fibre"), c.Percent)).ToList(),
                care[p.Id].OrderBy(c => c.CareId)
                    .Select(c => careCodes.TryGetValue(c.CareId, out var code)
                        ? new PublishedCareInstruction(new PublishedCode(code.Id, code.Code, code.Name), code.SymbolCode)
                        : throw Missing("care instruction", c.CareId)).ToList());
        }).ToList();
    }

    private static PublishedCode Code(IReadOnlyDictionary<short, ApparelCode> map, short id, string kind) =>
        map.TryGetValue(id, out var code) ? new PublishedCode(code.Id, code.Code, code.Name) : throw Missing(kind, id);

    private static InvalidOperationException Missing(string kind, short id) =>
        new($"The {kind} with id {id.ToString(CultureInfo.InvariantCulture)} is not in the reference data.");
}
