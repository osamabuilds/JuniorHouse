using Romp.BuildingBlocks.Domain;
using Romp.Modules.Customers.Application.Wishlist;
using Romp.Modules.Customers.Tests.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Wishlist;

/// <summary>customers/wishlist W4 (WSL-1, WSL-4, WSL-7): add and remove through the real pipeline and database.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class WishlistCommandTests(CustomersPostgres postgres)
{
    [Fact]
    [Trait("Spec", "WSL-1")]
    public async Task Add_SavesTheProduct_WithThePriceFromTheStorefront()
    {
        using var harness = new AddressHarness(postgres);
        harness.Catalog.Publish(42, 1999.50m);
        var customer = await harness.NewCustomerAsync();

        await harness.Send(customer, new AddWishlistItemCommand(42));

        Assert.Equal([(42L, 1999.50m)], await harness.StoredWishlistAsync(customer));
    }

    [Fact]
    [Trait("Spec", "WSL-4")]
    public async Task Add_TheSameProductTwice_StillLeavesOneRow_AndKeepsTheFirstSavedPrice()
    {
        using var harness = new AddressHarness(postgres);
        harness.Catalog.Publish(42, 2000m);
        var customer = await harness.NewCustomerAsync();
        await harness.Send(customer, new AddWishlistItemCommand(42));
        harness.Catalog.Publish(42, 1500m);

        await harness.Send(customer, new AddWishlistItemCommand(42));

        Assert.Equal([(42L, 2000m)], await harness.StoredWishlistAsync(customer));
    }

    [Fact]
    [Trait("Spec", "WSL-4")]
    public async Task Add_TheSameProductAtOnce_LeavesOneRow()
    {
        using var harness = new AddressHarness(postgres);
        harness.Catalog.Publish(42, 2000m);
        var customer = await harness.NewCustomerAsync();

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => harness.Send(customer, new AddWishlistItemCommand(42))));

        Assert.Single(await harness.StoredWishlistAsync(customer));
    }

    [Fact]
    [Trait("Spec", "WSL-4")]
    public async Task Add_AtTheLimit_IsRefused_ButASavedProductCanBeAddedAgain()
    {
        using var harness = new AddressHarness(postgres, maxWishlistItems: 2);
        foreach (var id in new long[] { 1, 2, 3 })
        {
            harness.Catalog.Publish(id, 100m);
        }

        var customer = await harness.NewCustomerAsync();
        await harness.Send(customer, new AddWishlistItemCommand(1));
        await harness.Send(customer, new AddWishlistItemCommand(2));

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Send(customer, new AddWishlistItemCommand(3)));
        await harness.Send(customer, new AddWishlistItemCommand(1));

        Assert.Contains("2", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, (await harness.StoredWishlistAsync(customer)).Count);
    }

    [Fact]
    [Trait("Spec", "WSL-5")]
    public async Task Add_AProductThatIsNotPublished_IsNotFound_AndNothingIsSaved()
    {
        using var harness = new AddressHarness(postgres);
        var customer = await harness.NewCustomerAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => harness.Send(customer, new AddWishlistItemCommand(999)));

        Assert.Empty(await harness.StoredWishlistAsync(customer));
    }

    [Fact]
    [Trait("Spec", "WSL-1")]
    public async Task Remove_TakesTheProductOffTheList()
    {
        using var harness = new AddressHarness(postgres);
        harness.Catalog.Publish(1, 100m);
        harness.Catalog.Publish(2, 100m);
        var customer = await harness.NewCustomerAsync();
        await harness.Send(customer, new AddWishlistItemCommand(1));
        await harness.Send(customer, new AddWishlistItemCommand(2));

        await harness.Send(customer, new RemoveWishlistItemCommand(1));

        Assert.Equal([2L], (await harness.StoredWishlistAsync(customer)).Select(r => r.ProductId));
    }

    [Fact]
    [Trait("Spec", "WSL-7")]
    public async Task Remove_AnotherCustomersProduct_IsNotFound_AndTheirRowIsUntouched()
    {
        using var harness = new AddressHarness(postgres);
        harness.Catalog.Publish(1, 100m);
        var owner = await harness.NewCustomerAsync();
        var intruder = await harness.NewCustomerAsync();
        await harness.Send(owner, new AddWishlistItemCommand(1));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => harness.Send(intruder, new RemoveWishlistItemCommand(1)));

        Assert.Single(await harness.StoredWishlistAsync(owner));
    }

    [Fact]
    [Trait("Spec", "WSL-7")]
    public async Task Commands_WithNoSignedInCustomer_AreRefused()
    {
        using var harness = new AddressHarness(postgres);
        harness.Catalog.Publish(1, 100m);

        await Assert.ThrowsAnyAsync<Exception>(() => harness.Send(null, new AddWishlistItemCommand(1)));
        await Assert.ThrowsAnyAsync<Exception>(() => harness.Send(null, new RemoveWishlistItemCommand(1)));
    }
}
