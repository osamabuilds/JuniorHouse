using Romp.Modules.Orders.Application.OrderLookup;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Tests.Support;

namespace Romp.Modules.Orders.Tests.Orders;

public sealed class ViewOrderByLinkTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private static int _seq;

    [Fact]
    [Trait("Spec", "OLK-5")]
    public async Task AValidLink_OpensTheMaskedStatusPage_WithNoFurtherInput()
    {
        using var host = new OrdersHost(postgres);
        var mobile = $"03{Interlocked.Increment(ref _seq) + 400_000_000:000000000}";
        var placed = await host.SendAsync(new PlaceOrderCommand(
            Guid.NewGuid().ToString("N"),
            new CheckoutContact("Ayesha Khan", null, mobile, false),
            new CheckoutAddress("Ayesha Khan", mobile, "House 4", null, 2, 7, null, null), null, PaymentMethods.CashOnDelivery));

        var order = await host.SendAsync(new ViewOrderByLinkQuery(new FakeLookupLinks().Create(placed.OrderNumber!)));

        Assert.NotNull(order);
        Assert.Equal(placed.OrderNumber, order.OrderNumber);
        Assert.Equal("Lahore", order.City);
        Assert.DoesNotContain(mobile[1..], order.MaskedMobile, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Spec", "OLK-5")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("trk-RMP-2026-99999-0")]
    public async Task ABrokenLink_OrALinkToNoOrder_GivesNothing(string? token)
    {
        using var host = new OrdersHost(postgres);

        Assert.Null(await host.SendAsync(new ViewOrderByLinkQuery(token)));
    }
}
