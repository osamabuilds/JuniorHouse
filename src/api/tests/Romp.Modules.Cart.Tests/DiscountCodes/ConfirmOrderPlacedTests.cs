using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Cart.Application;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Application.DiscountCodes;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Cart.Domain.DiscountCodes;
using Romp.Modules.Cart.Tests.Support;

namespace Romp.Modules.Cart.Tests.DiscountCodes;

public sealed class ConfirmOrderPlacedTests(CartPostgres postgres) : IClassFixture<CartPostgres>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static int _seq = 90_000;

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private async Task<(long CartId, long CodeId, string Key)> HeldAsync()
    {
        var n = Interlocked.Increment(ref _seq);
        await using var db = postgres.NewContext();
        var cart = ShoppingCart.Create(CartOwner.ForCustomer(n), Now, TimeSpan.FromDays(30));
        cart.AddLine(n, 2, 10, 50, Now, TimeSpan.FromDays(30));
        var code = DiscountCode.Create($"CF{n}", DiscountKind.Percentage, 10m, null, null, null, 1, false, [], Now.AddDays(-1));
        db.Carts.Add(cart);
        db.DiscountCodes.Add(code);
        await db.SaveChangesAsync();
        cart.ApplyCode(code.Id, Now, TimeSpan.FromDays(30));
        var key = $"cf-{n}";
        db.Redemptions.Add(new Redemption(code.Id, key, $"C{n}", 100m));
        await db.SaveChangesAsync();
        return (cart.Id, code.Id, key);
    }

    private async Task ConfirmAsync(string key, string? cartRef)
    {
        await using var db = postgres.NewContext();
        await new ConfirmOrderPlacedCommandHandler(db, new FixedTime(Now), Options.Create(new CartOptions())).Handle(new ConfirmOrderPlacedCommand(key, "RMP-2026-00001-4", cartRef), CancellationToken.None);
        await db.SaveChangesAsync();
    }

    [Fact]
    [Trait("Spec", "DSC-10")]
    public async Task APlacedOrder_ConfirmsTheHeldUse_AndEmptiesTheBasket_AndARepeatChangesNothing()
    {
        var (cartId, codeId, key) = await HeldAsync();

        await ConfirmAsync(key, cartId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await ConfirmAsync(key, cartId.ToString(System.Globalization.CultureInfo.InvariantCulture));

        await using var db = postgres.NewContext();
        var redemption = await db.Redemptions.SingleAsync(r => r.PlacementKey == key);
        Assert.Equal((RedemptionStatus.Confirmed, "RMP-2026-00001-4"), (redemption.Status, redemption.OrderNumber));
        var cart = await db.Carts.Include(c => c.Lines).SingleAsync(c => c.Id == cartId);
        Assert.True(cart.IsEmpty);
        Assert.Null(cart.AppliedCodeId);
        Assert.NotEqual(0, codeId);
    }

    [Fact]
    [Trait("Spec", "DSC-10")]
    public async Task AnOrderWithNoCodeAndAnUnknownBasket_IsFine()
    {
        await ConfirmAsync($"none-{Guid.NewGuid():N}", "999999999");
        await ConfirmAsync($"none-{Guid.NewGuid():N}", null);
    }

    [Fact]
    [Trait("Spec", "ORD-9")]
    public async Task ACancelledOrder_GivesBackTheConfirmedUse_Once()
    {
        var (cartId, _, key) = await HeldAsync();
        await ConfirmAsync(key, cartId.ToString(System.Globalization.CultureInfo.InvariantCulture));

        for (var i = 0; i < 2; i++)
        {
            await using var db = postgres.NewContext();
            await new ReleaseRedemptionCommandHandler(db).Handle(new ReleaseRedemptionCommand(key), CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await using var read = postgres.NewContext();
        Assert.Equal(RedemptionStatus.Released, (await read.Redemptions.SingleAsync(r => r.PlacementKey == key)).Status);
    }
}
