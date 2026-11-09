using System.Text.Json;
using Romp.Modules.Cart.Contracts;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Infrastructure.Orders;
using Romp.Modules.Orders.Tests.Support;

namespace Romp.Modules.Orders.Tests.Orders;

/// <summary>S14 O1: Fulfilment reads an order's lines, totals, payment method and recipient name and city, and nothing else personal.</summary>
public sealed class OrderFulfilmentQueriesTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private static int _seq;

    private static PlaceOrderCommand Place(short method) =>
        new(Guid.NewGuid().ToString("N"), new CheckoutContact("Fulfil Tester", "f@example.com", $"03{Interlocked.Increment(ref _seq) + 600_000_000:000000000}", true),
            new CheckoutAddress("Sara Khan", "0300 7654321", "House 4, Street 9", null, 2, 7, null, null), null, method);

    private static async Task<PlaceOrderResult> PlaceAsync(OrdersHost host, short method, long variant)
    {
        host.Pricing.Priced = FakePricing.Basket(0m, "7", (variant, 2, 150m)) with
        {
            Lines = [new CheckoutLine(variant, 55, "Floral Dress", "/girls/dresses/floral", $"ROMP-V{variant}", "Red", "2-3Y", 150m, 2, 300m, 9000 + variant)],
        };
        return await host.SendAsync(Place(method));
    }

    [Fact]
    [Trait("Spec", "ALC-1")]
    public async Task ACashOnDeliveryOrder_ReturnsItsLinesPlacementKeyAndTheCashToCollect()
    {
        using var host = new OrdersHost(postgres);
        var placed = await PlaceAsync(host, PaymentMethods.CashOnDelivery, 8801);
        await using var db = postgres.NewContext();

        var view = await new OrderFulfilmentQueries(db).FindAsync(placed.OrderId!.Value, CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal(placed.OrderNumber, view.OrderNumber);
        Assert.True(view.IsCashOnDelivery);
        Assert.Equal(view.Total, view.CashToCollect);
        Assert.False(string.IsNullOrWhiteSpace(view.PlacementKey));
        var line = Assert.Single(view.Lines);
        Assert.Equal((8801L, 2, 150m, 300m), (line.VariantId, line.Quantity, line.UnitPrice, line.LineTotal));
        Assert.Equal("Sara Khan", view.RecipientName);
    }

    [Fact]
    [Trait("Spec", "SLP-3")]
    public async Task AnOnlineOrder_HasNothingToCollect()
    {
        using var host = new OrdersHost(postgres);
        var placed = await PlaceAsync(host, PaymentMethods.Card, 8802);
        await using var db = postgres.NewContext();

        var view = await new OrderFulfilmentQueries(db).FindAsync(placed.OrderId!.Value, CancellationToken.None);

        Assert.NotNull(view);
        Assert.False(view.IsCashOnDelivery);
        Assert.Equal(0m, view.CashToCollect);
    }

    [Fact]
    [Trait("Spec", "SLP-7")]
    public async Task TheView_CarriesNoPhoneEmailOrStreetAddress()
    {
        using var host = new OrdersHost(postgres);
        var placed = await PlaceAsync(host, PaymentMethods.CashOnDelivery, 8803);
        await using var db = postgres.NewContext();

        var view = await new OrderFulfilmentQueries(db).FindAsync(placed.OrderId!.Value, CancellationToken.None);

        var json = JsonSerializer.Serialize(view);
        Assert.DoesNotContain("0300", json);
        Assert.DoesNotContain("f@example.com", json);
        Assert.DoesNotContain("House 4", json);
    }

    [Fact]
    [Trait("Spec", "ALC-1")]
    public async Task AnUnknownOrder_IsNull()
    {
        await using var db = postgres.NewContext();

        Assert.Null(await new OrderFulfilmentQueries(db).FindAsync(987_654_321, CancellationToken.None));
    }
}
