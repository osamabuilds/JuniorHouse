using Romp.BuildingBlocks.Application;
using Romp.Modules.Customers.Application.Wishlist;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Wishlist;

/// <summary>customers/wishlist W7 (WSL-3): the guest's remembered products join the account list once.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class WishlistMergeTests(CustomersPostgres postgres)
{
    [Fact]
    [Trait("Spec", "WSL-3")]
    public async Task Merge_AddsTheRememberedProducts_AndASecondMergeAddsNothing()
    {
        using var harness = new AddressHarness(postgres);
        harness.Catalog.Publish(1, 100m);
        harness.Catalog.Publish(2, 200m);
        var customer = await harness.NewCustomerAsync();

        var first = await harness.Send(customer, new MergeGuestWishlistCommand([1, 2]));
        var second = await harness.Send(customer, new MergeGuestWishlistCommand([1, 2]));

        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Equal([(1L, 100m), (2L, 200m)], await harness.StoredWishlistAsync(customer));
    }

    [Fact]
    [Trait("Spec", "WSL-3")]
    public async Task Merge_SkipsUnknownDuplicateAndAlreadySavedProducts()
    {
        using var harness = new AddressHarness(postgres);
        harness.Catalog.Publish(1, 100m);
        harness.Catalog.Publish(2, 200m);
        var customer = await harness.NewCustomerAsync();
        await harness.Send(customer, new AddWishlistItemCommand(1));

        var added = await harness.Send(customer, new MergeGuestWishlistCommand([1, 2, 2, 999]));

        Assert.Equal(1, added);
        Assert.Equal([1L, 2L], (await harness.StoredWishlistAsync(customer)).Select(r => r.ProductId));
    }

    [Fact]
    [Trait("Spec", "WSL-3")]
    public async Task Merge_NeverGoesPastTheListLimit()
    {
        using var harness = new AddressHarness(postgres, maxWishlistItems: 2);
        foreach (var id in new long[] { 1, 2, 3 })
        {
            harness.Catalog.Publish(id, 100m);
        }

        var customer = await harness.NewCustomerAsync();

        var added = await harness.Send(customer, new MergeGuestWishlistCommand([1, 2, 3]));

        Assert.Equal(2, added);
        Assert.Equal(2, (await harness.StoredWishlistAsync(customer)).Count);
    }

    [Fact]
    [Trait("Spec", "WSL-3")]
    public async Task Merge_WithMoreThanFiftyIds_OrANonPositiveId_IsRejected()
    {
        using var harness = new AddressHarness(postgres);
        var customer = await harness.NewCustomerAsync();

        await Assert.ThrowsAsync<ValidationException>(() => harness.Send(customer, new MergeGuestWishlistCommand(Enumerable.Range(1, 51).Select(i => (long)i).ToList())));
        await Assert.ThrowsAsync<ValidationException>(() => harness.Send(customer, new MergeGuestWishlistCommand([0])));
    }

    [Fact]
    [Trait("Spec", "WSL-7")]
    public async Task Merge_AddsOnlyToTheCallersOwnList()
    {
        using var harness = new AddressHarness(postgres);
        harness.Catalog.Publish(1, 100m);
        var me = await harness.NewCustomerAsync();
        var other = await harness.NewCustomerAsync();

        await harness.Send(me, new MergeGuestWishlistCommand([1]));

        Assert.Empty(await harness.StoredWishlistAsync(other));
    }
}
