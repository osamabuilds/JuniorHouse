using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Application.PublishedCatalog;

namespace Romp.Modules.Storefront.Tests.Support;

/// <summary>Fixed projection settings so tests do not depend on configuration.</summary>
internal sealed class FakeReadModelSettings : IReadModelSettings
{
    public int LowStockThreshold => 5;

    public IReadOnlyList<int> ImageWidths { get; } = [320, 640];
}

/// <summary>The size lookup in a known order (5-6Y before 7-8Y, ids deliberately not in that order); every other lookup is unused by the projection.</summary>
internal sealed class FakeCatalogLookups : ICatalogLookupQueries
{
    public Task<IReadOnlyList<CatalogLookupItem>> ListSizesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CatalogLookupItem>>(
        [
            new CatalogLookupItem(9, "5-6Y", "5-6 Years", 10, true),
            new CatalogLookupItem(4, "7-8Y", "7-8 Years", 20, true),
        ]);

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetGendersAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetCategoriesAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetAgeBracketsAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetCollectionsAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetFibresAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyDictionary<short, CareInstructionCode>> GetCareInstructionsAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetSizesAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyDictionary<string, short>> GetGenderIdsBySegmentAsync(IReadOnlyCollection<string> segments, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyDictionary<string, short>> GetCategoryIdsBySegmentAsync(IReadOnlyCollection<string> segments, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetColoursAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<CatalogLookupItem>> ListGendersAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<CatalogLookupItem>> ListCategoriesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<CatalogLookupItem>> ListAgeBracketsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<CatalogLookupItem>> ListCollectionsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<CatalogLookupItem>> ListColoursAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal static class Snapshots
{
    public static readonly DateTimeOffset PublishedAt = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    public static PublishedVariant Variant(long id, string? sku, decimal? price = 1299m, decimal? compareAt = null, short size = 9, bool sellable = true) =>
        new(id, new PublishedCode(1, "RED", "Red"), new PublishedCode(size, size == 9 ? "5-6Y" : "7-8Y", size == 9 ? "5-6 Years" : "7-8 Years"),
            sku, price, compareAt, sellable);

    public static PublishedImage Image(long id, int sort, string key = "images/a.jpg") =>
        new(id, null, key, 1200, 1600, "Red floral dress", sort);

    public static PublishedProductSnapshot Product(
        long id = 1, long version = 1, string? path = null, string name = "Floral dress",
        IReadOnlyList<PublishedVariant>? variants = null, IReadOnlyList<PublishedImage>? images = null) =>
        new(
            id, version, path ?? $"/girls/dresses/floral-{id}", name, "A dress.", "STY-1",
            new PublishedCode(1, "GIRLS", "Girls"), new PublishedCode(2, "DRESSES", "Dresses"), new PublishedCode(3, "KIDS", "Kids 4-8Y"),
            [new PublishedCode(5, "SUMMER", "Summer")], PublishedAt, "Wash separately",
            variants ?? [Variant(10 * id + 1, $"SKU-{id}-1")],
            images ?? [Image(100 * id + 1, 1), Image(100 * id + 2, 2, "images/b.jpg")],
            [new PublishedCompositionLine(new PublishedCode(1, "COTTON", "Cotton"), 100m)],
            [new PublishedCareInstruction(new PublishedCode(1, "WASH30", "Wash at 30"), "wash-30")]);
}

/// <summary>The three producer contracts the sync commands read, held in memory with call counters (a redelivered event must not read twice).</summary>
internal sealed class FakeProducers :
    Romp.Modules.Catalog.Contracts.IPublishedProductQueries,
    Romp.Modules.Catalog.Contracts.ISizeChartQueries,
    Romp.Modules.Warehouse.Contracts.IStockAvailabilityQueries
{
    public Dictionary<long, PublishedProductSnapshot> Products { get; } = [];

    public Dictionary<(short Category, short Gender), PublishedSizeChart> Charts { get; } = [];

    public Dictionary<long, Romp.Modules.Warehouse.Contracts.StockAvailability> Stock { get; } = [];

    public int Reads { get; private set; }

    public Task<PublishedProductSnapshot?> GetSnapshotAsync(long productId, CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(Products.GetValueOrDefault(productId));
    }

    public Task<PublishedSizeChart?> GetChartAsync(short categoryId, short genderId, CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(Charts.GetValueOrDefault((categoryId, genderId)));
    }

    public Task<Romp.Modules.Warehouse.Contracts.StockAvailability?> GetAsync(long variantId, CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(Stock.GetValueOrDefault(variantId));
    }

    public Task<IReadOnlyList<PublishedProductVersion>> ListVersionsAsync(long afterProductId, int take, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<PublishedProductSnapshot>> ListSnapshotsAsync(long afterProductId, int take, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<PublishedSizeChart>> ListChartsAsync(long afterChartId, int take, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<Romp.Modules.Warehouse.Contracts.StockAvailability>> ListAvailabilityAsync(long afterVariantId, int take, CancellationToken cancellationToken) => throw new NotSupportedException();
}
