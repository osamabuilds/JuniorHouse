using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Tests.Support;

namespace Romp.Modules.Notifications.Tests.OrderConfirmation;

public sealed class CodRequestMessageTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>
{
    [Fact]
    [Trait("Spec", "COD-13")]
    public async Task Render_OnWhatsApp_CarriesConfirmAndCancelButtons()
    {
        await using var db = postgres.NewContext();

        var rendered = await new TemplateMessageRenderer(db).RenderAsync(
            NotificationKind.CodConfirmationRequest, NotificationChannel.WhatsApp, OrderMessageKit.Order(), CancellationToken.None);

        Assert.Equal("romp_cod_confirmation", rendered.ProviderTemplate);
        Assert.Equal(2, rendered.Buttons!.Count);
        Assert.Contains(rendered.Buttons, b => b.Title == "Confirm order");
        Assert.Contains(rendered.Buttons, b => b.Title == "Cancel order");
    }

    [Fact]
    [Trait("Spec", "COD-18")]
    public async Task Render_Email_CarriesTheSignedConfirmLinkAsAButton()
    {
        await using var db = postgres.NewContext();
        var url = "https://romp.pk/cod/confirm?token=a%2Bb";

        var rendered = await new TemplateMessageRenderer(db).RenderAsync(
            NotificationKind.CodConfirmationRequest, NotificationChannel.Email, OrderMessageKit.Order(confirmUrl: url), CancellationToken.None);

        Assert.Contains(url, rendered.Text, StringComparison.Ordinal);
        Assert.Contains("<a href=\"" + url + "\">Confirm my order</a>", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "COD-18")]
    public async Task Render_Email_HostileLinkValueCannotBreakOutOfTheAttribute()
    {
        await using var db = postgres.NewContext();

        var rendered = await new TemplateMessageRenderer(db).RenderAsync(
            NotificationKind.CodConfirmationRequest, NotificationChannel.Email, OrderMessageKit.Order(confirmUrl: "https://x.pk/\"><script>1</script>"), CancellationToken.None);

        Assert.DoesNotContain("<script>", rendered.Html, StringComparison.Ordinal);
    }
}
