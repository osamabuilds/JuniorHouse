using Romp.Modules.Customers.Application.Wishlist;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Wishlist;

/// <summary>customers/wishlist W5 (WSL-5, WSL-6, WSL-8): the paged list and the ids, with flags and one bulk catalogue call.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class WishlistQueryTests(CustomersPostgres postgres)
{
    private static async Task SaveAsync(AddressHarness harness, long customer, params long[] productIds)
    {
        foreach (var id in productIds)
        {
            await harness.Send(customer, new AddWishlistItemCommand(id));
        }
    }

    [Fact]
    [Trait("Spec", "WSL-5")]
    [Trait("Spec", "WSL-6")]
    public async Task List_ShowsCurrentPrices_AndFlagsDropsSoldOutAndUnpublished()
    {
        using var harness = new AddressHarness(postgres);
        harness.Catalog.Publish(1, 2000m);
        harness.Catalog.Publish(2, 2000m);
        harness.Catalog.Publish(3, 2000m);
        var customer = await harness.NewCustomerAsync();
        await SaveAsync(harness, customer, 1, 2, 3);
        harness.Catalog.Publish(1, 1500m, was: 2000m);
        harness.Catalog.Publish(2, 2000m, available: false);
        harness.Catalog.Unpublish(3);

        var page = await harness.Send(customer, new ListWishlistQuery());

        var byId = page.Items.ToDictionary(i => i.ProductId);
        Assert.Equal(["PriceDropped", "OnSale"], byId[1].Notes);
        Assert.Equal(1500m, byId[1].Price);
        Assert.Equal(["SoldOut"], byId[2].Notes);
        Assert.False(byId[2].IsAvailable);
        Assert.Equal(["Unavailable"], byId[3].Notes);
        Assert.Null(byId[3].Price);
        Assert.Equal(2000m, byId[3].SavedPrice);
        Assert.Equal("/media/10-640.webp", byId[1].ImageUrl);
        Assert.Equal("/media/10-640.jpg", byId[1].ImageFallbackUrl);
        Assert.Null(byId[3].ImageUrl);
    }

    [Fact]
    [Trait("Spec", "WSL-8")]
    public async Task List_IsPaged_NewestFirst_AndOneCatalogueCallPerPage()
    {
        using var harness = new AddressHarness(postgres);
        var ids = Enumerable.Range(1, 5).Select(i => (long)i).ToArray();
        foreach (var id in ids)
        {
            harness.Catalog.Publish(id, 100m);
        }

        var customer = await harness.NewCustomerAsync();
        await SaveAsync(harness, customer, ids);
        var before = harness.Catalog.Calls;

        var first = await harness.Send(customer, new ListWishlistQuery(Page: 1, PageSize: 2));
        var second = await harness.Send(customer, new ListWishlistQuery(Page: 3, PageSize: 2));

        Assert.Equal([5L, 4L], first.Items.Select(i => i.ProductId));
        Assert.Equal(5, first.TotalCount);
        Assert.Equal([1L], second.Items.Select(i => i.ProductId));
        Assert.Equal(2, harness.Catalog.Calls - before);
    }

    [Fact]
    [Trait("Spec", "WSL-7")]
    public async Task List_AndIds_OnlyShowTheCallersOwnProducts()
    {
        using var harness = new AddressHarness(postgres);
        harness.Catalog.Publish(1, 100m);
        harness.Catalog.Publish(2, 100m);
        var mine = await harness.NewCustomerAsync();
        var theirs = await harness.NewCustomerAsync();
        await SaveAsync(harness, mine, 1);
        await SaveAsync(harness, theirs, 2);

        var page = await harness.Send(mine, new ListWishlistQuery());
        var ids = await harness.Send(mine, new GetWishlistIdsQuery());

        Assert.Equal([1L], page.Items.Select(i => i.ProductId));
        Assert.Equal([1L], ids);
    }

    [Fact]
    [Trait("Spec", "WSL-1")]
    public async Task Ids_AreNewestFirst_AndCappedAtTheListLimit()
    {
        using var harness = new AddressHarness(postgres, maxWishlistItems: 2);
        foreach (var id in new long[] { 1, 2 })
        {
            harness.Catalog.Publish(id, 100m);
        }

        var customer = await harness.NewCustomerAsync();
        await SaveAsync(harness, customer, 1, 2);

        Assert.Equal([2L, 1L], await harness.Send(customer, new GetWishlistIdsQuery()));
    }
}
