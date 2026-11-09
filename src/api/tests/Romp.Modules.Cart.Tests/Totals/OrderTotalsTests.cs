using Romp.Modules.Cart.Domain.Totals;

namespace Romp.Modules.Cart.Tests.Totals;

public sealed class OrderTotalsTests
{
    private static TotalLine L(decimal price, int quantity) => new(price, quantity);

    [Fact]
    [Trait("Spec", "TOT-1")]
    public void ShippingIsChargedOncePerOrder_NotPerItem_AndTheArithmeticIsExact()
    {
        var totals = OrderTotals.Calculate([L(1000m, 2), L(500m, 3), L(250m, 1)], discount: 0m, shippingFee: 250m);

        Assert.Equal(3750m, totals.Subtotal);
        Assert.Equal(250m, totals.Shipping);
        Assert.Equal(4000m, totals.Total);
        Assert.True(totals.ShippingKnown);
    }

    [Fact]
    [Trait("Spec", "TOT-2")]
    public void FractionalRupees_AreRoundedHalfAwayFromZero_AndThePartsAddUpToTheTotal()
    {
        var totals = OrderTotals.Calculate([L(33.335m, 1), L(10.005m, 3)], discount: 5.125m, shippingFee: 99.995m);

        Assert.Equal(33.34m + 30.02m, totals.Subtotal);
        Assert.Equal(5.13m, totals.Discount);
        Assert.Equal(100.00m, totals.Shipping);
        Assert.Equal(totals.Subtotal - totals.Discount + totals.Shipping, totals.Total);
    }

    [Theory]
    [Trait("Spec", "TOT-2")]
    [InlineData(0.005, 0.01)]
    [InlineData(0.004, 0.00)]
    [InlineData(2.675, 2.68)]
    [InlineData(-2.675, -2.68)]
    public void Round_IsHalfAwayFromZero(double input, double expected)
    {
        Assert.Equal((decimal)expected, OrderTotals.Round((decimal)input));
    }

    [Fact]
    [Trait("Spec", "TOT-4")]
    public void AnEmptyBasket_IsAllZero_AndIsNotChargedShipping()
    {
        var totals = OrderTotals.Calculate([], discount: 100m, shippingFee: 250m);

        Assert.Equal(new OrderAmounts(0m, 0m, 0m, true, 0m), totals);
    }

    [Fact]
    [Trait("Spec", "TOT-6")]
    public void ADiscountLargerThanTheSubtotal_NeverMakesTheTotalNegative()
    {
        var totals = OrderTotals.Calculate([L(500m, 1)], discount: 9000m, shippingFee: 200m);

        Assert.Equal(500m, totals.Discount);
        Assert.Equal(200m, totals.Total);
        Assert.True(totals.Total >= 0);
    }

    [Fact]
    [Trait("Spec", "TOT-4")]
    public void WithNoCityYet_ShippingIsUnknownAndNotInTheTotal()
    {
        var totals = OrderTotals.Calculate([L(1000m, 1)], discount: 0m, shippingFee: null);

        Assert.False(totals.ShippingKnown);
        Assert.Equal(0m, totals.Shipping);
        Assert.Equal(1000m, totals.Total);
    }

