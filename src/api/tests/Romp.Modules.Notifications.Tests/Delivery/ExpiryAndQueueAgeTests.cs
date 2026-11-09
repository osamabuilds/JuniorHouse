using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Delivery;
using Romp.Modules.Notifications.Tests.Support;

namespace Romp.Modules.Notifications.Tests.Delivery;

public sealed class ExpiryTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>, IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(NotificationKind.OrderConfirmation, 8 * 24)]
    [InlineData(NotificationKind.OrderCancelled, 25)]
    [InlineData(NotificationKind.CodConfirmationRequest, 25)]
    [Trait("Spec", "DLV-10")]
    public async Task Run_WhenTheMessageIsTooLate_ExpiresItAndSendsNothing(NotificationKind kind, int hoursLate)
    {
        var sender = new ScriptedSender();
        await using var seed = postgres.NewContext();
        var id = (await DeliveryTestKit.SeedAsync(seed, 1, Start, kind))[0];

        await using (var db = postgres.NewContext())
        {
            await DeliveryTestKit.Processor(db, new TestClock(Start.AddHours(hoursLate)), [sender]).RunOnceAsync(CancellationToken.None);
        }

        await using var check = postgres.NewContext();
        var delivery = await DeliveryTestKit.LoadAsync(check, id);
        Assert.Equal(DeliveryStatus.Expired, delivery.Status);
        Assert.Equal(0, sender.Calls);
    }

    [Fact]
    [Trait("Spec", "DLV-10")]
    public async Task Run_WithinTheValidPeriod_StillSends()
    {
        var sender = new ScriptedSender();
        await using var seed = postgres.NewContext();
        var id = (await DeliveryTestKit.SeedAsync(seed, 1, Start, NotificationKind.OrderConfirmation))[0];

        await using (var db = postgres.NewContext())
        {
            await DeliveryTestKit.Processor(db, new TestClock(Start.AddDays(6)), [sender]).RunOnceAsync(CancellationToken.None);
        }

        await using var check = postgres.NewContext();
        Assert.Equal(DeliveryStatus.Sent, (await DeliveryTestKit.LoadAsync(check, id)).Status);
    }
}

public sealed class QueueAgeHealthTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>, IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(QueueSnapshot Snapshot, HealthCheckResult Health)> ReadAsync(TimeSpan after)
    {
        await using var db = postgres.NewContext();
        var queue = new QueueHealth(db, new TestClock(Start + after));
        var check = new QueueAgeHealthCheck(queue, Options.Create(new DeliverySettings()));
        return (await queue.ReadAsync(CancellationToken.None), await check.CheckHealthAsync(new HealthCheckContext()));
    }

    [Fact]
    [Trait("Spec", "DLV-11")]
    public async Task Read_WithAnEmptyQueue_IsHealthyWithZeroAge()
    {
        var (snapshot, health) = await ReadAsync(TimeSpan.FromHours(1));

        Assert.Equal(TimeSpan.Zero, snapshot.OldestDueAge);
        Assert.Equal(HealthStatus.Healthy, health.Status);
    }

    [Fact]
    [Trait("Spec", "DLV-11")]
    public async Task Read_WhenTheOldestDueDeliveryHasWaitedPastTheLimit_IsDegraded()
    {
        await using var seed = postgres.NewContext();
        await DeliveryTestKit.SeedAsync(seed, 3, Start);

        var (snapshot, health) = await ReadAsync(TimeSpan.FromMinutes(20));

        Assert.Equal(TimeSpan.FromMinutes(20), snapshot.OldestDueAge);
        Assert.Equal(3, snapshot.QueuedCount);
        Assert.Equal(HealthStatus.Degraded, health.Status);
    }

    [Fact]
    [Trait("Spec", "DLV-11")]
    public async Task Read_WhenTheWaitIsShort_IsHealthy()
    {
        await using var seed = postgres.NewContext();
        await DeliveryTestKit.SeedAsync(seed, 1, Start);

        var (snapshot, health) = await ReadAsync(TimeSpan.FromMinutes(5));

        Assert.Equal(TimeSpan.FromMinutes(5), snapshot.OldestDueAge);
        Assert.Equal(HealthStatus.Healthy, health.Status);
    }
}
