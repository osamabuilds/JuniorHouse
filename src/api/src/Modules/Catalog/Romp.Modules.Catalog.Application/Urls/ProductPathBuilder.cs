using Romp.Modules.Catalog.Domain.Urls;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Catalog.Application.Urls;

/// <summary>The gender and category path segments for a set of ids, read once (two queries however many products).</summary>
public sealed class PathSegmentMap(IReadOnlyDictionary<short, string> genders, IReadOnlyDictionary<short, string> categories)
{
    /// <summary>The public path for a product's gender, category and slug; null when either lookup row is unknown.</summary>
    public string? PathOf(short genderId, short categoryId, string slug) =>
        genders.TryGetValue(genderId, out var gender) && categories.TryGetValue(categoryId, out var category)
            ? new ProductPath(gender, category, slug).ToString()
            : null;
}

/// <summary>Builds public paths from lookup codes (ADR 0013). Urls owns it; products and every other feature use it through this interface.</summary>
public interface IProductPathBuilder
{
    Task<PathSegmentMap> LoadAsync(IReadOnlyCollection<short> genderIds, IReadOnlyCollection<short> categoryIds, CancellationToken cancellationToken);
}

public sealed class ProductPathBuilder(ICatalogLookupQueries lookups) : IProductPathBuilder
{
    public async Task<PathSegmentMap> LoadAsync(IReadOnlyCollection<short> genderIds, IReadOnlyCollection<short> categoryIds, CancellationToken cancellationToken)
    {
        var genders = await lookups.GetGendersAsync(genderIds, cancellationToken);
        var categories = await lookups.GetCategoriesAsync(categoryIds, cancellationToken);
        return new PathSegmentMap(
            genders.ToDictionary(g => g.Key, g => ProductPath.SegmentOf(g.Value.Code)),
            categories.ToDictionary(c => c.Key, c => ProductPath.SegmentOf(c.Value.Code)));
    }
}

/// <summary>Convenience for the common single-product case.</summary>
public static class ProductPathBuilderExtensions
{
    public static async Task<string> PathOfAsync(
        this IProductPathBuilder builder, short genderId, short categoryId, string slug, CancellationToken cancellationToken)
    {
        var map = await builder.LoadAsync([genderId], [categoryId], cancellationToken);
        return map.PathOf(genderId, categoryId, slug)
            ?? throw new InvalidOperationException($"Gender {genderId} or category {categoryId} is not in the reference data.");
    }
}