    [Fact]
    [Trait("Spec", "TOT-7")]
    public void TheSameInputs_GiveTheSameAnswer_EveryTime()
    {
        var lines = new[] { L(1234.56m, 3), L(99.99m, 7) };

        var first = OrderTotals.Calculate(lines, 150m, 300m);

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(first, OrderTotals.Calculate(lines, 150m, 300m));
        }
    }

    [Fact]
    [Trait("Spec", "TOT-3")]
    public void NegativeAmounts_AreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrderTotals.Calculate([L(1m, 1)], -1m, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => OrderTotals.Calculate([L(1m, 1)], 0m, -5m));
    }

    [Fact]
    [Trait("Spec", "TOT-2")]
    public void ForManyRandomBaskets_ThePartsAlwaysAddUp_AndNothingIsNegative()
    {
        var random = new Random(20261102);
        for (var i = 0; i < 500; i++)
        {
            var lines = Enumerable.Range(0, random.Next(0, 8)).Select(_ => L(Math.Round((decimal)random.NextDouble() * 5000m, 3), random.Next(1, 10))).ToList();
            var discount = Math.Round((decimal)random.NextDouble() * 20000m, 3);
            var shipping = random.Next(0, 2) == 0 ? (decimal?)null : Math.Round((decimal)random.NextDouble() * 600m, 3);

            var t = OrderTotals.Calculate(lines, discount, shipping);

            Assert.True(t.Total >= 0 && t.Subtotal >= 0 && t.Discount >= 0 && t.Shipping >= 0);
            Assert.True(t.Discount <= t.Subtotal);
            Assert.Equal(t.Subtotal - t.Discount + t.Shipping, t.Total);
            Assert.Equal(t.Subtotal, lines.Sum(l => OrderTotals.Round(l.UnitPrice * l.Quantity)));
        }
    }
}

public sealed class ShippingPolicyTests
{
    private static ShippingPolicy Policy(decimal? freeThreshold = null, decimal? codFee = null) =>
        new(new Dictionary<int, decimal> { [1] = 200m, [2] = 350m }, defaultFee: 500m, freeThreshold, codFee);

    [Fact]
    [Trait("Spec", "TOT-1")]
    public void ACityWithItsOwnRate_UsesIt_AndAnyOtherCityUsesTheDefault()
    {
        Assert.Equal(200m, Policy().FeeFor(1, 1000m, isCashOnDelivery: false));
        Assert.Equal(350m, Policy().FeeFor(2, 1000m, isCashOnDelivery: false));
        Assert.Equal(500m, Policy().FeeFor(99, 1000m, isCashOnDelivery: false));
    }

    [Fact]
    [Trait("Spec", "TOT-4")]
    public void WithNoCity_TheFeeIsNotKnownYet()
    {
        Assert.Null(Policy().FeeFor(null, 1000m, isCashOnDelivery: false));
    }

    [Theory]
    [Trait("Spec", "TOT-1")]
    [InlineData(7999.99, 200)]
    [InlineData(8000, 0)]
    [InlineData(12000, 0)]
    public void DeliveryIsFree_OnceTheSubtotalAfterTheDiscountReachesTheThreshold(double subtotal, double expected)
    {
        Assert.Equal((decimal)expected, Policy(freeThreshold: 8000m).FeeFor(1, (decimal)subtotal, isCashOnDelivery: false));
    }

    [Fact]
    [Trait("Spec", "TOT-1")]
    public void ThereIsNoFreeDeliveryOffer_WhenNoThresholdIsSet()
    {
        Assert.Equal(200m, Policy().FeeFor(1, 1_000_000m, isCashOnDelivery: false));
    }

    [Fact]
    [Trait("Spec", "TOT-1")]
    public void TheCodFee_IsAddedOnlyToCodOrders_EvenWhenDeliveryIsFree()
    {
        var policy = Policy(freeThreshold: 8000m, codFee: 100m);

        Assert.Equal(300m, policy.FeeFor(1, 1000m, isCashOnDelivery: true));
        Assert.Equal(200m, policy.FeeFor(1, 1000m, isCashOnDelivery: false));
        Assert.Equal(100m, policy.FeeFor(1, 9000m, isCashOnDelivery: true));
    }

    [Fact]
    [Trait("Spec", "TOT-2")]
    public void FeesAreRoundedToTwoDecimals()
    {
        var policy = new ShippingPolicy(new Dictionary<int, decimal>(), 199.999m, null, 0.005m);

        Assert.Equal(200.01m, policy.FeeFor(1, 100m, isCashOnDelivery: true));
    }

    [Fact]
    [Trait("Spec", "TOT-1")]
    public void ANegativeFee_IsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShippingPolicy(new Dictionary<int, decimal>(), -1m, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShippingPolicy(new Dictionary<int, decimal>(), 0m, -5m, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShippingPolicy(new Dictionary<int, decimal>(), 0m, null, -1m));
    }
}
