using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Auditing;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Application.Payments;
using Romp.Modules.Orders.Domain.Payments;
using Romp.Modules.Orders.Infrastructure.Payments;
using Romp.Modules.Orders.Tests.Support;

namespace Romp.Modules.Orders.Tests.Payments;

/// <summary>
/// A flood of forged callbacks must not fill the database or starve real work (SEC-10, SEC-26, NFR-04). Found by the load test: every forged
/// call wrote a log row, and every row was audited into the outbox, which queued thousands of audit events ahead of real order events. Now the
/// log table is not audited, only a budget of rejected messages is stored a minute, and old rejected records are purged.
/// </summary>
public sealed class WebhookHygieneTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private static readonly DateTimeOffset Start = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Spec", "SEC-26")]
    public void TheWebhookLog_IsNotAudited_SoAFloodCannotFillTheAuditOutbox()
    {
        Assert.NotNull(typeof(PaymentWebhook).GetCustomAttribute<NotAuditedAttribute>());
        Assert.Null(typeof(Payment).GetCustomAttribute<NotAuditedAttribute>());
        Assert.Null(typeof(PaymentAttempt).GetCustomAttribute<NotAuditedAttribute>());
    }

    [Fact]
    [Trait("Spec", "SEC-26")]
    public void TheBudget_AllowsTheConfiguredNumberAMinute_ThenRefuses_ThenStartsAgain()
    {
        var time = new FixedTime(Start);
        var budget = new WebhookRejectionBudget(time, Options.Create(new OrdersOptions { RejectedWebhooksStoredPerMinute = 5 }));

        var first = Enumerable.Range(0, 8).Select(_ => budget.TryTake()).ToList();
        time.Now += TimeSpan.FromSeconds(30);
        var sameMinute = budget.TryTake();
        time.Now += TimeSpan.FromSeconds(31);
        var nextMinute = budget.TryTake();

        Assert.Equal([true, true, true, true, true, false, false, false], first);
        Assert.False(sameMinute);
        Assert.True(nextMinute);
    }

    [Fact]
    [Trait("Spec", "SEC-26")]
    public async Task TheBudget_IsSafeUnderManyCallersAtOnce()
    {
        var budget = new WebhookRejectionBudget(new FixedTime(Start), Options.Create(new OrdersOptions { RejectedWebhooksStoredPerMinute = 100 }));

        var results = await Task.WhenAll(Enumerable.Range(0, 1000).Select(_ => Task.Run(budget.TryTake)));

        Assert.Equal(100, results.Count(r => r));
    }

    [Fact]
    [Trait("Spec", "SEC-26")]
    public async Task AFloodOfForgedCallbacks_IsAllRejected_ButOnlyABudgetOfThemIsStored()
    {
        using var rig = new PaymentRig(postgres);
        rig.Host.Options.RejectedWebhooksStoredPerMinute = 25;
        var (body, _) = rig.Gateway.BuildCallback("fk_x", GatewayOutcome.Paid);
        var forged = new Dictionary<string, string> { [FakeGateway.SignatureHeader] = $"t={OrdersHost.Start.ToUnixTimeSeconds()},v1={new string('c', 64)}" };
        await using var before = postgres.NewContext();
        var storedBefore = await before.PaymentWebhooks.CountAsync(w => w.OutcomeId == WebhookOutcomes.Rejected);

        var results = new List<WebhookResult>();
        for (var i = 0; i < 200; i++)
        {
            results.Add(await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", body, forged)));
        }

        Assert.All(results, r => Assert.Equal(WebhookResultKind.Rejected, r.Kind));
        await using var after = postgres.NewContext();
        Assert.Equal(25, await after.PaymentWebhooks.CountAsync(w => w.OutcomeId == WebhookOutcomes.Rejected) - storedBefore);
    }

    [Fact]
    [Trait("Spec", "SEC-26")]
    public async Task AGenuineMessage_IsNeverHeldBackByTheRejectionBudget()
    {
        using var rig = new PaymentRig(postgres);
        rig.Host.Options.RejectedWebhooksStoredPerMinute = 1;
        var placed = await rig.PlaceAndStartAsync();
        var (body, _) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid);
        var forged = new Dictionary<string, string> { [FakeGateway.SignatureHeader] = $"t={OrdersHost.Start.ToUnixTimeSeconds()},v1={new string('d', 64)}" };
        for (var i = 0; i < 10; i++)
        {
            await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", body, forged));
        }

        var genuine = await rig.DeliverAsync(placed.GatewayRef!, GatewayOutcome.Paid);

        Assert.Equal(WebhookResultKind.Accepted, genuine.Kind);
        Assert.Equal(Romp.Modules.Orders.Domain.Orders.OrderStatuses.Paid, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
    }

    [Fact]
    [Trait("Spec", "SEC-26")]
    public async Task OldRejectedRecords_ArePurged_ButVerifiedOnesAndRecentOnesAreKept()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        await rig.DeliverAsync(placed.GatewayRef!, GatewayOutcome.Paid);
        var now = rig.Host.Time.Now;
        await using (var db = postgres.NewContext())
        {
            string Hash() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            db.PaymentWebhooks.Add(new PaymentWebhook("fake", null, false, Hash(), WebhookOutcomes.Rejected, null, null, now.AddDays(-45)));
            db.PaymentWebhooks.Add(new PaymentWebhook("fake", null, false, Hash(), WebhookOutcomes.Rejected, null, null, now.AddDays(-31)));
            db.PaymentWebhooks.Add(new PaymentWebhook("fake", null, false, Hash(), WebhookOutcomes.Rejected, null, null, now.AddDays(-2)));
            db.PaymentWebhooks.Add(new PaymentWebhook("fake", "old-verified-" + Guid.NewGuid().ToString("N"), true, Hash(), WebhookOutcomes.Unmatched, "fk_old", null, now.AddDays(-90)));
            await db.SaveChangesAsync();
        }

        var purged = await rig.Host.SendAsync(new PurgePaymentWebhooksCommand());

        Assert.True(purged >= 2);
        await using var check = postgres.NewContext();
        Assert.Equal(0, await check.PaymentWebhooks.CountAsync(w => w.OutcomeId == WebhookOutcomes.Rejected && w.ReceivedAt < now.AddDays(-30)));
        Assert.True(await check.PaymentWebhooks.AnyAsync(w => w.OutcomeId == WebhookOutcomes.Rejected && w.ReceivedAt > now.AddDays(-3)));
        Assert.True(await check.PaymentWebhooks.AnyAsync(w => w.OutcomeId == WebhookOutcomes.Unmatched && w.ReceivedAt < now.AddDays(-80)));
        Assert.True(await check.PaymentWebhooks.AnyAsync(w => w.OutcomeId == WebhookOutcomes.Applied));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100000, true)]
    [InlineData(100001, false)]
    public void TheBudgetSetting_MustBeSensible(int perMinute, bool valid)
    {
        Assert.Equal(valid, OrdersOptions.IsValid(new OrdersOptions { RejectedWebhooksStoredPerMinute = perMinute }));
    }
}
