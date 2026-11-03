using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Email;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Contracts;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Delivery;
using Romp.Modules.Notifications.Tests.Support;

namespace Romp.Modules.Notifications.Tests.Delivery;

public sealed class DeadLetterTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>, IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<long> KillOneAsync()
    {
        var clock = new TestClock(Start);
        var sender = new ScriptedSender(Enumerable.Repeat<Exception?>(new ChannelSendException(ErrorCategory.Temporary, "down"), 20).ToArray());
        await using var seed = postgres.NewContext();
        var id = (await DeliveryTestKit.SeedAsync(seed, 1, Start))[0];

        for (var pass = 0; pass < 8; pass++)
        {
            await using var db = postgres.NewContextWithOutbox();
            await DeliveryTestKit.Processor(db, clock, [sender]).RunOnceAsync(CancellationToken.None);
            clock.Advance(TimeSpan.FromHours(15));
        }

        return id;
    }

    [Fact]
    [Trait("Spec", "DLV-3")]
    [Trait("Spec", "DLV-12")]
    [Trait("Spec", "TC-NOTIF-03")]
    public async Task Run_WhenAttemptsRunOut_StoresADeadLetterWithItsHistoryAndRaisesTheAlertEvent()
    {
        var id = await KillOneAsync();

        await using var check = postgres.NewContext();
        var dead = await check.DeadLetters.AsNoTracking().SingleAsync();
        Assert.Equal(id, dead.DeliveryId);
        Assert.Equal(NotificationKind.OrderConfirmation, dead.Kind);
        Assert.Equal(NotificationChannel.Email, dead.Channel);
        Assert.Equal(6, dead.AttemptCount);
        Assert.Equal(6, await check.Attempts.CountAsync(a => a.DeliveryId == id));

        var message = await check.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.EventType == nameof(NotificationDeadLetteredEvent));
        Assert.Contains("\"ORCF\"", message.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", message.Payload, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "DLV-3")]
    public async Task Run_AfterTheDeliveryIsDead_DoesNotStoreASecondDeadLetter()
    {
        await KillOneAsync();
        await KillOneAsync();

        await using var check = postgres.NewContext();
        Assert.Equal(2, await check.DeadLetters.CountAsync());
        Assert.Equal(2, await check.Set<OutboxMessage>().CountAsync(m => m.EventType == nameof(NotificationDeadLetteredEvent)));
    }

    private static DeadLetterAlertConsumer Consumer(Support.NotificationsPostgres pg, long messageId, FakeEmailSender email, string? opsEmail, out Romp.Modules.Notifications.Infrastructure.Persistence.NtfyDbContext db)
    {
        db = pg.NewContext();
        var guard = new InboxGuard(new OutboxMessageContext { MessageId = messageId, Schema = "NTFY" }, TimeProvider.System);
        var settings = Options.Create(new DeliverySettings { OpsAlertEmail = opsEmail });
        return new DeadLetterAlertConsumer(db, guard, email, settings, NullLogger<DeadLetterAlertConsumer>.Instance);
    }

    [Fact]
    [Trait("Spec", "DLV-3")]
    public async Task Alert_EmailsOperationsOnce_WithIdsAndCodesOnly()
    {
        var email = new FakeEmailSender();
        var evt = new NotificationDeadLetteredEvent(77, "ORCF", "EMAL");

        var first = Consumer(postgres, 1, email, "ops@example.com", out var db1);
        await using (db1)
        {
            await first.HandleAsync(evt, CancellationToken.None);
        }

        var again = Consumer(postgres, 1, email, "ops@example.com", out var db2);
        await using (db2)
        {
            await again.HandleAsync(evt, CancellationToken.None);
        }

        var sent = Assert.Single(email.Sent);
        Assert.Equal("ops@example.com", sent.To);
        Assert.Contains("77", sent.Subject, StringComparison.Ordinal);
        Assert.Contains("ORCF", sent.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "DLV-3")]
    public async Task Alert_WithNoOpsAddress_StillSucceedsAndSendsNothing()
    {
        var email = new FakeEmailSender();

        var consumer = Consumer(postgres, 2, email, null, out var db);
        await using (db)
        {
            await consumer.HandleAsync(new NotificationDeadLetteredEvent(78, "ORCN", "WHTS"), CancellationToken.None);
        }

        Assert.Empty(email.Sent);
    }
}
