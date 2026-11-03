using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Channels;

namespace Romp.Modules.Notifications.Tests.Channels;

public sealed class FakeWhatsAppSenderTests
{
    private static WhatsAppMessage Message(string number = "+923001234567") => new(number, "hello", []);

    [Fact]
    [Trait("Spec", "CHN-8")]
    public async Task SendAsync_RecordsTheMessageAndReturnsDistinctProviderIds()
    {
        var fake = new FakeWhatsAppSender();

        var first = await fake.SendAsync(Message(), CancellationToken.None);
        var second = await fake.SendAsync(Message("+923009999999"), CancellationToken.None);

        Assert.NotEqual(first, second);
        Assert.Equal(2, fake.Sent.Count);
        Assert.Equal("+923009999999", fake.Sent[1].RecipientNumber);
    }

    [Fact]
    [Trait("Spec", "CHN-8")]
    public async Task SendAsync_WhenToldToFail_ThrowsTheCategoryThenRecovers()
    {
        var fake = new FakeWhatsAppSender();
        fake.FailNext(ErrorCategory.Temporary, 2);

        var ex = await Assert.ThrowsAsync<ChannelSendException>(() => fake.SendAsync(Message(), CancellationToken.None));
        await Assert.ThrowsAsync<ChannelSendException>(() => fake.SendAsync(Message(), CancellationToken.None));
        await fake.SendAsync(Message(), CancellationToken.None);

        Assert.Equal(ErrorCategory.Temporary, ex.Category);
        Assert.Single(fake.Sent);
    }

    [Fact]
    [Trait("Spec", "CHN-8")]
    public void RaiseInbound_ReachesSubscribers()
    {
        var fake = new FakeWhatsAppSender();
        WhatsAppInboundEvent? seen = null;
        fake.InboundReceived += e => seen = e;

        var inbound = new WhatsAppInboundEvent("evt-1", WhatsAppInboundKind.Stop, "+923001234567", null, null);
        fake.RaiseInbound(inbound);

        Assert.Equal(inbound, seen);
    }

    [Fact]
    [Trait("Spec", "CHN-8")]
    public async Task ChannelSender_SendsThroughThePortAsTheWhatsAppChannel()
    {
        var fake = new FakeWhatsAppSender();
        var channel = new WhatsAppChannelSender(fake);

        var id = await channel.SendAsync(new OutboundMessage(NotificationChannel.WhatsApp, "+923001234567", "s", "body", null), CancellationToken.None);

        Assert.Equal(NotificationChannel.WhatsApp, channel.Channel);
        Assert.StartsWith("fake-wamid-", id, StringComparison.Ordinal);
        Assert.Equal("body", fake.Sent[0].Text);
    }
}
