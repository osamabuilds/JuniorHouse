using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Tests.Support;

namespace Romp.Modules.Orders.Tests.Orders;

/// <summary>A session that times out while the checkout page is open, and the Back button (orders H3, CHK-5, CHK-10).</summary>
public sealed class CheckoutSessionTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private static PlaceOrderCommand Place(string key, short method = PaymentMethods.CashOnDelivery) =>
        new(key, new CheckoutContact("Ayesha Khan", "ayesha@example.com", "0300 1234567", false),
            new CheckoutAddress("Ayesha Khan", "0300 1234567", "House 4, Street 2", null, 2, 7, null, null), null, method);

    private async Task<int> CountOrdersAsync()
    {
        await using var db = postgres.NewContext();
        return await db.Orders.CountAsync();
    }

    [Fact]
    [Trait("Spec", "CHK-5")]
    [Trait("Spec", "TC-CHK-18")]
    public async Task WhenTheSessionHasTimedOut_NothingIsPlacedOrCharged_AndTheShopperIsToldToRefreshOrSignIn()
    {
        using var host = new OrdersHost(postgres);
        host.Pricing.CallerKey = null;
        var before = await CountOrdersAsync();

        var result = await host.SendAsync(Place(Guid.NewGuid().ToString("N"), PaymentMethods.Card));

        Assert.Equal((PlaceOrderOutcome.Refused, PlaceOrderRefusal.EmptyBasket), (result.Outcome, result.Refusal));
        Assert.Contains("refresh the page or sign in again", Assert.Single(result.Messages), StringComparison.Ordinal);
        Assert.Equal(before, await CountOrdersAsync());
        Assert.Empty(host.Reservations.Reserved);
        Assert.Equal(0, host.Gateway.SessionCreateCalls);
    }

    [Fact]
    [Trait("Spec", "CHK-5")]
    public async Task ASubmitAfterTheSessionChanged_CannotDoubleUpAnOrderThatWasAlreadyPlaced()
    {
        using var host = new OrdersHost(postgres);
        var key = Guid.NewGuid().ToString("N");
        var first = await host.SendAsync(Place(key));
        var before = await CountOrdersAsync();

        // The session ends and the shopper submits the same page again: a new caller, the same key, an emptied basket.
        host.Pricing.CallerKey = "SOMEONE-ELSE";
        host.Pricing.Priced = FakePricing.Basket();
        var again = await host.SendAsync(Place(key));

        // A different caller can never collide with, or read, the earlier order (ADR 0025): it is simply a new placement for them.
        Assert.NotEqual(first.OrderNumber, again.OrderNumber);
        Assert.Equal(before + 1, await CountOrdersAsync());
    }

    [Fact]
    [Trait("Spec", "CHK-10")]
    [Trait("Spec", "TC-CHK-19")]
    public async Task BackAndResubmit_WithTheSameSession_GivesTheSameOrder_NotASecondOne()
    {
        using var host = new OrdersHost(postgres);
        var key = Guid.NewGuid().ToString("N");
        var first = await host.SendAsync(Place(key, PaymentMethods.Card));
        var before = await CountOrdersAsync();

        var back = await host.SendAsync(Place(key, PaymentMethods.Card));

        Assert.True(back.WasRepeat);
        Assert.Equal(first.OrderNumber, back.OrderNumber);
        Assert.Equal(before, await CountOrdersAsync());
        Assert.Single(host.Reservations.Reserved);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Payments.CountAsync(p => db.Orders.Any(o => o.Id == p.OrderId && o.OrderNumber == first.OrderNumber)));
    }

    [Fact]
    [Trait("Spec", "CHK-10")]
    public async Task FiftyDoubleClicks_AtTheSameMoment_PlaceOneOrderAndOnePayment()
    {
        using var host = new OrdersHost(postgres);
        var key = Guid.NewGuid().ToString("N");
        var before = await CountOrdersAsync();
        var gate = new TaskCompletionSource();

        var tasks = Enumerable.Range(0, 50).Select(async _ =>
        {
            await gate.Task;
            return await host.SendAsync(Place(key, PaymentMethods.Card));
        }).ToList();
        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Single(results.Select(r => r.OrderNumber).Distinct());
        Assert.Equal(before + 1, await CountOrdersAsync());
        await using var db = postgres.NewContext();
        var number = results[0].OrderNumber!;
        Assert.Equal(1, await db.Payments.CountAsync(p => db.Orders.Any(o => o.Id == p.OrderId && o.OrderNumber == number)));
    }
}
