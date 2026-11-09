using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Domain.Channels;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Channels;
using Romp.Modules.Notifications.Tests.Support;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Tests.Channels;

/// <summary>Receipts move the WhatsApp status; the daily count shows the cost (CHN-14, CHN-15, DLG-10).</summary>
public sealed class ReceiptAndCountTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await postgres.ResetAsync();
        await using var db = postgres.NewContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE \"NTFY\".\"NTFY_DAY_CNT\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class OptedInOrders : IOrderNotificationQueries
    {
        public Task<OrderNotificationView?> FindAsync(long orderId, CancellationToken cancellationToken) =>
            Task.FromResult<OrderNotificationView?>(new OrderNotificationView(orderId, "RMP-0001", "COD", "A", "a@example.com", "+923331234567", true, 100m, []));
    }

    private async Task<(long EmailId, long WhatsAppId)> SeedSentAsync(string providerId)
    {
        await using var db = postgres.NewContext();
        var n = new Notification(
            NotificationKind.OrderConfirmation, ReferenceKinds.Order, 9, Guid.NewGuid(), Now,
            NotificationPolicy.ValidFor(NotificationKind.OrderConfirmation), NotificationPolicy.PriorityOf(NotificationKind.OrderConfirmation));
        n.Plan(ChannelPlan.For(true, false), Now);
        db.Notifications.Add(n);
        await db.SaveChangesAsync();
        var email = n.Deliveries.Single(d => d.Channel == NotificationChannel.Email);
        var whatsApp = n.Deliveries.Single(d => d.Channel == NotificationChannel.WhatsApp);
        email.MarkSent(null);
        whatsApp.MarkSent(providerId);
        await db.SaveChangesAsync();
        return (email.Id, whatsApp.Id);
    }

    private async Task<DeliveryStatus> ReceiptAsync(string providerId, string state, long deliveryId)
    {
        await using (var db = postgres.NewContext())
        {
            var matched = await new ReceiptHandler(db).HandleAsync(
                new WhatsAppInboundEvent($"{providerId}:{state}", WhatsAppEventType.Receipt, "+923331234567", providerId, state), CancellationToken.None);
            Assert.Equal(deliveryId, matched);
        }

        await using var check = postgres.NewContext();
        return (await DeliveryTestKit.LoadAsync(check, deliveryId)).Status;
    }

    [Theory]
    [InlineData("delivered")]
    [InlineData("read")]
    [Trait("Spec", "CHN-14")]
    [Trait("Spec", "DLG-10")]
    public async Task Receipt_DeliveredOrRead_MakesTheWhatsAppDeliveryDelivered_AndEmailStaysAtSent(string state)
    {
        var (emailId, whatsAppId) = await SeedSentAsync("wamid.R1");

        Assert.Equal(DeliveryStatus.Delivered, await ReceiptAsync("wamid.R1", state, whatsAppId));

        await using var check = postgres.NewContext();
        Assert.Equal(DeliveryStatus.Sent, (await DeliveryTestKit.LoadAsync(check, emailId)).Status);
    }

    [Fact]
    [Trait("Spec", "CHN-14")]
    public async Task Receipt_RepeatedOrOutOfOrder_ChangesNothingMore()
    {
        var (_, whatsAppId) = await SeedSentAsync("wamid.R2");

        await ReceiptAsync("wamid.R2", "delivered", whatsAppId);
        await ReceiptAsync("wamid.R2", "sent", whatsAppId);
        var afterRead = await ReceiptAsync("wamid.R2", "read", whatsAppId);
        var afterLateFailed = await ReceiptAsync("wamid.R2", "failed", whatsAppId);

        Assert.Equal(DeliveryStatus.Delivered, afterRead);
        Assert.Equal(DeliveryStatus.Delivered, afterLateFailed);
    }

    [Fact]
    [Trait("Spec", "CHN-14")]
    public async Task Receipt_Failed_MarksTheSentWhatsAppDeliveryFailed_AndLeavesEmailAlone()
    {
        var (emailId, whatsAppId) = await SeedSentAsync("wamid.R3");

        var status = await ReceiptAsync("wamid.R3", "failed", whatsAppId);

        Assert.Equal(DeliveryStatus.Failed, status);
        await using var check = postgres.NewContext();
        Assert.Equal(DeliveryStatus.Sent, (await DeliveryTestKit.LoadAsync(check, emailId)).Status);
        Assert.Equal(ErrorCategory.Permanent, (await DeliveryTestKit.LoadAsync(check, whatsAppId)).LastError);
    }

    [Fact]
    [Trait("Spec", "CHN-14")]
    public async Task Receipt_ForAMessageOfNoOurs_IsIgnored()
    {
        await using var db = postgres.NewContext();

        var matched = await new ReceiptHandler(db).HandleAsync(
            new WhatsAppInboundEvent("x:delivered", WhatsAppEventType.Receipt, "+923331234567", "wamid.UNKNOWN", "delivered"), CancellationToken.None);

        Assert.Null(matched);
    }

    [Fact]
    [Trait("Spec", "CHN-15")]
    public async Task Worker_CountsEachSendPerDayAndChannel()
    {
        await using (var seed = postgres.NewContext())
        {
            await DeliveryTestKit.SeedAsync(seed, 3, Now);
        }

        var whatsApp = new FakeWhatsAppSender();
        await using (var db = postgres.NewContext())
        {
            await DeliveryTestKit.Processor(db, new TestClock(Now), [new ScriptedSender(), new WhatsAppChannelSender(whatsApp)], new OptedInOrders())
                .RunOnceAsync(CancellationToken.None);
        }

        await using var check = postgres.NewContext();
        var counter = new SqlDailySendCounter(check, new TestClock(Now));
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        Assert.Equal(3, await counter.CountAsync(today, NotificationChannel.Email, CancellationToken.None));
        Assert.Equal(0, await counter.CountAsync(today, NotificationChannel.WhatsApp, CancellationToken.None));
        Assert.Equal(0, await counter.CountAsync(today.AddDays(-1), NotificationChannel.Email, CancellationToken.None));
    }

    [Fact]
    [Trait("Spec", "CHN-15")]
    public async Task Counter_ConcurrentIncrements_NeverLoseACount()
    {
        var today = DateOnly.FromDateTime(Now.UtcDateTime);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var db = postgres.NewContext();
            await new SqlDailySendCounter(db, new TestClock(Now)).IncrementAsync(today, NotificationChannel.WhatsApp, CancellationToken.None);
        }));

        await using var check = postgres.NewContext();
        Assert.Equal(20, await new SqlDailySendCounter(check, new TestClock(Now)).CountAsync(today, NotificationChannel.WhatsApp, CancellationToken.None));
    }
}
