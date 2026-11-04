using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Tests.Support;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Tests.OrderConfirmation;

public sealed class OrderConfirmationTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>
{
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private async Task<RenderedMessage> RenderAsync(OrderNotificationView order)
    {
        await using var db = postgres.NewContext();
        return await new TemplateMessageRenderer(db).RenderAsync(NotificationKind.OrderConfirmation, NotificationChannel.Email, order, CancellationToken.None);
    }

    [Fact]
    [Trait("Spec", "OCF-2")]
    public async Task Render_ForCod_SaysPendingConfirmationAndWhenTheCallComes()
    {
        var rendered = await RenderAsync(OrderMessageKit.Order("COD"));

        Assert.Contains("pending confirmation", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("within about an hour", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("RMP-0001", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("PKR 1,500.00", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "OCF-2")]
    public async Task Render_ForCod_WithTheSignedLink_IncludesItAndNoPhoneNumber()
    {
        var rendered = await RenderAsync(OrderMessageKit.Order("COD", confirmUrl: "https://romp.pk/cod/confirm?token=abc"));

        Assert.Contains("https://romp.pk/cod/confirm?token=abc", rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("+923001234567", rendered.Text + rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "OCF-2")]
    public async Task Render_ForCard_DoesNotPromiseACall()
    {
        var rendered = await RenderAsync(OrderMessageKit.Order("CARD"));

        Assert.DoesNotContain("call you", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("payment has been received", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "OCF-7")]
    public async Task Render_Email_HasAPlainTextTwinAndNoUnsubscribeOrMarketingContent()
    {
        var rendered = await RenderAsync(OrderMessageKit.Order());

        Assert.False(string.IsNullOrWhiteSpace(rendered.Text));
        Assert.NotNull(rendered.Html);
        Assert.DoesNotContain("<img", rendered.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unsubscribe", rendered.Text + rendered.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Spec", "OCF-5")]
    public async Task Run_SendsTheOrderAsStoredAtSendTime_NotAsItWasWhenPlanned()
    {
        var clock = new TestClock(Start);
        var sender = new ScriptedSender();
        await using var seed = postgres.NewContext();
        await DeliveryTestKit.SeedAsync(seed, 1, Start);

        // Staff corrected the price between planning and sending: the message follows the order as stored now.
        await using var db = postgres.NewContext();
        await OrderMessageKit.Processor(db, clock, sender, new FixedOrder(() => OrderMessageKit.Order(total: 2750m))).RunOnceAsync(CancellationToken.None);

        var message = Assert.Single(sender.Sent);
        Assert.Contains("PKR 2,750.00", message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("1,500.00", message.Text, StringComparison.Ordinal);
    }
}
