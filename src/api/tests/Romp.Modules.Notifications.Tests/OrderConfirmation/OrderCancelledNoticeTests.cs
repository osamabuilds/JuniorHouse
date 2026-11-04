using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Tests.Support;

namespace Romp.Modules.Notifications.Tests.OrderConfirmation;

public sealed class OrderCancelledNoticeTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>
{
    [Theory]
    [InlineData("CODN", "could not reach you")]
    [InlineData("CODR", "declined it")]
    [InlineData("CUST", "asked us to cancel")]
    [InlineData("PYFL", "payment did not go through")]
    [InlineData("PYEX", "not completed in time")]
    [InlineData("STAF", "unable to fulfil")]
    [InlineData(null, "could not be completed")]
    [InlineData("UNKN", "could not be completed")]
    [Trait("Spec", "OCF-4")]
    [Trait("Spec", "TC-CHK-15")]
    public async Task Render_NamesTheReasonCategory(string? reasonCode, string expected)
    {
        await using var db = postgres.NewContext();

        var rendered = await new TemplateMessageRenderer(db).RenderAsync(
            NotificationKind.OrderCancelled, NotificationChannel.Email, OrderMessageKit.Order(reason: reasonCode), CancellationToken.None);

        Assert.Contains(expected, rendered.Text, StringComparison.Ordinal);
        Assert.Contains(expected, rendered.Html, StringComparison.Ordinal);
        Assert.Contains("RMP-0001", rendered.Subject, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "OCF-4")]
    public async Task Render_OnWhatsApp_UsesTheApprovedTemplateWithItsThreeValues()
    {
        await using var db = postgres.NewContext();

        var rendered = await new TemplateMessageRenderer(db).RenderAsync(
            NotificationKind.OrderCancelled, NotificationChannel.WhatsApp, OrderMessageKit.Order(reason: "CODN"), CancellationToken.None);

        Assert.Equal("romp_order_cancelled", rendered.ProviderTemplate);
        Assert.Equal(3, rendered.Parameters!.Count);
    }
}
