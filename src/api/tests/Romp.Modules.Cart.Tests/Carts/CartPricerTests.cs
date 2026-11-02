using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Cart.Tests.Support;

namespace Romp.Modules.Cart.Tests.Carts;

public sealed class CartPricerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Guest = TimeSpan.FromDays(30);

    private readonly FakeCatalog _catalog = new();
    private readonly FakeStock _stock = new();

    private CartPricer Pricer() => new(_catalog, _stock);

    private static ShoppingCart Cart(params (long Variant, int Quantity)[] lines)
    {
        var cart = ShoppingCart.Create(CartOwner.ForCustomer(1), Now, Guest);
        foreach (var (variant, quantity) in lines)
        {
            cart.AddLine(variant, quantity, 10, 50, Now, Guest);
        }

        return cart;
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task TheSubtotal_IsTheSumOfServerPricesTimesQuantities()
    {
        _catalog.Add(1, 1250.50m);
        _catalog.Add(2, 800m);
        _stock.Available[9001] = 10;
        _stock.Available[9002] = 10;

        var priced = await Pricer().PriceAsync(Cart((1, 2), (2, 1)), CancellationToken.None);

        Assert.Equal(2501.00m, priced.Lines[0].LineTotal);
        Assert.Equal(800m, priced.Lines[1].LineTotal);
        Assert.Equal(3301.00m, priced.Subtotal);
        Assert.Equal(3, priced.ItemCount);
        Assert.False(priced.HasProblems);
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task AnEmptyOrMissingCart_IsEmpty_AndCallsNothing()
    {
        Assert.True((await Pricer().PriceAsync(null, CancellationToken.None)).IsEmpty);
        Assert.True((await Pricer().PriceAsync(Cart(), CancellationToken.None)).IsEmpty);
        Assert.Equal(0, _catalog.Calls + _stock.Calls);
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task APricedBasket_CostsOneCatalogCallAndOneWarehouseCall_HoweverManyLines()
    {
        for (var v = 1; v <= 20; v++)
        {
            _catalog.Add(v, 100m + v);
            _stock.Available[9000 + v] = 5;
        }

        var priced = await Pricer().PriceAsync(Cart(Enumerable.Range(1, 20).Select(v => ((long)v, 1)).ToArray()), CancellationToken.None);

        Assert.Equal(20, priced.Lines.Count);
        Assert.Equal((1, 1), (_catalog.Calls, _stock.Calls));
    }

    [Fact]
    [Trait("Spec", "CRT-7")]
    public async Task ALineWithLessStockThanItsQuantity_IsFlagged_WithTheAvailableCount_AndTheCartStillLoads()
    {
        _catalog.Add(1);
        _catalog.Add(2);
        _stock.Available[9001] = 1;
        _stock.Available[9002] = 5;

        var priced = await Pricer().PriceAsync(Cart((1, 3), (2, 1)), CancellationToken.None);

        var short1 = priced.Lines.Single(l => l.VariantId == 1);
        Assert.True(short1.Issues.HasFlag(LineIssues.LowStock));
        Assert.Equal(1, short1.Available);
        Assert.False(short1.IsOrderable);
        Assert.Equal(LineIssues.None, priced.Lines.Single(l => l.VariantId == 2).Issues);
        Assert.True(priced.HasProblems);
    }

    [Fact]
    [Trait("Spec", "CRT-7")]
    public async Task AVariantWithNoStockRecord_CountsAsZeroAvailable()
    {
        _catalog.Add(1);

        var priced = await Pricer().PriceAsync(Cart((1, 1)), CancellationToken.None);

        Assert.Equal(0, priced.Lines[0].Available);
        Assert.True(priced.Lines[0].Issues.HasFlag(LineIssues.LowStock));
    }

    [Theory]
    [Trait("Spec", "CRT-5")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AVariantThatCannotBeBought_IsFlaggedUnavailable_KeptAndLeftOutOfTheSubtotal(bool knownToCatalog)
    {
        _catalog.Add(1, 500m);
        _stock.Available[9001] = 9;
        if (knownToCatalog)
        {
            _catalog.Add(2, price: null, sellable: false);
        }

        var priced = await Pricer().PriceAsync(Cart((1, 1), (2, 2)), CancellationToken.None);

        var gone = priced.Lines.Single(l => l.VariantId == 2);
        Assert.True(gone.Issues.HasFlag(LineIssues.Unavailable));
        Assert.Null(gone.UnitPrice);
        Assert.Equal(0m, gone.LineTotal);
        Assert.Equal(500m, priced.Subtotal);
        Assert.True(priced.HasProblems);
        if (!knownToCatalog)
        {
            Assert.Equal(CartPricer.GoneName, gone.Name);
        }
    }

    [Fact]
    [Trait("Spec", "CRT-8")]
    public async Task APriceThatMovedSinceTheShopperSawIt_IsFlagged_WithThePreviousPrice_AndTheNewOneIsUsed()
    {
        _catalog.Add(1, 1500m);
        _stock.Available[9001] = 9;
        var cart = Cart((1, 2));
        cart.NotePrices(new Dictionary<long, decimal> { [1] = 1200m });

        var priced = await Pricer().PriceAsync(cart, CancellationToken.None);

        var line = priced.Lines[0];
        Assert.True(line.Issues.HasFlag(LineIssues.PriceChanged));
        Assert.Equal(1200m, line.PreviousPrice);
        Assert.Equal(1500m, line.UnitPrice);
        Assert.Equal(3000m, line.LineTotal);
        Assert.True(line.IsOrderable);
    }

    [Fact]
    [Trait("Spec", "CRT-8")]
    public async Task ALineWhosePriceIsUnchangedOrNeverNoted_HasNoPriceFlag()
    {
        _catalog.Add(1, 1500m);
        _catalog.Add(2, 900m);
        _stock.Available[9001] = 9;
        _stock.Available[9002] = 9;
        var cart = Cart((1, 1), (2, 1));
        cart.NotePrices(new Dictionary<long, decimal> { [1] = 1500m });

        var priced = await Pricer().PriceAsync(cart, CancellationToken.None);

        Assert.All(priced.Lines, l => Assert.False(l.Issues.HasFlag(LineIssues.PriceChanged)));
    }

    [Fact]
    [Trait("Spec", "TOT-2")]
    public async Task LineTotals_AreRoundedToTwoDecimals_HalfAwayFromZero()
    {
        _catalog.Add(1, 33.335m);
        _stock.Available[9001] = 9;

        var priced = await Pricer().PriceAsync(Cart((1, 1)), CancellationToken.None);

        Assert.Equal(33.34m, priced.Lines[0].LineTotal);
    }
}
