using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Channels;
using Romp.Modules.Notifications.Tests.Support;

namespace Romp.Modules.Notifications.Tests.Channels;

/// <summary>Opt-in and fallback end to end through the worker: planner, per-channel rows, both senders (CHN-1, 2, 3, 10).</summary>
public sealed class ChannelFallbackTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>, IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<long> SeedAsync(bool optIn)
    {
        await using var db = postgres.NewContext();
        var notification = new Notification(
            NotificationKind.OrderConfirmation, ReferenceKinds.Order, 7001, Guid.NewGuid(), Start,
            NotificationPolicy.ValidFor(NotificationKind.OrderConfirmation), NotificationPolicy.PriorityOf(NotificationKind.OrderConfirmation));
        notification.Plan(ChannelPlan.For(optIn, numberSuppressed: false), Start);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        return notification.Id;
    }

    private async Task RunAsync(ScriptedSender email, FakeWhatsAppSender whatsApp)
    {
        await using var db = postgres.NewContext();
        var processor = DeliveryTestKit.Processor(db, new TestClock(Start), [email, new WhatsAppChannelSender(whatsApp)]);
        await processor.RunOnceAsync(CancellationToken.None);
    }

    private async Task<Dictionary<NotificationChannel, ChannelDelivery>> DeliveriesAsync(long notificationId)
    {
        await using var db = postgres.NewContext();
        return await db.Set<ChannelDelivery>().AsNoTracking().Where(d => d.NotificationId == notificationId)
            .ToDictionaryAsync(d => d.Channel);
    }

    [Fact]
    [Trait("Spec", "CHN-1")]
    public async Task Run_ForAnOptedInCustomer_SendsBothEmailAndWhatsApp()
    {
        var id = await SeedAsync(optIn: true);
        var email = new ScriptedSender();
        var whatsApp = new FakeWhatsAppSender();

        await RunAsync(email, whatsApp);

        var deliveries = await DeliveriesAsync(id);
        Assert.Equal(DeliveryStatus.Sent, deliveries[NotificationChannel.Email].Status);
        Assert.Equal(DeliveryStatus.Sent, deliveries[NotificationChannel.WhatsApp].Status);
        Assert.Single(email.Sent);
        Assert.Single(whatsApp.Sent);
    }

    [Fact]
    [Trait("Spec", "CHN-2")]
    public async Task Run_WithoutOptIn_NeverSendsWhatsApp()
    {
        var id = await SeedAsync(optIn: false);
        var email = new ScriptedSender();
        var whatsApp = new FakeWhatsAppSender();

        await RunAsync(email, whatsApp);

        var deliveries = await DeliveriesAsync(id);
        Assert.Equal(DeliveryStatus.Skipped, deliveries[NotificationChannel.WhatsApp].Status);
        Assert.Equal(ErrorCategory.OptedOut, deliveries[NotificationChannel.WhatsApp].LastError);
        Assert.Empty(whatsApp.Sent);
        Assert.Single(email.Sent);
    }

    [Theory]
    [InlineData(ErrorCategory.Permanent)]
    [InlineData(ErrorCategory.Temporary)]
    [Trait("Spec", "CHN-3")]
    [Trait("Spec", "CHN-10")]
    public async Task Run_WhenWhatsAppFails_EmailStillGoesAndEachChannelKeepsItsOwnStatus(ErrorCategory failure)
    {
        var id = await SeedAsync(optIn: true);
        var email = new ScriptedSender();
        var whatsApp = new FakeWhatsAppSender();
        whatsApp.FailNext(failure);

        await RunAsync(email, whatsApp);

        var deliveries = await DeliveriesAsync(id);
        Assert.Equal(DeliveryStatus.Sent, deliveries[NotificationChannel.Email].Status);
        Assert.Single(email.Sent);
        Assert.NotEqual(DeliveryStatus.Sent, deliveries[NotificationChannel.WhatsApp].Status);
        Assert.Equal(1, deliveries[NotificationChannel.WhatsApp].AttemptCount);
        Assert.Equal(failure, deliveries[NotificationChannel.WhatsApp].LastError);
    }

    [Fact]
    [Trait("Spec", "CHN-3")]
    public async Task Run_WhenTheCircuitIsOpenForWhatsApp_EmailStillGoesAndNoAttemptIsUsed()
    {
        var id = await SeedAsync(optIn: true);
        var email = new ScriptedSender();
        var whatsApp = new FakeWhatsAppSender();
        whatsApp.FailNext(ErrorCategory.CircuitOpen);

        await RunAsync(email, whatsApp);

        var deliveries = await DeliveriesAsync(id);
        Assert.Equal(DeliveryStatus.Sent, deliveries[NotificationChannel.Email].Status);
        Assert.Equal(DeliveryStatus.Queued, deliveries[NotificationChannel.WhatsApp].Status);
        Assert.Equal(0, deliveries[NotificationChannel.WhatsApp].AttemptCount);
    }
}
