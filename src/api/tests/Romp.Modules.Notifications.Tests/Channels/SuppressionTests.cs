using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Channels;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Channels;
using Romp.Modules.Notifications.Tests.Support;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Tests.Channels;

/// <summary>STOP stops WhatsApp at once and keeps no phone number (CHN-6).</summary>
public sealed class SuppressionTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private static readonly WhatsAppSettings Settings = new() { NumberSalt = "test-salt" };

    public async Task InitializeAsync()
    {
        await postgres.ResetAsync();
        await using var db = postgres.NewContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE \"NTFY\".\"NTFY_BLCK\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class OptedInOrders(string mobile) : IOrderNotificationQueries
    {
        public Task<OrderNotificationView?> FindAsync(long orderId, CancellationToken cancellationToken) =>
            Task.FromResult<OrderNotificationView?>(new OrderNotificationView(orderId, "RMP-0001", "COD", "A", "a@example.com", mobile, true, 100m, []));
    }

    private async Task StopAsync(string number)
    {
        await using var db = postgres.NewContext();
        await new StopHandler(db, Settings, new TestClock(Now)).HandleAsync(
            new WhatsAppInboundEvent(Guid.NewGuid().ToString("N"), WhatsAppEventType.Stop, number, null, null), CancellationToken.None);
    }

    [Theory]
    [InlineData("03331234567")]
    [InlineData("+923331234567")]
    [InlineData("92 333 1234567")]
    [Trait("Spec", "CHN-6")]
    public void Hash_TreatsEveryWayOfWritingANumberAsOneNumber(string written)
    {
        Assert.Equal(NumberHasher.Hash("s", "+923331234567"), NumberHasher.Hash("s", written));
        Assert.NotEqual(NumberHasher.Hash("s", "+923331234567"), NumberHasher.Hash("other", "+923331234567"));
    }

    [Fact]
    [Trait("Spec", "CHN-6")]
    public async Task Stop_StoresASaltedHashAndNeverTheNumber_AndSayingItTwiceChangesNothing()
    {
        await StopAsync("+923331234567");
        await StopAsync("03331234567");

        await using var db = postgres.NewContext();
        var row = await db.BlockedNumbers.SingleAsync();
        Assert.Equal(NumberHasher.Hash("test-salt", "+923331234567"), row.NumberHash);
        Assert.DoesNotContain("3331234567", row.NumberHash, StringComparison.Ordinal);
        Assert.True(await new SuppressionList(db, Settings).IsBlockedAsync("+92 333 1234567", CancellationToken.None));
        Assert.False(await new SuppressionList(db, Settings).IsBlockedAsync("+923009999999", CancellationToken.None));
    }

    [Fact]
    [Trait("Spec", "CHN-6")]
    public async Task NewNotification_ForANumberThatSaidStop_GetsEmailOnlyAndRecordsWhy()
    {
        await StopAsync("+923331234567");
        await using var db = postgres.NewContext();
        var create = new CreateNotification(db, new OptedInOrders("+923331234567"), new SuppressionList(db, Settings), new TestClock(Now));

        var notification = await create.HandleAsync(new NotificationIntent(NotificationKind.OrderConfirmation, 1, Guid.NewGuid()), CancellationToken.None);

        Assert.NotNull(notification);
        Assert.Equal(DeliveryStatus.Queued, notification.Deliveries.Single(d => d.Channel == NotificationChannel.Email).Status);
        var whatsApp = notification.Deliveries.Single(d => d.Channel == NotificationChannel.WhatsApp);
        Assert.Equal((DeliveryStatus.Skipped, ErrorCategory.OptedOut), (whatsApp.Status, whatsApp.LastError!.Value));
    }

    [Fact]
    [Trait("Spec", "CHN-6")]
    public async Task Worker_WhenTheNumberSaidStopAfterPlanning_SendsNoWhatsAppAndStillSendsEmail()
    {
        long notificationId;
        await using (var seed = postgres.NewContext())
        {
            var n = new Notification(
                NotificationKind.OrderConfirmation, ReferenceKinds.Order, 55, Guid.NewGuid(), Now,
                NotificationPolicy.ValidFor(NotificationKind.OrderConfirmation), NotificationPolicy.PriorityOf(NotificationKind.OrderConfirmation));
            n.Plan(ChannelPlan.For(true, false), Now);
            seed.Notifications.Add(n);
            await seed.SaveChangesAsync();
            notificationId = n.Id;
        }

        await StopAsync("+923331234567");
        var email = new ScriptedSender();
        var whatsApp = new FakeWhatsAppSender();
        await using (var db = postgres.NewContext())
        {
            var processor = DeliveryTestKit.Processor(
                db, new TestClock(Now), [email, new WhatsAppChannelSender(whatsApp)], new OptedInOrders("+923331234567"), new SuppressionList(db, Settings));
            await processor.RunOnceAsync(CancellationToken.None);
        }

        Assert.Empty(whatsApp.Sent);
        Assert.Single(email.Sent);
        await using var check = postgres.NewContext();
        var deliveries = await check.Deliveries.AsNoTracking().Where(d => d.NotificationId == notificationId).ToDictionaryAsync(d => d.Channel);
        Assert.Equal(DeliveryStatus.Skipped, deliveries[NotificationChannel.WhatsApp].Status);
        Assert.Equal(DeliveryStatus.Sent, deliveries[NotificationChannel.Email].Status);
    }
}
