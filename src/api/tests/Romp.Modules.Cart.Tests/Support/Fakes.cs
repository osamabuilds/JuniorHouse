using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Cart.Tests.Support;

/// <summary>Catalog's bulk contract held in memory, counting calls (a basket must cost one call however many lines it has).</summary>
internal sealed class FakeCatalog : IVariantSalesQueries
{
    public Dictionary<long, VariantSale> Variants { get; } = [];

    public int Calls { get; private set; }

    public VariantSale Add(long variantId, decimal? price = 1000m, bool sellable = true, decimal? compareAt = null, long? labellingId = null, string name = "Floral Dress")
    {
        var sale = new VariantSale(
            variantId, 500 + variantId, name, "/girls/dresses/floral-dress", "Red", "2-3Y", sellable ? $"ROMP-S{variantId}" : null,
            sellable ? labellingId ?? 9000 + variantId : null, price, compareAt, sellable, 3, 7000 + variantId, "images/a.jpg", "A red dress", 40, 60);
        Variants[variantId] = sale;
        return sale;
    }

    public Task<IReadOnlyList<VariantSale>> GetManyAsync(IReadOnlyCollection<long> variantIds, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<IReadOnlyList<VariantSale>>([.. variantIds.Where(Variants.ContainsKey).Select(id => Variants[id])]);
    }
}

/// <summary>Warehouse's availability contract held in memory, keyed by the Warehouse (Labelling) variant id.</summary>
internal sealed class FakeStock : IStockAvailabilityQueries
{
    public Dictionary<long, int> Available { get; } = [];

    public int Calls { get; private set; }

    public Task<IReadOnlyList<StockAvailability>> GetManyAsync(IReadOnlyCollection<long> variantIds, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<IReadOnlyList<StockAvailability>>(
            [.. variantIds.Where(Available.ContainsKey).Select(id => new StockAvailability(id, $"SKU-{id}", Available[id], 1))]);
    }

    public Task<IReadOnlyList<StockAvailability>> ListAvailabilityAsync(long afterVariantId, int take, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<StockAvailability?> GetAsync(long variantId, CancellationToken cancellationToken) => throw new NotSupportedException();
}

/// <summary>Storefront's thumbnail contract held in memory: an image has a picture only when a test gives it one, and calls are counted.</summary>
internal sealed class FakeImages : Romp.Modules.Storefront.Contracts.IStorefrontImageQueries
{
    public Dictionary<long, Romp.Modules.Storefront.Contracts.ImageThumbnail> Thumbnails { get; } = [];

    public int Calls { get; private set; }

    public Task<IReadOnlyList<Romp.Modules.Storefront.Contracts.ImageThumbnail>> GetThumbnailsAsync(IReadOnlyCollection<long> imageIds, int minWidth, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<IReadOnlyList<Romp.Modules.Storefront.Contracts.ImageThumbnail>>([.. imageIds.Where(Thumbnails.ContainsKey).Select(id => Thumbnails[id])]);
    }
}
