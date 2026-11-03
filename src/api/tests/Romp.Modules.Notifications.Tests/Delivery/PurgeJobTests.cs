using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Tests.Support;

namespace Romp.Modules.Notifications.Tests.Delivery;

public sealed class PurgeJobTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Saves notifications, then backdates them (the audit interceptor is not part of these test contexts).</summary>
    private async Task<long[]> SeedAsync(int count, DateTimeOffset createdAt, DeliveryStatusCode status)
    {
        await using var db = postgres.NewContext();
        var ids = await DeliveryTestKit.SeedAsync(db, count, createdAt);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"NTFY\".\"NTFY_MAIN\" SET \"INSR_DTE\" = {0} WHERE \"ID\" IN (SELECT \"NTFY_ID\" FROM \"NTFY\".\"NTFY_CHNL\" WHERE \"ID\" = ANY({1}))",
            createdAt, ids);
        await db.Database.ExecuteSqlRawAsync("UPDATE \"NTFY\".\"NTFY_CHNL\" SET \"CHNL_STS_ID\" = {0} WHERE \"ID\" = ANY({1})", (short)status, ids);
        return ids;
    }

    private enum DeliveryStatusCode : short
    {
        Queued = 1,
        Sent = 2,
    }

    private async Task<int> PurgeAsync(int batch)
    {
        await using var db = postgres.NewContext();
        return await new PurgeProcessor(db, new DeliverySettings { PurgeBatchSize = batch }, new TestClock(Now)).RunOnceAsync(CancellationToken.None);
    }

    [Fact]
    [Trait("Spec", "DLV-12")]
    public async Task Purge_DeletesFinishedNotificationsOlderThanTwelveMonths_InBatches()
    {
        await SeedAsync(5, Now.AddMonths(-13), DeliveryStatusCode.Sent);

        var deleted = await PurgeAsync(batch: 2);

        Assert.Equal(5, deleted);
        await using var check = postgres.NewContext();
        Assert.Equal(0, await check.Notifications.CountAsync());
        Assert.Equal(0, await check.Set<ChannelDelivery>().CountAsync());
    }

    [Fact]
    [Trait("Spec", "DLV-12")]
    public async Task Purge_KeepsRecentNotifications_AndOldOnesStillWaitingToBeSent()
    {
        await SeedAsync(2, Now.AddMonths(-2), DeliveryStatusCode.Sent);
        var waiting = await SeedAsync(1, Now.AddMonths(-14), DeliveryStatusCode.Queued);

        var deleted = await PurgeAsync(batch: 10);

        Assert.Equal(0, deleted);
        await using var check = postgres.NewContext();
        Assert.Equal(3, await check.Notifications.CountAsync());
        Assert.True(await check.Set<ChannelDelivery>().AnyAsync(d => d.Id == waiting[0]));
    }
}
