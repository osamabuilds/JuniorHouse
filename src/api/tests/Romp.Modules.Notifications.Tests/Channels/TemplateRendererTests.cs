using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Tests.Support;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Tests.Channels;

public sealed class TemplateRendererTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>
{
    private static OrderNotificationView Order(string customer = "Ayesha", string product = "Red Dress") =>
        new(1, "RMP-0001", "COD", customer, "a@example.com", "+923001234567", true, 1500m, [new OrderNotificationLine(product, "Red", "4-5Y", 2, 1500m)]);

    private static MessageTemplate Email(string subject = "Order {{OrderNumber}}") =>
        new(NotificationKind.OrderConfirmation, NotificationChannel.Email, "EN", 1, subject, "Hi {{CustomerName}} {{Lines}} {{Total}}", "<p>{{CustomerName}}</p>{{Lines}}", null);

    [Fact]
    [Trait("Spec", "CHN-9")]
    public void Render_FillsPlaceholdersFromTheOrder()
    {
        var rendered = TemplateEngine.Render(Email(), Order());

        Assert.Equal("Order RMP-0001", rendered.Subject);
        Assert.Contains("Hi Ayesha", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("PKR 1,500.00", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("2 x Red Dress (Red, 4-5Y)", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("<li>2 x Red Dress", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "CHN-9")]
    [Trait("Spec", "OCF-7")]
    public void Render_WithHostileValues_EncodesHtmlAndStripsLineBreaksFromSubject()
    {
        var hostile = "<script>alert(1)</script>\r\nBcc: evil@example.com";
        var rendered = TemplateEngine.Render(Email("Hello {{CustomerName}}"), Order(hostile, "<img src=x onerror=1>"));

        Assert.DoesNotContain("<script>", rendered.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", rendered.Html, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', rendered.Subject);
        Assert.DoesNotContain('\r', rendered.Subject);
    }

    [Fact]
    [Trait("Spec", "CHN-9")]
    public void Render_DoesNotExpandPlaceholdersInsideValues()
    {
        var rendered = TemplateEngine.Render(Email(), Order("{{Total}}"));

        Assert.StartsWith("Hi {{Total}}", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "CHN-9")]
    public void Render_ForWhatsApp_CapsLengthAndHasNoHtml()
    {
        var template = new MessageTemplate(NotificationKind.OrderConfirmation, NotificationChannel.WhatsApp, "EN", 1, "s", "Hi {{CustomerName}}", "<p>x</p>", "romp_x");

        var rendered = TemplateEngine.Render(template, Order(new string('a', 500)));

        Assert.Null(rendered.Html);
        Assert.Equal("Hi ".Length + 200, rendered.Text.Length);
    }

    [Theory]
    [InlineData(NotificationKind.OrderConfirmation, NotificationChannel.Email)]
    [InlineData(NotificationKind.OrderConfirmation, NotificationChannel.WhatsApp)]
    [InlineData(NotificationKind.OrderCancelled, NotificationChannel.Email)]
    [InlineData(NotificationKind.OrderCancelled, NotificationChannel.WhatsApp)]
    [InlineData(NotificationKind.CodConfirmationRequest, NotificationChannel.Email)]
    [InlineData(NotificationKind.CodConfirmationRequest, NotificationChannel.WhatsApp)]
    [Trait("Spec", "CHN-9")]
    public async Task RenderAsync_EveryKindAndChannelHasASeededTemplate(NotificationKind kind, NotificationChannel channel)
    {
        await using var db = postgres.NewContext();
        var renderer = new TemplateMessageRenderer(db);

        var rendered = await renderer.RenderAsync(kind, channel, Order(), CancellationToken.None);

        Assert.Contains("RMP-0001", rendered.Text + rendered.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", rendered.Text + rendered.Subject + rendered.Html, StringComparison.Ordinal);
        Assert.Equal(channel == NotificationChannel.Email, rendered.Html is not null);
    }

    [Fact]
    [Trait("Spec", "CHN-9")]
    public async Task RenderAsync_UsesTheNewestActiveVersion_AndFallsBackWhenNoTemplateExists()
    {
        await using var db = postgres.NewContext();
        var renderer = new TemplateMessageRenderer(db);
        await using var transaction = await db.Database.BeginTransactionAsync(); // rolled back: the seeded rows stay for the other tests

        db.Templates.Add(new MessageTemplate(NotificationKind.OrderConfirmation, NotificationChannel.Email, "EN", 3, "v3 {{OrderNumber}}", "body", null, null));
        await db.SaveChangesAsync();
        var newest = await renderer.RenderAsync(NotificationKind.OrderConfirmation, NotificationChannel.Email, Order(), CancellationToken.None);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"NTFY\".\"NTFY_TMPL\" WHERE \"NTFY_KIND_ID\" = 1 AND \"CHNL_ID\" = 1");
        var fallback = await renderer.RenderAsync(NotificationKind.OrderConfirmation, NotificationChannel.Email, Order(), CancellationToken.None);

        Assert.Equal("v3 RMP-0001", newest.Subject);
        Assert.Equal("Your Romp order RMP-0001", fallback.Subject);
    }
}
