using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Cart.Tests.Support;

namespace Romp.Modules.Cart.Tests.Carts;

public sealed class GetCartQueryTests(CartPostgres postgres) : IClassFixture<CartPostgres>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Guest = TimeSpan.FromDays(30);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private async Task<PricedCart> QueryAsync(CartOwner owner, FakeCatalog catalog, FakeStock stock, DateTimeOffset? at = null)
    {
        await using var db = postgres.NewContext();
        var handler = new GetCartQueryHandler(db, new CartPricer(catalog, stock, new FakeImages()), new FixedClock(at ?? Now));
        return await handler.Handle(new GetCartQuery(owner), CancellationToken.None);
    }

    private async Task SaveAsync(ShoppingCart cart)
    {
        await using var db = postgres.NewContext();
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
    }

    private static (FakeCatalog Catalog, FakeStock Stock) Shop()
    {
        var catalog = new FakeCatalog();
        var stock = new FakeStock();
        catalog.Add(1, 1000m);
        stock.Available[9001] = 10;
        return (catalog, stock);
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task TheCallersBasket_IsReadAndPriced()
    {
        var (catalog, stock) = Shop();
        var cart = ShoppingCart.Create(CartOwner.ForCustomer(9101), Now, Guest);
        cart.AddLine(1, 3, 10, 50, Now, Guest);
        await SaveAsync(cart);

        var priced = await QueryAsync(CartOwner.ForCustomer(9101), catalog, stock);

        Assert.Equal(3000m, priced.Subtotal);
        Assert.Equal(3, priced.ItemCount);
    }

    [Fact]
    [Trait("Spec", "CRT-9")]
    public async Task AnotherOwnersBasket_IsNeverSeen()
    {
        var (catalog, stock) = Shop();
        var cart = ShoppingCart.Create(CartOwner.ForCustomer(9201), Now, Guest);
        cart.AddLine(1, 1, 10, 50, Now, Guest);
        await SaveAsync(cart);
        var guest = ShoppingCart.Create(CartOwner.ForGuest("guest-9202"), Now, Guest);
        guest.AddLine(1, 2, 10, 50, Now, Guest);
        await SaveAsync(guest);

        Assert.True((await QueryAsync(CartOwner.ForCustomer(9299), catalog, stock)).IsEmpty);
        Assert.True((await QueryAsync(CartOwner.ForGuest("someone-else"), catalog, stock)).IsEmpty);
        Assert.Equal(2, (await QueryAsync(CartOwner.ForGuest("guest-9202"), catalog, stock)).ItemCount);
        Assert.Equal(1, (await QueryAsync(CartOwner.ForCustomer(9201), catalog, stock)).ItemCount);
    }

    [Fact]
    [Trait("Spec", "CRT-10")]
    public async Task AnExpiredGuestBasket_ReadsAsEmpty_ButAnAccountBasketNever()
    {
        var (catalog, stock) = Shop();
        var guest = ShoppingCart.Create(CartOwner.ForGuest("guest-9301"), Now, Guest);
        guest.AddLine(1, 1, 10, 50, Now, Guest);
        await SaveAsync(guest);
        var account = ShoppingCart.Create(CartOwner.ForCustomer(9302), Now, Guest);
        account.AddLine(1, 1, 10, 50, Now, Guest);
        await SaveAsync(account);
        var later = Now.AddDays(31);

        Assert.True((await QueryAsync(CartOwner.ForGuest("guest-9301"), catalog, stock, later)).IsEmpty);
        Assert.Equal(1, (await QueryAsync(CartOwner.ForCustomer(9302), catalog, stock, later)).ItemCount);
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task OwnersWithNoBasket_GetAnEmptyOne_WithoutCallingOtherModules()
    {
        var (catalog, stock) = Shop();

        var priced = await QueryAsync(CartOwner.ForCustomer(9401), catalog, stock);

        Assert.True(priced.IsEmpty);
        Assert.Equal(0, catalog.Calls + stock.Calls);
    }
}
