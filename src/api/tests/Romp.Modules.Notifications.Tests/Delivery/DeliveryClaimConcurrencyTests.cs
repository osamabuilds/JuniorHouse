using Romp.Modules.Notifications.Infrastructure.Delivery;
using Romp.Modules.Notifications.Tests.Support;

namespace Romp.Modules.Notifications.Tests.Delivery;

public sealed class DeliveryClaimConcurrencyTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>, IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<IReadOnlyList<long>> ClaimAsync(DateTimeOffset now, int batch)
    {
        await using var db = postgres.NewContext();
        return await new SkipLockedDeliveryClaimer(db).ClaimAsync(now, batch, TimeSpan.FromMinutes(2), CancellationToken.None);
    }

    [Fact]
    [Trait("Spec", "DLV-9")]
    [Trait("Spec", "NFR-FT-05")]
    public async Task Claim_TwoWorkersAtOnce_NeverHoldTheSameDelivery()
    {
        await using var seed = postgres.NewContext();
        var all = await DeliveryTestKit.SeedAsync(seed, 40, Start);

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ClaimAsync(Start, 15)));

        var claimed = results.SelectMany(r => r).ToList();
        Assert.Equal(claimed.Count, claimed.Distinct().Count());
        Assert.All(results, r => Assert.True(r.Count <= 15));
        Assert.All(claimed, id => Assert.Contains(id, all));
        Assert.True(claimed.Count >= 15);
    }

    [Fact]
    [Trait("Spec", "DLV-9")]
    public async Task Claim_AHeldDelivery_IsNotTakenAgainUntilItsLeaseRunsOut()
    {
        await using var seed = postgres.NewContext();
        await DeliveryTestKit.SeedAsync(seed, 3, Start);

        var first = await ClaimAsync(Start, 10);
        var during = await ClaimAsync(Start.AddMinutes(1), 10);
        var after = await ClaimAsync(Start.AddMinutes(3), 10);

        Assert.Equal(3, first.Count);
        Assert.Empty(during);
        Assert.Equal(3, after.Count);
    }

    [Fact]
    [Trait("Spec", "DLV-9")]
    public async Task Claim_BeforeTheyAreDue_TakesNothing()
    {
        await using var seed = postgres.NewContext();
        await DeliveryTestKit.SeedAsync(seed, 2, Start);

        Assert.Empty(await ClaimAsync(Start.AddMinutes(-5), 10));
    }
}
