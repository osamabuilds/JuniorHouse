using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Tests.Support;

namespace Romp.Modules.Notifications.Tests.Delivery;

public sealed class DeliveryWorkerTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>, IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static ChannelSendException Temporary() => new(ErrorCategory.Temporary, "down");

    private async Task<int> PassAsync(TestClock clock, ScriptedSender sender, Romp.Modules.Orders.Contracts.IOrderNotificationQueries? orders = null)
    {
        await using var db = postgres.NewContext();
        return await DeliveryTestKit.Processor(db, clock, [sender], orders).RunOnceAsync(CancellationToken.None);
    }

    [Fact]
    [Trait("Spec", "DLV-2")]
    [Trait("Spec", "TC-NOTIF-02")]
    public async Task Run_WhenProviderFailsTwiceThenSucceeds_SendsExactlyOnce()
    {
        var clock = new TestClock(Start);
        var sender = new ScriptedSender(Temporary(), Temporary());
        await using var seed = postgres.NewContext();
        var id = (await DeliveryTestKit.SeedAsync(seed, 1, Start))[0];

        for (var pass = 0; pass < 4; pass++)
        {
            await PassAsync(clock, sender);
            clock.Advance(TimeSpan.FromHours(15)); // longer than any step of the schedule with jitter
        }

        await using var check = postgres.NewContext();
        var delivery = await DeliveryTestKit.LoadAsync(check, id);
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        Assert.Equal(3, delivery.AttemptCount);
        Assert.Equal(3, sender.Calls);
        Assert.Single(sender.Sent);
        Assert.Equal(3, await check.Attempts.CountAsync(a => a.DeliveryId == id));
    }

    [Fact]
    [Trait("Spec", "DLV-8")]
    public async Task Run_WhenTheErrorIsPermanent_FailsFastAndIsNotRetried()
    {
        var clock = new TestClock(Start);
        var sender = new ScriptedSender(new ChannelSendException(ErrorCategory.Permanent, "bad address"));
        await using var seed = postgres.NewContext();
        var id = (await DeliveryTestKit.SeedAsync(seed, 1, Start))[0];

        await PassAsync(clock, sender);
        clock.Advance(TimeSpan.FromDays(1));
        await PassAsync(clock, sender);

        await using var check = postgres.NewContext();
        var delivery = await DeliveryTestKit.LoadAsync(check, id);
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal(ErrorCategory.Permanent, delivery.LastError);
        Assert.Equal(1, sender.Calls);
    }

    [Fact]
    [Trait("Spec", "DLV-3")]
    public async Task Run_WhenEveryAttemptFails_EndsDeadAfterTheSixthAttempt()
    {
        var clock = new TestClock(Start);
        var sender = new ScriptedSender(Enumerable.Repeat<Exception?>(Temporary(), 20).ToArray());
        await using var seed = postgres.NewContext();
        var id = (await DeliveryTestKit.SeedAsync(seed, 1, Start))[0];

        for (var pass = 0; pass < 9; pass++)
        {
            await PassAsync(clock, sender);
            clock.Advance(TimeSpan.FromHours(15));
        }

        await using var check = postgres.NewContext();
        var delivery = await DeliveryTestKit.LoadAsync(check, id);
        Assert.Equal(DeliveryStatus.Dead, delivery.Status);
        Assert.Equal(6, delivery.AttemptCount);
        Assert.Equal(6, sender.Calls);
    }

    [Fact]
    [Trait("Spec", "OCF-8")]
    public async Task Run_WhenTheOrderHasNoEmail_FailsWithNoAddressAndSendsNothing()
    {
        var clock = new TestClock(Start);
        var sender = new ScriptedSender();
        await using var seed = postgres.NewContext();
        var id = (await DeliveryTestKit.SeedAsync(seed, 1, Start))[0];

        await PassAsync(clock, sender, new StubOrders(email: null));

        await using var check = postgres.NewContext();
        var delivery = await DeliveryTestKit.LoadAsync(check, id);
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal(ErrorCategory.NoAddress, delivery.LastError);
        Assert.Equal(0, sender.Calls);
    }

    [Fact]
    [Trait("Spec", "CHN-4")]
    public async Task Run_WhenNoSenderCarriesTheChannel_WaitsWithoutUsingAnAttempt()
    {
        var clock = new TestClock(Start);
        await using var seed = postgres.NewContext();
        var id = (await DeliveryTestKit.SeedAsync(seed, 1, Start))[0];

        await using (var db = postgres.NewContext())
        {
            await DeliveryTestKit.Processor(db, clock, []).RunOnceAsync(CancellationToken.None);
        }

        await using var check = postgres.NewContext();
        var delivery = await DeliveryTestKit.LoadAsync(check, id);
        Assert.Equal(DeliveryStatus.Queued, delivery.Status);
        Assert.Equal(0, delivery.AttemptCount);
        Assert.True(delivery.NextAttemptAt > Start);
    }

    [Fact]
    [Trait("Spec", "DLV-2")]
    public async Task Run_WhenTheSenderThrowsSomethingUnexpected_TreatsItAsTemporaryAndKeepsNoText()
    {
        var clock = new TestClock(Start);
        var sender = new ScriptedSender(new InvalidOperationException("secret@example.com exploded"));
        await using var seed = postgres.NewContext();
        var id = (await DeliveryTestKit.SeedAsync(seed, 1, Start))[0];

        await PassAsync(clock, sender);

        await using var check = postgres.NewContext();
        var delivery = await DeliveryTestKit.LoadAsync(check, id);
        Assert.Equal(DeliveryStatus.Queued, delivery.Status);
        Assert.Equal(ErrorCategory.Temporary, delivery.LastError);
        Assert.Equal(1, delivery.AttemptCount);
    }

    [Fact]
    [Trait("Spec", "OCF-7")]
    [Trait("Spec", "CHN-9")]
    public async Task Run_WithAHostileProductName_EncodesItInTheHtmlBody()
    {
        var clock = new TestClock(Start);
        var sender = new ScriptedSender();
        await using var seed = postgres.NewContext();
        await DeliveryTestKit.SeedAsync(seed, 1, Start);

        await PassAsync(clock, sender, new StubOrders(name: "<script>alert(1)</script>"));

        var sent = Assert.Single(sender.Sent);
        Assert.DoesNotContain("<script>", sent.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", sent.Html, StringComparison.Ordinal);
        Assert.Equal("a@example.com", sent.Recipient);
    }

    [Fact]
    [Trait("Spec", "DLV-11")]
    public async Task Run_WithMoreDueThanTheBatch_TakesHigherPriorityFirst()
    {
        var clock = new TestClock(Start);
        await using var seed = postgres.NewContext();
        var reminder = (await DeliveryTestKit.SeedAsync(seed, 1, Start, NotificationKind.CodConfirmationRequest))[0];
        var confirmation = (await DeliveryTestKit.SeedAsync(seed, 1, Start, NotificationKind.OrderConfirmation))[0];

        await using var db = postgres.NewContext();
        var ids = await new Romp.Modules.Notifications.Infrastructure.Delivery.SkipLockedDeliveryClaimer(db)
            .ClaimAsync(Start, 1, TimeSpan.FromMinutes(2), CancellationToken.None);

        Assert.Equal([confirmation], ids);
        Assert.NotEqual(reminder, ids[0]);
    }
}
