using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Cart.Application;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Cart.Tests.Support;

namespace Romp.Modules.Cart.Tests.Carts;

public sealed class CartCommandTests(CartPostgres postgres) : IClassFixture<CartPostgres>
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private sealed class MovableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly FakeCatalog _catalog = new();
    private readonly FakeStock _stock = new();
    private readonly MovableClock _clock = new(Start);

    private CartCommandSupport Support(CartDbContextHolder holder) =>
        new(holder.Db, _catalog, _stock, new CartPricer(_catalog, _stock), Options.Create(new CartOptions()), _clock);

    private sealed class CartDbContextHolder(Romp.Modules.Cart.Infrastructure.Persistence.CartDbContext db) : IAsyncDisposable
    {
        public Romp.Modules.Cart.Infrastructure.Persistence.CartDbContext Db { get; } = db;

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    /// <summary>One command in its own context, saved at the end, as the transaction behaviour does in the host.</summary>
    private async Task<PricedCart> RunAsync(Func<CartCommandSupport, Task<PricedCart>> act)
    {
        await using var holder = new CartDbContextHolder(postgres.NewContext());
        var priced = await act(Support(holder));
        await holder.Db.SaveChangesAsync();
        return priced;
    }

    private Task<PricedCart> AddAsync(CartOwner owner, long variant, int quantity) =>
        RunAsync(s => new AddLineCommandHandler(s).Handle(new AddLineCommand(owner, variant, quantity), CancellationToken.None));

    private Task<PricedCart> SetAsync(CartOwner owner, long variant, int quantity) =>
        RunAsync(s => new SetQuantityCommandHandler(s).Handle(new SetQuantityCommand(owner, variant, quantity), CancellationToken.None));

    private Task<PricedCart> RemoveAsync(CartOwner owner, long variant) =>
        RunAsync(s => new RemoveLineCommandHandler(s).Handle(new RemoveLineCommand(owner, variant), CancellationToken.None));

    private void Stock(long variant, decimal price, int available)
    {
        _catalog.Add(variant, price);
        _stock.Available[9000 + variant] = available;
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task AddingToAnEmptyOwner_CreatesTheBasket_AndPricesIt()
    {
        Stock(1, 1200m, 10);

        var priced = await AddAsync(CartOwner.ForCustomer(11_001), 1, 2);

        Assert.Equal(2400m, priced.Subtotal);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Carts.CountAsync(c => c.CustomerId == 11_001));
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task ChangingTheQuantity_RecalculatesTheSubtotalFromServerPrices()
    {
        Stock(1, 1000m, 10);
        var owner = CartOwner.ForCustomer(11_002);
        await AddAsync(owner, 1, 1);

        var priced = await SetAsync(owner, 1, 4);

        Assert.Equal(4000m, priced.Subtotal);
        Assert.Equal(4, priced.ItemCount);
    }

    [Fact]
    [Trait("Spec", "CRT-2")]
    public async Task RemovingTheLastLine_LeavesAnEmptyBasket_AndRemovingAnUnknownVariantIsHarmless()
    {
        Stock(1, 1000m, 10);
        var owner = CartOwner.ForCustomer(11_003);
        await AddAsync(owner, 1, 1);

        var unchanged = await RemoveAsync(owner, 999);
        var empty = await RemoveAsync(owner, 1);

        Assert.Equal(1, unchanged.ItemCount);
        Assert.True(empty.IsEmpty);
        Assert.Equal(0m, empty.Subtotal);
    }

    [Fact]
    [Trait("Spec", "CRT-4")]
    public async Task AddingMoreThanIsInStock_IsRefused_NamingTheAvailableCount_AndNothingIsSaved()
    {
        Stock(1, 1000m, 3);
        var owner = CartOwner.ForCustomer(11_004);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => AddAsync(owner, 1, 4));

        Assert.Contains("Only 3 are in stock", ex.Message, StringComparison.Ordinal);
        await using var db = postgres.NewContext();
        Assert.Equal(0, await db.Carts.CountAsync(c => c.CustomerId == 11_004));
    }

    [Fact]
    [Trait("Spec", "CRT-4")]
    public async Task TheStockCheck_CountsWhatTheBasketAlreadyHolds()
    {
        Stock(1, 1000m, 5);
        var owner = CartOwner.ForCustomer(11_005);
        await AddAsync(owner, 1, 3);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => AddAsync(owner, 1, 3));
        var ok = await AddAsync(owner, 1, 2);

        Assert.Contains("Only 5 are in stock", ex.Message, StringComparison.Ordinal);
        Assert.Equal(5, ok.ItemCount);
    }

    [Fact]
    [Trait("Spec", "CRT-4")]
    public async Task AnOutOfStockVariant_IsRefusedWithAClearMessage()
    {
        Stock(1, 1000m, 0);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => AddAsync(CartOwner.ForCustomer(11_006), 1, 1));

        Assert.Contains("out of stock", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "CRT-4")]
    public async Task SettingAQuantityAboveStock_IsRefused_AndTheLineKeepsItsOldQuantity()
    {
        Stock(1, 1000m, 4);
        var owner = CartOwner.ForCustomer(11_007);
        await AddAsync(owner, 1, 2);

        await Assert.ThrowsAsync<ValidationException>(() => SetAsync(owner, 1, 9));

        Assert.Equal(2, (await RemoveAsync(owner, 999)).ItemCount);
    }

    [Theory]
    [Trait("Spec", "CRT-5")]
    [InlineData("unknown")]
    [InlineData("unsellable")]
    public async Task AVariantThatCannotBeBought_IsRefused(string kind)
    {
        if (kind == "unsellable")
        {
            _catalog.Add(2, price: null, sellable: false);
        }

        var ex = await Assert.ThrowsAsync<ValidationException>(() => AddAsync(CartOwner.ForCustomer(11_008), 2, 1));

        Assert.Contains("not available", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Spec", "CRT-11")]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(11)]
    public async Task AQuantityOutsideTheLimits_IsRefusedWithAReason(int quantity)
    {
        Stock(1, 1000m, 50);
        var owner = CartOwner.ForCustomer(11_009);

        await Assert.ThrowsAsync<DomainException>(() => AddAsync(owner, 1, quantity));
        await AddAsync(owner, 1, 1);
        await Assert.ThrowsAsync<DomainException>(() => SetAsync(owner, 1, quantity));
    }

    [Fact]
    [Trait("Spec", "CRT-6")]
    public void TheCommands_HaveNoPlaceForAClientPriceOrTotal()
    {
        foreach (var type in new[] { typeof(AddLineCommand), typeof(SetQuantityCommand), typeof(RemoveLineCommand) })
        {
            var money = type.GetProperties().Where(p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?) || p.PropertyType == typeof(double));
            Assert.Empty(money);
        }
    }

    [Fact]
    [Trait("Spec", "CRT-9")]
    public async Task TwoOwnersNeverShareABasket()
    {
        Stock(1, 1000m, 50);
        await AddAsync(CartOwner.ForCustomer(11_010), 1, 1);
        await AddAsync(CartOwner.ForGuest("guest-11011"), 1, 5);

        Assert.Equal(1, (await RemoveAsync(CartOwner.ForCustomer(11_010), 999)).ItemCount);
        Assert.Equal(5, (await RemoveAsync(CartOwner.ForGuest("guest-11011"), 999)).ItemCount);
    }

    [Fact]
    [Trait("Spec", "CRT-10")]
    public async Task AnExpiredGuestBasket_StartsAgainEmpty_ReusingItsRow()
    {
        Stock(1, 1000m, 50);
        Stock(2, 500m, 50);
        var guest = CartOwner.ForGuest("guest-11012");
        await AddAsync(guest, 1, 3);
        _clock.Now = Start.AddDays(31);

        var priced = await AddAsync(guest, 2, 1);

        Assert.Equal(1, priced.ItemCount);
        Assert.Single(priced.Lines);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Carts.CountAsync(c => c.GuestKeyHash == "guest-11012"));
    }

    [Fact]
    [Trait("Spec", "CRT-8")]
    public async Task APriceChange_AfterTheShopperLastSawIt_IsFlaggedOnTheNextWrite_NotSilentlyAbsorbed()
    {
        Stock(1, 1000m, 50);
        var owner = CartOwner.ForCustomer(11_013);
        await AddAsync(owner, 1, 1);
        _catalog.Add(1, 1300m);
        _stock.Available[9001] = 50;

        var priced = await new GetCartQueryHandler(postgres.NewContext(), new CartPricer(_catalog, _stock), _clock)
            .Handle(new GetCartQuery(owner), CancellationToken.None);

        var line = Assert.Single(priced.Lines);
        Assert.True(line.Issues.HasFlag(LineIssues.PriceChanged));
        Assert.Equal(1000m, line.PreviousPrice);
        Assert.Equal(1300m, line.UnitPrice);
    }
}
