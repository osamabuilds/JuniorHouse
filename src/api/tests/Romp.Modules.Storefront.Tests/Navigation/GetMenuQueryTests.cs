using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Application.Navigation;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Domain.Navigation;
using Romp.Modules.Storefront.Infrastructure.Navigation;
using Romp.Modules.Storefront.Tests.Support;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Tests.Navigation;

/// <summary>Navigation task 2: <see cref="GetMenuQuery"/> against a real PostgreSQL and lookup rows (NAV-1, 3, 5, 9).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class GetMenuQueryTests(StorefrontPostgres postgres)
{
    private static async Task<StorefrontHost> HostAsync(StorefrontPostgres postgres)
    {
        var host = await StorefrontHost.CreateAsync(postgres);
        host.Lookups.Genders.AddRange([new(1, "GIRLS", "Girls", 10, true), new(2, "BOYS", "Boys", 20, true), new(3, "UNISEX", "Unisex", 30, true)]);
        host.Lookups.Categories.AddRange([new(1, "DRESSES", "Dresses", 10, true), new(2, "TOPS", "Tops", 20, true)]);
        host.Lookups.Collections.AddRange([new(1, "SUMMER", "Summer", 10, true)]);
        host.Lookups.AgeBrackets.AddRange([new(1, "AGE_3Y", "3Y", 10, true), new(2, "KIDS", "Kids 4-8Y", 20, true)]);
        host.Storage.Files["images/a.jpg"] = [1, 2, 3];
        return host;
    }

    /// <summary>Publishes a product, gives its only variant stock and makes its main image ready, so it is listable.</summary>
    private static async Task ListableAsync(StorefrontHost host, long id, string gender, string category, bool onSale = false, string? collection = null, bool stocked = true)
    {
        var snapshot = Snapshots.Product(id: id, variants: [Snapshots.Variant(10 * id + 1, $"SKU-{id}", price: 800m, compareAt: onSale ? 1000m : null)], images: [Snapshots.Image(100 * id + 1, 1)]) with
        {
            Gender = new PublishedCode(1, gender, gender),
            Category = new PublishedCode(1, category, category),
            Collections = collection is null ? [] : [new PublishedCode(1, collection, collection)],
        };
        await host.SendAsync(new ApplyProductSnapshotCommand(id, snapshot));
        if (stocked)
        {
            await host.SendAsync(new ApplyAvailabilityCommand(new StockAvailability(10 * id + 1, $"SKU-{id}", 20, 1)));
        }

        while (await host.SendAsync(new ProcessDerivativeCommand()) != DerivativeOutcome.NothingDue)
        {
        }
    }

    [Fact]
    [Trait("Spec", "NAV-1")]
    [Trait("Spec", "NAV-3")]
    public async Task TheMenu_OffersOnlyWhatHasListableProducts_ScopedToTheAudience()
    {
        using var host = await HostAsync(postgres);
        await ListableAsync(host, 1, "GIRLS", "DRESSES", onSale: true, collection: "SUMMER");
        await ListableAsync(host, 2, "BOYS", "TOPS");
        await ListableAsync(host, 3, "GIRLS", "TOPS", stocked: false); // not listable: nothing in stock

        var menu = await host.SendAsync(new GetMenuQuery());

        Assert.Equal(["GIRLS", "BOYS"], menu.Audiences.Select(a => a.Code).ToArray());
        var girls = menu.Audiences[0].Entries;
        Assert.Equal(["NewIn", "Sale", "Category:DRESSES", "Collection:SUMMER", "Age:KIDS"], girls.Select(Describe).ToArray());
        Assert.Equal(["NewIn", "Category:TOPS", "Age:KIDS"], menu.Audiences[1].Entries.Select(Describe).ToArray());
        Assert.Equal(["/collections/summer"], menu.Collections.Select(c => c.Path).ToArray());
        Assert.True(menu.HasSale);
    }

    [Fact]
    [Trait("Spec", "NAV-3")]
    public async Task AnEmptyShop_HasAnEmptyMenu()
    {
        using var host = await HostAsync(postgres);

        var menu = await host.SendAsync(new GetMenuQuery());

        Assert.Empty(menu.Audiences);
        Assert.Empty(menu.Collections);
        Assert.False(menu.HasNewIn);
    }

    [Fact]
    [Trait("Spec", "NAV-1")]
    public async Task UnisexProducts_DoNotMakeAHeaderEntry()
    {
        using var host = await HostAsync(postgres);
        await ListableAsync(host, 1, "UNISEX", "TOPS");

        Assert.Empty((await host.SendAsync(new GetMenuQuery())).Audiences);
    }

    [Fact]
    [Trait("Spec", "NAV-5")]
    [Trait("Spec", "NAV-9")]
    public async Task TheLookupCache_ReloadsAfterSixtySeconds_SoANewLookupRowAppears()
    {
        var clock = new SteppingClock();
        var cache = new MenuLookupCache(clock);
        var calls = 0;
        Task<MenuLookups> Load(CancellationToken _)
        {
            calls++;
            return Task.FromResult(new MenuLookups([], [], [], []));
        }

        await cache.GetOrLoadAsync(Load, default);
        await cache.GetOrLoadAsync(Load, default);
        clock.Advance(TimeSpan.FromSeconds(61));
        await cache.GetOrLoadAsync(Load, default);

        Assert.Equal(2, calls);
    }

    [Fact]
    [Trait("Spec", "NAV-1")]
    public async Task TheLookupCache_IsSingleFlight_UnderConcurrentCallers()
    {
        var cache = new MenuLookupCache(TimeProvider.System);
        var calls = 0;
        async Task<MenuLookups> Load(CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(100, token);
            return new MenuLookups([], [], [], []);
        }

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cache.GetOrLoadAsync(Load, default)));

        Assert.Equal(1, calls);
    }

    [Fact]
    [Trait("Spec", "NAV-1")]
    public async Task TheMenu_UsesAConstantNumberOfQueries_HoweverManyEntriesItHas()
    {
        using var host = await HostAsync(postgres);
        var counter = new QueryCounter();
        await using var context = StorefrontPostgres.NewContext(host.ConnectionString, counter);
        var handler = new GetMenuQueryHandler(context, host.Lookups, new MenuLookupCache(TimeProvider.System), new FakeMenuSettings());

        await ListableAsync(host, 1, "GIRLS", "DRESSES");
        await handler.Handle(new GetMenuQuery(), default);
        var forOne = counter.Count;

        await ListableAsync(host, 2, "GIRLS", "TOPS", onSale: true, collection: "SUMMER");
        await ListableAsync(host, 3, "BOYS", "TOPS");
        counter.Reset();
        await handler.Handle(new GetMenuQuery(), default);

        Assert.Equal(forOne, counter.Count);
        Assert.Equal(2, counter.Count);
    }

    [Fact]
    [Trait("Spec", "PCT-3")]
    public void TheMenuResponse_IsAPublicResponse() =>
        Assert.True(typeof(Romp.Modules.Storefront.Application.Abstractions.IPublicStorefrontResponse).IsAssignableFrom(typeof(MenuDto)));

    private static string Describe(MenuEntry e) => e.Kind is MenuEntryKind.NewIn or MenuEntryKind.Sale ? e.Kind.ToString() : $"{e.Kind}:{e.Code}";
}

/// <summary>A clock the test moves by hand.</summary>
internal sealed class SteppingClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
