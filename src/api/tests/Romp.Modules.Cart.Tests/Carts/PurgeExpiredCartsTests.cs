using Microsoft.EntityFrameworkCore;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Cart.Tests.Support;

namespace Romp.Modules.Cart.Tests.Carts;

public sealed class PurgeExpiredCartsTests(CartPostgres postgres) : IClassFixture<CartPostgres>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Guest = TimeSpan.FromDays(30);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private async Task AddAsync(CartOwner owner, DateTimeOffset touched)
    {
        var cart = ShoppingCart.Create(owner, touched, Guest);
        cart.AddLine(1, 1, 10, 50, touched, Guest);
        await using var db = postgres.NewContext();
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
    }

    private async Task<int> PurgeAsync(DateTimeOffset at)
    {
        await using var db = postgres.NewContext();
        return await new PurgeExpiredCartsCommandHandler(db, new FixedClock(at)).Handle(new PurgeExpiredCartsCommand(), CancellationToken.None);
    }

    [Fact]
    [Trait("Spec", "CRT-10")]
    public async Task ExpiredGuestBaskets_AreRemovedWithTheirLines_AndEverythingElseStays()
    {
        await AddAsync(CartOwner.ForGuest("old-guest-aaaa"), Now.AddDays(-40));
        await AddAsync(CartOwner.ForGuest("fresh-guest-bbb"), Now.AddDays(-5));
        await AddAsync(CartOwner.ForCustomer(13_001), Now.AddDays(-400));

        var removed = await PurgeAsync(Now);

        Assert.True(removed >= 1);
        await using var db = postgres.NewContext();
        Assert.Equal(0, await db.Carts.CountAsync(c => c.GuestKeyHash == "old-guest-aaaa"));
        Assert.Equal(1, await db.Carts.CountAsync(c => c.GuestKeyHash == "fresh-guest-bbb"));
        Assert.Equal(1, await db.Carts.CountAsync(c => c.CustomerId == 13_001));
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM \"CART\".\"CART_LINE\" l LEFT JOIN \"CART\".\"CART_MAIN\" c ON c.\"ID\" = l.\"CART_ID\" WHERE c.\"ID\" IS NULL").SingleAsync());
    }

    [Fact]
    [Trait("Spec", "CRT-10")]
    public async Task ARunWithNothingExpired_RemovesNothing()
    {
        await PurgeAsync(Now);

        Assert.Equal(0, await PurgeAsync(Now));
    }
}
