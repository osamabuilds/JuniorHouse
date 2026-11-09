using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Application.Payments;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Domain.Payments;
using Romp.Modules.Orders.Infrastructure.Payments;
using Romp.Modules.Orders.Tests.Support;
using Xunit.Abstractions;

namespace Romp.Modules.Orders.Tests.Payments;

/// <summary>
/// "No duplicate payment must happen." Races and repeats against a real PostgreSQL: many identical and many different callbacks at once, many
/// Pay clicks at once, the webhook racing the reconciliation and expiry jobs, a staff cancel racing a payment, and a randomised run of every
/// operation in every order. After each, the invariants hold: the money counts once, one paid event, one open try, and a second charge is
/// flagged and never confirmed twice (PAY-3, PAY-5, FR-30, NFR-09).
/// </summary>
public sealed class NoDuplicatePaymentTests(OrdersPostgres postgres, ITestOutputHelper output) : IClassFixture<OrdersPostgres>
{
    private const int Parallel = 40;

    /// <summary>Runs <paramref name="work"/> many times at the same moment, each in its own scope like a separate request, and collects results and errors.</summary>
    private static async Task<(List<T> Results, List<Exception> Errors)> RaceAsync<T>(int count, Func<int, Task<T>> work)
    {
        var gate = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, count).Select(async i =>
        {
            await gate.Task;
            try
            {
                return (Result: await work(i), Error: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Result: default(T)!, Error: ex);
            }
        }).ToList();
        await Task.Yield();
        gate.SetResult();
        var outcomes = await Task.WhenAll(tasks);
        return ([.. outcomes.Where(o => o.Error is null).Select(o => o.Result)], [.. outcomes.Where(o => o.Error is not null).Select(o => o.Error!)]);
    }

    [Fact]
    [Trait("Spec", "PAY-5")]
    [Trait("Spec", "NFR-09")]
    public async Task FortyCopiesOfTheSameCallback_ArrivingTogether_ConfirmTheOrderOnce()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        var (body, headers) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid, eventId: "evt-race-same");

        var (results, errors) = await RaceAsync(Parallel, _ => rig.InScopeAsync(s => s.Send(new HandlePaymentWebhookCommand("fake", body, headers))));

        // A copy that lost the race at the database is the repeat: it may throw the unique violation, which the endpoint turns into 200.
        Assert.All(errors, e => Assert.IsAssignableFrom<DbUpdateException>(e));
        Assert.Equal(1, results.Count(r => r.Kind == WebhookResultKind.Accepted));
        Assert.Equal(1, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
        Assert.Equal(OrderStatuses.Paid, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        var payment = await rig.PaymentAsync(placed.OrderNumber);
        Assert.Single(await rig.WebhooksAsync(payment.Id));
        Assert.Null(payment.ReviewReasonId);
        output.WriteLine($"{results.Count} answered, {errors.Count} database repeats; one paid event.");
    }

    [Fact]
    [Trait("Spec", "PAY-5")]
    public async Task FortyDifferentSuccessMessagesForOneAttempt_ArrivingTogether_StillConfirmOnce()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();

        var (results, errors) = await RaceAsync(Parallel, i => rig.InScopeAsync(s =>
        {
            var (body, headers) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid, eventId: $"evt-race-diff-{i}");
            return s.Send(new HandlePaymentWebhookCommand("fake", body, headers));
        }));

        Assert.Empty(errors);
        Assert.All(results, r => Assert.Equal(WebhookResultKind.Accepted, r.Kind));
        Assert.Equal(1, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
        var payment = await rig.PaymentAsync(placed.OrderNumber);
        Assert.Equal((PaymentStatuses.Paid, null), (payment.StatusId, payment.ReviewReasonId));
        var hooks = await rig.WebhooksAsync(payment.Id);
        Assert.Equal(1, hooks.Count(h => h.OutcomeId == WebhookOutcomes.Applied));
        Assert.Equal(Parallel - 1, hooks.Count(h => h.OutcomeId == WebhookOutcomes.Ignored));
    }

    [Fact]
    [Trait("Spec", "PAY-3")]
    public async Task FortyPayClicksAtOnce_OpenOneAttempt_AndEveryoneGetsTheSamePaymentPage()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceOnlineAsync();

        var (results, errors) = await RaceAsync(Parallel, _ => rig.InScopeAsync(s => s.Send(new StartOnlinePaymentCommand(placed.Token))));

        Assert.Empty(errors);
        Assert.All(results, r => Assert.True(r.Kind is StartPaymentKind.Started or StartPaymentKind.Reused, r.Kind.ToString()));
        Assert.Single(results.Select(r => r.RedirectUrl).Distinct());
        Assert.Single(rig.Gateway.Sessions);
        Assert.Single((await rig.PaymentAsync(placed.OrderNumber)).Attempts);
        output.WriteLine($"{rig.Gateway.SessionCreateCalls} gateway calls for {Parallel} clicks, one session, one attempt.");
    }

    [Fact]
    [Trait("Spec", "FR-30")]
    public async Task TheWebhook_TheReconciliationJob_AndTheExpiryJob_RacingForOnePaidOrder_ConfirmItOnceAndNeverCancelIt()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        rig.Gateway.SetOutcome(placed.GatewayRef!, GatewayOutcome.Paid);
        rig.Advance(TimeSpan.FromMinutes(31));
        var (body, headers) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid, eventId: "evt-three-way");

        var (_, errors) = await RaceAsync(24, i => (i % 3) switch
        {
            0 => rig.InScopeAsync(async s => (object)await s.Send(new HandlePaymentWebhookCommand("fake", body, headers))),
            1 => rig.InScopeAsync(async s => (object)await s.Send(new ReconcilePaymentsCommand())),
            _ => rig.InScopeAsync(async s => (object)await s.Send(new ExpireUnpaidOrdersCommand())),
        });

        // A conflict that rolled one back is safe: the next run (or the gateway's retry) finds the order already paid.
        foreach (var error in errors)
        {
            output.WriteLine("rolled back safely: " + error.GetType().Name);
        }

        await rig.InScopeAsync(async s => (object)await s.Send(new HandlePaymentWebhookCommand("fake", body, headers)));
        await rig.InScopeAsync(async s => (object)await s.Send(new ReconcilePaymentsCommand()));
        Assert.Equal(OrderStatuses.Paid, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        Assert.Equal(1, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
        Assert.Equal(0, await rig.EventCountAsync("OrderCancelledEvent", placed.OrderNumber));
        Assert.Equal(PaymentStatuses.Paid, (await rig.PaymentAsync(placed.OrderNumber)).StatusId);
    }

    [Fact]
    [Trait("Spec", "PAY-11")]
    public async Task APaymentArrivingWhileStaffCancelTheOrder_EndsConsistent_NeverPaidAndCancelled()
    {
        using var rig = new PaymentRig(postgres);
        for (var round = 0; round < 8; round++)
        {
            var placed = await rig.PlaceAndStartAsync();
            var (body, headers) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid, eventId: $"evt-cancel-race-{round}");

            await RaceAsync(2, i => i == 0
                ? rig.InScopeAsync(async s => (object)await s.Send(new HandlePaymentWebhookCommand("fake", body, headers)))
                : rig.InScopeAsync(async s => (object)await s.Send(new CancelOrderCommand(placed.OrderNumber, CancelReasons.Staff, ActorKinds.Staff, "staff-1", null))));

            // The gateway retries a message that failed with a conflict; the same happens here.
            await rig.InScopeAsync(async s => (object)await s.Send(new HandlePaymentWebhookCommand("fake", body, headers)));

            var order = await rig.OrderAsync(placed.OrderNumber);
            var payment = await rig.PaymentAsync(placed.OrderNumber);
            var paidEvents = await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber);
            var cancelEvents = await rig.EventCountAsync("OrderCancelledEvent", placed.OrderNumber);
            Assert.True(paidEvents <= 1, $"round {round}: {paidEvents} paid events");
            if (order.StatusId == OrderStatuses.Paid)
            {
                Assert.Equal((PaymentStatuses.Paid, 1, 0), (payment.StatusId, paidEvents, cancelEvents));
            }
            else
            {
                // Staff cancelled. Either the cancel came first and the late money is held for review, or the payment came first and a refund
                // is now due (a paid, undispatched order may be cancelled): the order is never left half way.
                Assert.Equal(OrderStatuses.Cancelled, order.StatusId);
                Assert.Equal(1, cancelEvents);
                if (paidEvents == 0)
                {
                    Assert.Equal((PaymentStatuses.InReview, PaymentReviewReasons.OrderNotPayable), (payment.StatusId, payment.ReviewReasonId));
                }
                else
                {
                    Assert.Equal(PaymentStatuses.Paid, payment.StatusId);
                }
            }
        }
    }

    [Theory]
    [Trait("Spec", "PAY-3")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task ARandomRunOfEveryOperation_NeverCountsTheMoneyTwice(int seed)
    {
        var random = new Random(seed);
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceOnlineAsync();
        var used = new List<string>();

        for (var step = 0; step < 16; step++)
        {
            var open = await rig.OpenRefAsync(placed.OrderNumber);
            var all = (await rig.PaymentAsync(placed.OrderNumber)).Attempts.Select(a => a.GatewayRef).ToList();
            var pick = all.Count == 0 ? "none" : all[random.Next(all.Count)];
            switch (random.Next(9))
            {
                case 0:
                case 1:
                    await rig.Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));
                    break;
                case 2:
                    await DeliverAsync(rig, pick, GatewayOutcome.Paid, used, repeat: random.Next(2) == 0);
                    break;
                case 3:
                    await DeliverAsync(rig, pick, GatewayOutcome.Failed, used, repeat: random.Next(2) == 0);
                    break;
                case 4 when open is not null:
                    rig.Gateway.SetOutcome(open, random.Next(2) == 0 ? GatewayOutcome.Paid : GatewayOutcome.Failed);
                    break;
                case 5:
                    rig.Advance(TimeSpan.FromMinutes(random.Next(0, 15)));
                    await rig.Host.SendAsync(new ReconcilePaymentsCommand());
                    break;
                case 6:
                    rig.Advance(TimeSpan.FromMinutes(random.Next(0, 20)));
                    await rig.Host.SendAsync(new ExpireUnpaidOrdersCommand());
                    break;
                case 7:
                    await DeliverAsync(rig, pick, GatewayOutcome.Paid, used, repeat: true, amount: 1m);
                    break;
                default:
                    rig.Advance(TimeSpan.FromMinutes(random.Next(1, 8)));
                    break;
            }

            await AssertInvariantsAsync(rig, placed.OrderNumber, $"seed {seed} step {step}");
        }
    }

    private static async Task DeliverAsync(PaymentRig rig, string reference, GatewayOutcome outcome, List<string> used, bool repeat, decimal? amount = null)
    {
        if (reference == "none")
        {
            return;
        }

        var eventId = repeat && used.Count > 0 ? used[^1] : Guid.NewGuid().ToString("N");
        used.Add(eventId);
        await rig.DeliverAsync(reference, outcome, amount ?? (outcome == GatewayOutcome.Failed ? 0m : null), eventId, rig.Host.Time.Now);
    }

    private static async Task AssertInvariantsAsync(PaymentRig rig, string orderNumber, string where)
    {
        var order = await rig.OrderAsync(orderNumber);
        var payment = await rig.PaymentAsync(orderNumber);
        var paid = await rig.EventCountAsync("OrderPaidEvent", orderNumber);
        var cancelled = await rig.EventCountAsync("OrderCancelledEvent", orderNumber);
        var paidAttempts = payment.Attempts.Count(a => a.StatusId == PaymentStatuses.Paid);

        Assert.True(paid <= 1, $"{where}: {paid} paid events");
        Assert.True(payment.Attempts.Count(a => a.StatusId == PaymentStatuses.Pending) <= 1, $"{where}: more than one open try");
        Assert.True(paidAttempts <= 1 || payment.ReviewReasonId is not null, $"{where}: {paidAttempts} paid attempts and nothing flagged");
        Assert.False(paid > 0 && cancelled > 0, $"{where}: the order was both paid and cancelled");
        if (paid == 1)
        {
            Assert.Equal(OrderStatuses.Paid, order.StatusId);
            Assert.Equal(PaymentStatuses.Paid, payment.StatusId);
        }

        if (order.StatusId == OrderStatuses.Paid)
        {
            Assert.Equal(PaymentStatuses.Paid, payment.StatusId);
            Assert.Equal(1, paid);
        }

        if (payment.StatusId == PaymentStatuses.Paid)
        {
            Assert.Equal(Total, payment.Amount);
            Assert.NotNull(payment.PaidAt);
        }
    }

    private const decimal Total = 3150m;
}
