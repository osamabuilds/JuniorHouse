using Romp.Modules.Cart.Contracts;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Infrastructure.Orders;
using Romp.Modules.Orders.Tests.Support;

namespace Romp.Modules.Orders.Tests.Orders;

/// <summary>Product recommendations O1 (REC-4): units sold per product per UTC day, cancelled orders left out, nothing personal returned.</summary>
public sealed class ProductSalesQueriesTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private static int _seq;

    // A product id range no other test in this class fixture uses, so the sums are exact.
    private static long NextProduct() => 7_000_000 + Interlocked.Increment(ref _seq);

    private static PlaceOrderCommand Place() =>
        new(Guid.NewGuid().ToString("N"), new CheckoutContact("Sales Tester", "s@example.com", $"03{Interlocked.Increment(ref _seq) + 500_000_000:000000000}", true),
            new CheckoutAddress("Sales Tester", "0300 1234567", "House 4", null, 2, 7, null, null), null, PaymentMethods.CashOnDelivery);

    private static async Task<PlaceOrderResult> PlaceAsync(OrdersHost host, DateTimeOffset at, long variant, long product, int quantity)
    {
        host.Time.Now = at;
        host.Pricing.Priced = FakePricing.Basket(0m, "7", (variant, quantity, 100m)) with
        {
            Lines = [new CheckoutLine(variant, product, "Dress", "/girls/dresses/dress", $"ROMP-V{variant}", "Red", "2-3Y", 100m, quantity, 100m * quantity, 9000 + variant)],
        };
        return await host.SendAsync(Place());
    }

    [Fact]
    [Trait("Spec", "REC-4")]
    public async Task Units_AreSummedPerProductPerUtcDay_AndCancelledOrdersAreLeftOut()
    {
        using var host = new OrdersHost(postgres);
        var product = NextProduct();
        var day1 = new DateTimeOffset(2030, 5, 10, 23, 30, 0, TimeSpan.Zero);
        var day2 = new DateTimeOffset(2030, 5, 11, 0, 30, 0, TimeSpan.Zero);

        await PlaceAsync(host, day1, 11, product, 2);
        await PlaceAsync(host, day1.AddMinutes(-20), 11, product, 3);
        await PlaceAsync(host, day2, 11, product, 4);
        var cancelled = await PlaceAsync(host, day2.AddHours(1), 11, product, 9);
        await host.SendAsync(new CancelOrderCommand(cancelled.OrderNumber!, CancelReasons.Staff, ActorKinds.Staff, "1", null));

        await using var db = postgres.NewContext();
        var rows = (await new ProductSalesQueries(db).UnitsSoldByProductAndDayAsync(new DateOnly(2030, 5, 10), new DateOnly(2030, 5, 11), CancellationToken.None))
            .Where(r => r.ProductId == product).ToList();

        Assert.Equal([new ProductDayUnits(product, new DateOnly(2030, 5, 10), 5), new ProductDayUnits(product, new DateOnly(2030, 5, 11), 4)], rows);
    }

    [Fact]
    [Trait("Spec", "REC-4")]
    public async Task Orders_OutsideTheRange_AreLeftOut()
    {
        using var host = new OrdersHost(postgres);
        var product = NextProduct();
        await PlaceAsync(host, new DateTimeOffset(2030, 6, 1, 12, 0, 0, TimeSpan.Zero), 11, product, 1);
        await PlaceAsync(host, new DateTimeOffset(2030, 6, 20, 12, 0, 0, TimeSpan.Zero), 11, product, 1);

        await using var db = postgres.NewContext();
        var queries = new ProductSalesQueries(db);
        var inside = await queries.UnitsSoldByProductAndDayAsync(new DateOnly(2030, 6, 1), new DateOnly(2030, 6, 1), CancellationToken.None);
        var reversed = await queries.UnitsSoldByProductAndDayAsync(new DateOnly(2030, 6, 2), new DateOnly(2030, 6, 1), CancellationToken.None);

        Assert.Equal(new ProductDayUnits(product, new DateOnly(2030, 6, 1), 1), Assert.Single(inside, r => r.ProductId == product));
        Assert.Empty(reversed);
    }

    [Fact]
    [Trait("Spec", "REC-4")]
    public void TheContractRow_CarriesOnlyProductDayAndUnits()
    {
        var names = typeof(ProductDayUnits).GetProperties().Select(p => p.Name).Order().ToArray();

        Assert.Equal(["Day", "ProductId", "Units"], names);
    }
}
