using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Domain.Channels;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Tests.Support;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Tests.Channels;

/// <summary>A customer's button reply reaches the right order, and only then (CHN-12, COD-14, COD-15, COD-16).</summary>
public sealed class CodReplyRoutingTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class RecordingResponses : ICodCustomerResponses
    {
        public List<(long OrderId, string Sender, bool Confirm)> Calls { get; } = [];

        public Task<CodReplyOutcome> RecordReplyAsync(long orderId, string senderMobile, bool confirm, CancellationToken cancellationToken)
        {
            Calls.Add((orderId, senderMobile, confirm));
            return Task.FromResult(confirm ? CodReplyOutcome.Confirmed : CodReplyOutcome.Cancelled);
        }
    }

    /// <summary>A notification whose WhatsApp message was sent with the given provider id.</summary>
    private async Task SeedSentAsync(long orderId, string providerId, NotificationKind kind = NotificationKind.CodConfirmationRequest)
    {
        await using var db = postgres.NewContext();
        var notification = new Notification(kind, ReferenceKinds.Order, orderId, Guid.NewGuid(), Now, NotificationPolicy.ValidFor(kind), NotificationPolicy.PriorityOf(kind));
        notification.Plan(ChannelPlan.For(true, false), Now);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        notification.Deliveries.Single(d => d.Channel == NotificationChannel.WhatsApp).MarkSent(providerId);
        await db.SaveChangesAsync();
    }

    private static WhatsAppInboundEvent Reply(string? replyTo, string? button, string sender = "+923001234567") =>
        new(Guid.NewGuid().ToString("N"), WhatsAppEventType.ButtonReply, sender, replyTo, button);

    [Theory]
    [InlineData(CodButtons.Confirm, true)]
    [InlineData(CodButtons.Cancel, false)]
    [Trait("Spec", "CHN-12")]
    [Trait("Spec", "COD-14")]
    [Trait("Spec", "COD-15")]
    public async Task HandleAsync_MatchesTheReplyToTheMessageItAnswers_AndCallsOrders(string button, bool confirm)
    {
        await SeedSentAsync(4242, "wamid.OUT1");
        var orders = new RecordingResponses();
        await using var db = postgres.NewContext();

        var matched = await new CodReplyHandler(db, orders).HandleAsync(Reply("wamid.OUT1", button), CancellationToken.None);

        Assert.NotNull(matched);
        Assert.Equal((4242L, "+923001234567", confirm), Assert.Single(orders.Calls));
    }

    [Theory]
    [InlineData("wamid.NOPE", CodButtons.Confirm)]
    [InlineData(null, CodButtons.Confirm)]
    [InlineData("wamid.OUT1", "something_else")]
    [InlineData("wamid.OUT1", null)]
    [Trait("Spec", "CHN-12")]
    [Trait("Spec", "COD-16")]
    public async Task HandleAsync_IgnoresAReplyThatMatchesNoSentMessageOrNoKnownButton(string? replyTo, string? button)
    {
        await SeedSentAsync(4242, "wamid.OUT1");
        var orders = new RecordingResponses();
        await using var db = postgres.NewContext();

        var matched = await new CodReplyHandler(db, orders).HandleAsync(Reply(replyTo, button), CancellationToken.None);

        Assert.Null(matched);
        Assert.Empty(orders.Calls);
    }

    [Fact]
    [Trait("Spec", "COD-16")]
    public async Task HandleAsync_IgnoresAReplyToAMessageThatWasNotACodRequest()
    {
        await SeedSentAsync(4242, "wamid.OUT1", NotificationKind.OrderConfirmation);
        var orders = new RecordingResponses();
        await using var db = postgres.NewContext();

        var matched = await new CodReplyHandler(db, orders).HandleAsync(Reply("wamid.OUT1", CodButtons.Confirm), CancellationToken.None);

        Assert.Null(matched);
        Assert.Empty(orders.Calls);
    }

    [Fact]
    [Trait("Spec", "CHN-12")]
    public async Task Webhook_ASignedReply_RoutesThroughTheProcessorToOrders()
    {
        await SeedSentAsync(777, "wamid.OUT9");
        var orders = new RecordingResponses();
        await using var db = postgres.NewContext();
        var processor = new WhatsAppWebhookProcessor(
            db, new WhatsAppSettings { AppSecret = "s" }, [new CodReplyHandler(db, orders)], new TestClock(Now), NullLogger<WhatsAppWebhookProcessor>.Instance);
        var body = System.Text.Encoding.UTF8.GetBytes(
            """{"entry":[{"changes":[{"value":{"messages":[{"from":"923001234567","id":"wamid.IN9","type":"interactive","context":{"id":"wamid.OUT9"},"interactive":{"type":"button_reply","button_reply":{"id":"cod_confirm"}}}]}}]}]}""");

        var outcome = await processor.HandleAsync(body, WhatsAppSignature.Compute("s", body), "1.1.1.1", CancellationToken.None);

        Assert.Equal(WebhookOutcome.Accepted, outcome);
        Assert.Equal((777L, "+923001234567", true), Assert.Single(orders.Calls));
        Assert.NotNull((await db.WhatsAppEvents.SingleAsync(e => e.ProviderEventId == "wamid.IN9")).MatchedDeliveryId);
    }
}
