using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Application.Payments;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Domain.Payments;
using Romp.Modules.Orders.Infrastructure.Payments;

namespace Romp.Modules.Orders.Tests.Support;

/// <summary>An online order that has been placed: its number, its payment token and the gateway reference of its open try (once started).</summary>
internal sealed record PlacedOnline(string OrderNumber, string Token)
{
    public string? GatewayRef { get; set; }
}

/// <summary>
/// Everything the payment tests share: the Orders pipeline against a real PostgreSQL, the fake gateway, and short helpers to place an online
/// order, open its payment, deliver signed callbacks and look at the result. Every call uses its own scope, like a real request.
/// </summary>
internal sealed class PaymentRig(OrdersPostgres postgres) : IDisposable
{
    public OrdersHost Host { get; } = new(postgres);

    public FakeGateway Gateway => Host.Gateway;

    public static string NewKey() => Guid.NewGuid().ToString("N");

    public static PlaceOrderCommand Place(short method = PaymentMethods.Card) =>
        new(NewKey(), new CheckoutContact("Ayesha Khan", "ayesha@example.com", "0300 1234567", true),
            new CheckoutAddress("Ayesha Khan", "0300 1234567", "House 4, Street 2", null, 2, 7, "54000", null), null, method);

    public async Task<PlacedOnline> PlaceOnlineAsync(short method = PaymentMethods.Card)
    {
        var result = await Host.SendAsync(Place(method));
        Assert.Equal(PlaceOrderOutcome.Placed, result.Outcome);
        Assert.NotNull(result.PayToken);
        return new PlacedOnline(result.OrderNumber!, result.PayToken!);
    }

    /// <summary>Places an online order and opens its payment page (a gateway session now exists).</summary>
    public async Task<PlacedOnline> PlaceAndStartAsync(short method = PaymentMethods.Card)
    {
        var placed = await PlaceOnlineAsync(method);
        var started = await Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));
        Assert.Equal(StartPaymentKind.Started, started.Kind);
        placed.GatewayRef = await OpenRefAsync(placed.OrderNumber);
        return placed;
    }

    public async Task<string?> OpenRefAsync(string orderNumber)
    {
        await using var db = postgres.NewContext();
        return await (from a in db.PaymentAttempts
                      join p in db.Payments on a.PaymentId equals p.Id
                      join o in db.Orders on p.OrderId equals o.Id
                      where o.OrderNumber == orderNumber && a.StatusId == PaymentStatuses.Pending
                      orderby a.AttemptNo descending
                      select a.GatewayRef).FirstOrDefaultAsync();
    }

    /// <summary>Delivers a signed callback exactly as the gateway would, through the webhook command.</summary>
    public Task<WebhookResult> DeliverAsync(string gatewayRef, GatewayOutcome outcome, decimal? amount = null, string? eventId = null, DateTimeOffset? at = null)
    {
        var (body, headers) = Gateway.BuildCallback(gatewayRef, outcome, amount, eventId, at);
        return Host.SendAsync(new HandlePaymentWebhookCommand("fake", body, headers));
    }

    public async Task<TResult> InScopeAsync<TResult>(Func<ISender, Task<TResult>> work)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<ISender>());
    }

    public async Task<Order> OrderAsync(string number)
    {
        await using var db = postgres.NewContext();
        return await db.Orders.AsNoTracking().Include(o => o.History).SingleAsync(o => o.OrderNumber == number);
    }

    public async Task<Payment> PaymentAsync(string number)
    {
        await using var db = postgres.NewContext();
        return await (from p in db.Payments.AsNoTracking().Include(x => x.Attempts)
                      join o in db.Orders on p.OrderId equals o.Id
                      where o.OrderNumber == number
                      select p).SingleAsync();
    }

    public async Task<int> EventCountAsync(string eventType, string orderNumber)
    {
        await using var db = postgres.NewContext();
        // The payload is jsonb, which has no LIKE: read the payloads of this event type and match the order number in memory.
        var payloads = await db.Set<OutboxMessage>().Where(m => m.EventType == eventType).Select(m => m.Payload).ToListAsync();
        return payloads.Count(p => p.Contains(orderNumber, StringComparison.Ordinal));
    }

    public async Task<List<PaymentWebhook>> WebhooksAsync(long paymentId)
    {
        await using var db = postgres.NewContext();
        return await db.PaymentWebhooks.AsNoTracking().Where(w => w.PaymentId == paymentId).ToListAsync();
    }

    public void Advance(TimeSpan by) => Host.Time.Now += by;

    public void Dispose() => Host.Dispose();
}
