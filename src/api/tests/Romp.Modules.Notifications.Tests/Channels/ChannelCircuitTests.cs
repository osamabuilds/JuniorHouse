using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Channels;
using Romp.Modules.Notifications.Tests.Support;

namespace Romp.Modules.Notifications.Tests.Channels;

public sealed class ChannelCircuitTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private static ChannelSendException Temporary() => new(ErrorCategory.Temporary, "down");

    private static OutboundMessage Message(NotificationChannel channel) => new(channel, "x", "s", "t", null);

    private static async Task<ErrorCategory?> TrySend(CircuitBreakingChannelSender sender, NotificationChannel channel)
    {
        try
        {
            await sender.SendAsync(Message(channel), CancellationToken.None);
            return null;
        }
        catch (ChannelSendException ex)
        {
            return ex.Category;
        }
    }

    [Fact]
    [Trait("Spec", "CHN-4")]
    public async Task SendAsync_AfterRepeatedTemporaryFailures_OpensTheCircuitAndStopsCallingTheProvider()
    {
        var clock = new TestClock(Start);
        var circuits = new ChannelCircuits(new ChannelCircuitOptions(), clock);
        var inner = new ScriptedSender(Enumerable.Repeat<Exception?>(Temporary(), 20).ToArray());
        var sender = new CircuitBreakingChannelSender(inner, circuits);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(ErrorCategory.Temporary, await TrySend(sender, NotificationChannel.Email));
        }

        var callsWhenOpened = inner.Calls;
        var fastFail = await TrySend(sender, NotificationChannel.Email);

        Assert.Equal(ErrorCategory.CircuitOpen, fastFail);
        Assert.Equal(callsWhenOpened, inner.Calls);
    }

    [Fact]
    [Trait("Spec", "CHN-4")]
    public async Task SendAsync_AfterTheBreakDuration_TriesAgainAndClosesOnSuccess()
    {
        var clock = new TestClock(Start);
        var circuits = new ChannelCircuits(new ChannelCircuitOptions(), clock);
        var script = Enumerable.Repeat<Exception?>(Temporary(), 5).ToArray(); // then the provider recovers
        var inner = new ScriptedSender(script);
        var sender = new CircuitBreakingChannelSender(inner, circuits);
        for (var i = 0; i < 5; i++)
        {
            await TrySend(sender, NotificationChannel.Email);
        }

        Assert.Equal(ErrorCategory.CircuitOpen, await TrySend(sender, NotificationChannel.Email));
        clock.Advance(TimeSpan.FromSeconds(31));

        Assert.Null(await TrySend(sender, NotificationChannel.Email));
        Assert.Null(await TrySend(sender, NotificationChannel.Email));
    }

    [Fact]
    [Trait("Spec", "CHN-4")]
    public async Task SendAsync_WhenWhatsAppCircuitIsOpen_EmailStillSends()
    {
        var clock = new TestClock(Start);
        var circuits = new ChannelCircuits(new ChannelCircuitOptions(), clock);
        var whatsApp = new CircuitBreakingChannelSender(new FailingSender(NotificationChannel.WhatsApp), circuits);
        var emailInner = new ScriptedSender();
        var email = new CircuitBreakingChannelSender(emailInner, circuits);
        for (var i = 0; i < 6; i++)
        {
            await TrySend(whatsApp, NotificationChannel.WhatsApp);
        }

        Assert.Equal(ErrorCategory.CircuitOpen, await TrySend(whatsApp, NotificationChannel.WhatsApp));
        Assert.Null(await TrySend(email, NotificationChannel.Email));
        Assert.Equal(1, emailInner.Calls);
    }

    [Fact]
    [Trait("Spec", "CHN-4")]
    public async Task SendAsync_PermanentFailures_DoNotOpenTheCircuit()
    {
        var clock = new TestClock(Start);
        var circuits = new ChannelCircuits(new ChannelCircuitOptions(), clock);
        var inner = new ScriptedSender(Enumerable.Repeat<Exception?>(new ChannelSendException(ErrorCategory.Permanent, "bad address"), 10).ToArray());
        var sender = new CircuitBreakingChannelSender(inner, circuits);

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(ErrorCategory.Permanent, await TrySend(sender, NotificationChannel.Email));
        }

        Assert.Equal(10, inner.Calls);
    }

    private sealed class FailingSender(NotificationChannel channel) : IChannelSender
    {
        public NotificationChannel Channel => channel;

        public Task<string?> SendAsync(OutboundMessage message, CancellationToken cancellationToken) => throw Temporary();
    }
}
