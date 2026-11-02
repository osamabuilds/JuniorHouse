using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Cart.Application;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Application.DiscountCodes;
using Romp.Modules.Cart.Application.Totals;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Cart.Domain.DiscountCodes;
using Romp.Modules.Cart.Infrastructure.Persistence;
using Romp.Modules.Cart.Tests.Support;

namespace Romp.Modules.Cart.Tests.DiscountCodes;

public sealed class ApplyDiscountTests(CartPostgres postgres) : IClassFixture<CartPostgres>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static int _seq = 40_000;

    private readonly FakeCatalog _catalog = new();
    private readonly FakeStock _stock = new();
    private readonly FakeImages _images = new();

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static string NewCode() => $"T{Interlocked.Increment(ref _seq)}";

    private async Task<(CartOwner Owner, CartNotices Notices)> BasketAsync(decimal price = 1000m, int quantity = 2)
    {
        var id = Interlocked.Increment(ref _seq);
        _catalog.Add(id, price);
        _stock.Available[9000 + id] = 50;
        var owner = CartOwner.ForCustomer(id);
        var notices = new CartNotices();
        await using var db = postgres.NewContext();
        var support = Support(db, notices);
        await new AddLineCommandHandler(support).Handle(new AddLineCommand(owner, id, quantity), CancellationToken.None);
        await db.SaveChangesAsync();
        return (owner, notices);
    }

    private CartCommandSupport Support(CartDbContext db, CartNotices notices) =>
        new(db, _catalog, _stock, new CartPricer(_catalog, _stock, _images), Options.Create(new CartOptions()), new FixedTime(Now), new DiscountService(db), notices);

    private async Task<string> CreateCodeAsync(
        DiscountKind kind = DiscountKind.Percentage, decimal value = 10m, decimal? min = null, DateTimeOffset? expires = null, int? cap = null, int? perCustomer = 1)
    {
        var code = NewCode();
        await using var db = postgres.NewContext();
        db.DiscountCodes.Add(DiscountCode.Create(code, kind, value, min, expires, cap, perCustomer, false, [], Now.AddDays(-1)));
        await db.SaveChangesAsync();
        return code;
    }

    private async Task ApplyAsync(CartOwner owner, string code)
    {
        await using var db = postgres.NewContext();
        await new ApplyDiscountCommandHandler(db, new CartPricer(_catalog, _stock, _images), new DiscountService(db), new FixedTime(Now), Options.Create(new CartOptions()))
            .Handle(new ApplyDiscountCommand(owner, code), CancellationToken.None);
        await db.SaveChangesAsync();
    }

    private async Task<CartWithTotals> ReadAsync(CartOwner owner)
    {
        await using var db = postgres.NewContext();
        var handler = new GetCartWithTotalsQueryHandler(
            db, new CartPricer(_catalog, _stock, _images), new CartTotalsCalculator(new ShippingPolicyLoader(db)), new DiscountService(db), new FixedTime(Now));
        return await handler.Handle(new GetCartWithTotalsQuery(owner, null, false), CancellationToken.None);
    }

    private static string Reason(ValidationException ex) => ex.Errors["code"][0];

    [Fact]
    [Trait("Spec", "DSC-1")]
    public async Task AValidCode_TakesMoneyOff_AndShowsInTheTotals()
    {
        var (owner, _) = await BasketAsync(1000m, 2);
        var code = await CreateCodeAsync(value: 10m);

        await ApplyAsync(owner, code);
        var read = await ReadAsync(owner);

        Assert.Equal(code, read.DiscountCode);
        Assert.Equal(200m, read.Amounts.Discount);
        Assert.Equal(1800m, read.Amounts.Total);
    }

    [Fact]
    [Trait("Spec", "DSC-1")]
    public async Task TheCodeCanBeTypedInAnyCase()
    {
        var (owner, _) = await BasketAsync();
        var code = await CreateCodeAsync();

        await ApplyAsync(owner, $"  {code.ToLowerInvariant()} ");

        Assert.Equal(code, (await ReadAsync(owner)).DiscountCode);
    }

    [Fact]
    [Trait("Spec", "DSC-9")]
    public async Task AnUnknownCode_IsRefusedWithTheGenericReason()
    {
        var (owner, _) = await BasketAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => ApplyAsync(owner, "NOSUCHCODE"));

        Assert.Equal("That code is not valid.", Reason(ex));
        Assert.Null((await ReadAsync(owner)).DiscountCode);
    }

    [Fact]
    [Trait("Spec", "DSC-2")]
    public async Task AnExpiredCode_IsRefusedAsExpired()
    {
        var (owner, _) = await BasketAsync();
        var code = await CreateCodeAsync(expires: Now.AddMinutes(30));
        await using (var db = postgres.NewContext())
        {
            // Pushed into the past directly: the domain refuses to create an already-expired code.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"CART\".\"DISC_CODE\" SET \"EXPY_DTE\" = {Now.AddHours(-1)} WHERE \"CODE\" = {code}");
        }

        var ex = await Assert.ThrowsAsync<ValidationException>(() => ApplyAsync(owner, code));

        Assert.Equal("That code has expired.", Reason(ex));
    }

    [Fact]
    [Trait("Spec", "DSC-6")]
    public async Task ABasketBelowTheMinimum_IsToldTheMinimum()
    {
        var (owner, _) = await BasketAsync(500m, 1);
        var code = await CreateCodeAsync(min: 2000m);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => ApplyAsync(owner, code));

        Assert.Contains("2,000", Reason(ex), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "DSC-4")]
    public async Task ASecondCode_IsRefused_AndTheFirstStays()
    {
        var (owner, _) = await BasketAsync();
        var first = await CreateCodeAsync();
        var second = await CreateCodeAsync();
        await ApplyAsync(owner, first);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => ApplyAsync(owner, second));

        Assert.Contains(first, Reason(ex), StringComparison.Ordinal);
        Assert.Equal(first, (await ReadAsync(owner)).DiscountCode);
    }

    [Fact]
    [Trait("Spec", "DSC-4")]
    public async Task ReapplyingTheSameCode_IsHarmless()
    {
        var (owner, _) = await BasketAsync();
        var code = await CreateCodeAsync();

        await ApplyAsync(owner, code);
        await ApplyAsync(owner, code);

        Assert.Equal(code, (await ReadAsync(owner)).DiscountCode);
    }

    [Fact]
    [Trait("Spec", "DSC-4")]
    public async Task RemovingTheCode_PutsThePriceBack()
    {
        var (owner, _) = await BasketAsync(1000m, 2);
        var code = await CreateCodeAsync();
        await ApplyAsync(owner, code);

        await using (var db = postgres.NewContext())
        {
            await new RemoveDiscountCommandHandler(db, new FixedTime(Now), Options.Create(new CartOptions())).Handle(new RemoveDiscountCommand(owner), CancellationToken.None);
            await db.SaveChangesAsync();
        }

        var read = await ReadAsync(owner);
        Assert.Null(read.DiscountCode);
        Assert.Equal(2000m, read.Amounts.Total);
    }

    [Fact]
    [Trait("Spec", "DSC-3")]
    public async Task ACustomerWhoAlreadyUsedTheCode_IsToldSo()
    {
        var (owner, _) = await BasketAsync();
        var code = await CreateCodeAsync(perCustomer: 1);
        await using (var db = postgres.NewContext())
        {
            var found = await db.DiscountCodes.SingleAsync(c => c.Code == code);
            var redemption = new Redemption(found.Id, $"used-{code}", $"C{owner.CustomerId}", 100m);
            redemption.Confirm("RMP-2026-00001-1");
            db.Redemptions.Add(redemption);
            await db.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<ValidationException>(() => ApplyAsync(owner, code));

        Assert.Equal("You have already used that code.", Reason(ex));
    }

    [Fact]
    [Trait("Spec", "DSC-5")]
    public async Task ACodeAtItsUsageCap_IsNoLongerAvailable()
    {
        var (owner, _) = await BasketAsync();
        var code = await CreateCodeAsync(cap: 1, perCustomer: null);
        await using (var db = postgres.NewContext())
        {
            var found = await db.DiscountCodes.SingleAsync(c => c.Code == code);
            db.Redemptions.Add(new Redemption(found.Id, $"held-{code}", "C1", 100m));
            await db.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<ValidationException>(() => ApplyAsync(owner, code));

        Assert.Equal("That code is no longer available.", Reason(ex));
    }

    [Fact]
    [Trait("Spec", "DSC-13")]
    public async Task WhenTheBasketFallsBelowTheMinimum_TheCodeComesOff_WithANotice()
    {
        var (owner, _) = await BasketAsync(1000m, 2);
        var code = await CreateCodeAsync(min: 1500m);
        await ApplyAsync(owner, code);
        var variantId = owner.CustomerId!.Value;

        var notices = new CartNotices();
        await using (var db = postgres.NewContext())
        {
            await new SetQuantityCommandHandler(Support(db, notices)).Handle(new SetQuantityCommand(owner, variantId, 1), CancellationToken.None);
            await db.SaveChangesAsync();
        }

        Assert.Single(notices.Items);
        Assert.Contains(code, notices.Items[0], StringComparison.Ordinal);
        var read = await ReadAsync(owner);
        Assert.Null(read.DiscountCode);
        Assert.Equal(1000m, read.Amounts.Total);
    }

    [Fact]
    [Trait("Spec", "DSC-13")]
    public async Task ACodeThatStopsHolding_IsNotChargedOnARead_AndIsNoticed()
    {
        var (owner, _) = await BasketAsync(1000m, 2);
        var code = await CreateCodeAsync();
        await ApplyAsync(owner, code);
        await using (var db = postgres.NewContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"CART\".\"DISC_CODE\" SET \"ACT_IND\" = false WHERE \"CODE\" = {code}");
        }

        var read = await ReadAsync(owner);

        Assert.Equal(0m, read.Amounts.Discount);
        Assert.Equal(2000m, read.Amounts.Total);
        Assert.NotNull(read.DiscountNotice);
    }

    [Fact]
    [Trait("Spec", "DSC-1")]
    public async Task AnEmptyBasket_CannotTakeACode()
    {
        var code = await CreateCodeAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => ApplyAsync(CartOwner.ForCustomer(Interlocked.Increment(ref _seq)), code));

        Assert.Contains("bag", Reason(ex), StringComparison.Ordinal);
    }
}
