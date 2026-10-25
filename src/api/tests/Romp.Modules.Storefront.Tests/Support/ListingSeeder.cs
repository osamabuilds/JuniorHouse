using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Tests.Support;

/// <summary>One variant of a seeded product: its colour and size codes, price, optional compare-at price and units in stock.</summary>
internal sealed record SeedVariant(string Colour, string Size, decimal Price, int Stock, decimal? CompareAt = null);

/// <summary>Publishes products into the read model the way the projection would: snapshot, stock, then the image derivatives.</summary>
internal static class ListingSeeder
{
    public static async Task<StorefrontHost> HostWithLookupsAsync(StorefrontPostgres postgres)
    {
        var host = await StorefrontHost.CreateAsync(postgres);
        host.Lookups.Genders.AddRange([new(1, "GIRLS", "Girls", 10, true), new(2, "BOYS", "Boys", 20, true), new(3, "UNISEX", "Unisex", 30, true)]);
        host.Lookups.Categories.AddRange([new(1, "DRESSES", "Dresses", 10, true), new(2, "TOPS", "Tops", 20, true), new(3, "EMPTY", "Empty things", 30, true)]);
        host.Lookups.Collections.AddRange([new(1, "SUMMER", "Summer", 10, true)]);
        host.Lookups.AgeBrackets.AddRange([new(1, "AGE_3Y", "3Y", 10, true), new(2, "KIDS", "Kids 4-8Y", 20, true)]);
        host.Storage.Files["images/a.jpg"] = [1, 2, 3];
        return host;
    }

    public static async Task ListableAsync(
        StorefrontHost host, long id, string gender, string category, IReadOnlyList<SeedVariant>? variants = null,
        string? collection = null, DateTimeOffset? publishedAt = null, string age = "KIDS", string? name = null)
    {
        variants ??= [new SeedVariant("RED", "5-6Y", 800m, 10)];
        var published = variants.Select((v, i) => new PublishedVariant(
            id * 100 + i,
            new PublishedCode(1, v.Colour, v.Colour),
            new PublishedCode((short)(v.Size == "5-6Y" ? 9 : 4), v.Size, v.Size),
            $"SKU-{id}-{i}",
            v.Price,
            v.CompareAt,
            true)).ToList();

        var snapshot = Snapshots.Product(id: id, path: $"/{gender.ToLowerInvariant()}/{category.ToLowerInvariant()}/p-{id}", name: name ?? $"Product {id}", variants: published, images: [Snapshots.Image(100 * id + 1, 1)]) with
        {
            Gender = new PublishedCode(1, gender, gender),
            Category = new PublishedCode(1, category, category),
            AgeBracket = new PublishedCode(1, age, age),
            Collections = collection is null ? [] : [new PublishedCode(1, collection, collection)],
            PublishedAt = publishedAt ?? Snapshots.PublishedAt,
        };

        await host.SendAsync(new ApplyProductSnapshotCommand(id, snapshot));
        for (var i = 0; i < variants.Count; i++)
        {
            await host.SendAsync(new ApplyAvailabilityCommand(new StockAvailability(id * 100 + i, $"SKU-{id}-{i}", variants[i].Stock, 1)));
        }

        while (await host.SendAsync(new ProcessDerivativeCommand()) != DerivativeOutcome.NothingDue)
        {
        }
    }
}
