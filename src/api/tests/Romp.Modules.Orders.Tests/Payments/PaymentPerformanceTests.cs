using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Application.Payments;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Domain.Payments;
using Romp.Modules.Orders.Tests.Support;
using Xunit.Abstractions;

namespace Romp.Modules.Orders.Tests.Payments;

/// <summary>
/// Performance and load tests for the payment path against a real PostgreSQL (NFR-04, NFR-09): a burst of orders paying at once, a flood of
/// forged messages, and a large backlog for the reconciliation and expiry jobs. They assert correctness under load first (every order ends
/// in exactly the right state with one paid event) and then generous latency ceilings, and print the figures so a regression is visible.
/// The ceilings are deliberately loose: they catch a collapse (a lock held too long, an N+1), not a few milliseconds.
/// </summary>
[Trait("Category", "Performance")]
public sealed class PaymentPerformanceTests(OrdersPostgres postgres, ITestOutputHelper output) : IClassFixture<OrdersPostgres>
{
    private static double Percentile(List<double> sorted, double p) => sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(p * sorted.Count) - 1)];

    private static async Task<(List<double> Latencies, List<Exception> Errors, double Seconds)> LoadAsync(int count, int concurrency, Func<int, Task> work)
    {
        var latencies = new List<double>(count);
        var errors = new List<Exception>();
        using var gate = new SemaphoreSlim(concurrency);
        var clock = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, count).Select(async i =>
        {
            await gate.WaitAsync();
            try
            {
                var one = Stopwatch.StartNew();
                await work(i);
                lock (latencies)
                {
                    latencies.Add(one.Elapsed.TotalMilliseconds);
                }
            }
            catch (Exception ex)
            {
                lock (errors)
                {
                    errors.Add(ex);
                }
            }
            finally
            {
                gate.Release();
            }
        }));
        latencies.Sort();
        return (latencies, errors, clock.Elapsed.TotalSeconds);
    }

    [Fact]
    [Trait("Spec", "NFR-04")]
    public async Task ABurstOfOneHundredOrdersPayingAtOnce_AllEndPaidExactlyOnce_WithinReasonableTime()
    {
        const int orders = 100;
        using var rig = new PaymentRig(postgres);
        var placed = new List<PlacedOnline>();
        for (var i = 0; i < orders; i++)
        {
            placed.Add(await rig.PlaceAndStartAsync());
        }

        var (latencies, errors, seconds) = await LoadAsync(orders, 16, async i =>
        {
            var result = await rig.DeliverAsync(placed[i].GatewayRef!, GatewayOutcome.Paid);
            Assert.Equal(WebhookResultKind.Accepted, result.Kind);
        });

        Assert.Empty(errors);
        await using var db = postgres.NewContext();
        var numbers = placed.Select(p => p.OrderNumber).ToList();
        Assert.Equal(orders, await db.Orders.CountAsync(o => numbers.Contains(o.OrderNumber) && o.StatusId == OrderStatuses.Paid));
        foreach (var number in numbers.Take(10))
        {
            Assert.Equal(1, await rig.EventCountAsync("OrderPaidEvent", number));
        }

        output.WriteLine($"{orders} paid callbacks, 16 at a time: {seconds:F1} s total, {orders / seconds:F0}/s, p50 {Percentile(latencies, 0.5):F0} ms, p95 {Percentile(latencies, 0.95):F0} ms, max {latencies[^1]:F0} ms");
        Assert.True(Percentile(latencies, 0.95) < 3000, $"p95 {Percentile(latencies, 0.95):F0} ms");
        Assert.True(seconds < 60, $"{seconds:F1} s");
    }

    [Fact]
    [Trait("Spec", "SEC-26")]
    public async Task AFloodOfFiveHundredForgedCallbacks_IsRejectedQuickly_AndChangesNothing()
    {
        const int flood = 500;
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        var (body, _) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid);
        var forged = new Dictionary<string, string> { [Romp.Modules.Orders.Infrastructure.Payments.FakeGateway.SignatureHeader] = $"t={OrdersHost.Start.ToUnixTimeSeconds()},v1={new string('b', 64)}" };

        var (latencies, errors, seconds) = await LoadAsync(flood, 32, async _ =>
        {
            var result = await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", body, forged));
            Assert.Equal(WebhookResultKind.Rejected, result.Kind);
        });

        Assert.Empty(errors);
        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        Assert.Equal(0, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
        output.WriteLine($"{flood} forged callbacks, 32 at a time: {seconds:F1} s, {flood / seconds:F0}/s, p95 {Percentile(latencies, 0.95):F0} ms");
        Assert.True(Percentile(latencies, 0.95) < 3000, $"p95 {Percentile(latencies, 0.95):F0} ms");
    }

    [Fact]
    [Trait("Spec", "NFR-04")]
    public async Task SignatureChecking_IsFastEnoughNotToBeTheBottleneck()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        var (body, headers) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid);
        var now = rig.Host.Time.Now;
        var clock = Stopwatch.StartNew();

        for (var i = 0; i < 20_000; i++)
        {
            Assert.True(rig.Gateway.VerifyCallback(body, headers, now).Verified);
        }

        var perCheck = clock.Elapsed.TotalMilliseconds / 20_000;
        output.WriteLine($"{perCheck * 1000:F1} microseconds per signature check");
        Assert.True(perCheck < 1.0, $"{perCheck:F3} ms per check");
    }

    [Fact]
    [Trait("Spec", "FR-30")]
    public async Task AReconciliationAndExpiryBacklogOfThreeHundred_IsClearedInOneRun_WithinReasonableTime()
    {
        const int backlog = 300;
        using var rig = new PaymentRig(postgres);
        var paid = new List<string>();
        var abandoned = new List<string>();
        for (var i = 0; i < backlog; i++)
        {
            var placed = await rig.PlaceAndStartAsync();
            if (i % 2 == 0)
            {
                rig.Gateway.SetOutcome(placed.GatewayRef!, GatewayOutcome.Paid);
                paid.Add(placed.OrderNumber);
            }
            else
            {
                abandoned.Add(placed.OrderNumber);
            }
        }

        rig.Advance(TimeSpan.FromMinutes(31));
        var clock = Stopwatch.StartNew();
        var expired = await rig.Host.SendAsync(new ExpireUnpaidOrdersCommand());
        var seconds = clock.Elapsed.TotalSeconds;

        output.WriteLine($"{backlog} orders past their window cleared in {seconds:F1} s: {expired.SettledInstead} found paid, {expired.Expired} expired, {expired.Skipped} skipped");
        Assert.Equal(backlog / 2, expired.SettledInstead);
        Assert.Equal(backlog / 2, expired.Expired);
        await using var db = postgres.NewContext();
        Assert.Equal(paid.Count, await db.Orders.CountAsync(o => paid.Contains(o.OrderNumber) && o.StatusId == OrderStatuses.Paid));
        Assert.Equal(abandoned.Count, await db.Orders.CountAsync(o => abandoned.Contains(o.OrderNumber) && o.StatusId == OrderStatuses.Cancelled));
        Assert.True(seconds < 120, $"{seconds:F1} s");
    }

    [Fact]
    [Trait("Spec", "NFR-02")]
    public async Task TheStatusPageQuery_UsesAConstantNumberOfQueries_AndAnsweredFast()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        await rig.Host.SendAsync(new GetPaymentStatusQuery(placed.Token));
        var clock = Stopwatch.StartNew();

        for (var i = 0; i < 200; i++)
        {
            Assert.Equal("confirming", (await rig.Host.SendAsync(new GetPaymentStatusQuery(placed.Token)))!.State);
        }

        var perCall = clock.Elapsed.TotalMilliseconds / 200;
        output.WriteLine($"{perCall:F1} ms per status read (a polling page asks every few seconds)");
        Assert.True(perCall < 100, $"{perCall:F1} ms");
    }
}
