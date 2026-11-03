using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Notifications.Application.DeliveryLog;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Persistence;
using Romp.Modules.Notifications.Tests.Support;

namespace Romp.Modules.Notifications.Tests.DeliveryLog;

public sealed class ResendTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
    private const string Staff = "support@romp.pk";

    private sealed class FixedActor(string name) : ICurrentActor
    {
        public string UserName => name;
    }

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private NtfyDbContext StaffContext() => postgres.NewContext(new AuditSaveChangesInterceptor(new TestClock(Now), new FixedActor(Staff)));

    private static async Task SetStatusAsync(NtfyDbContext db, long deliveryId, short statusId, int attempts = 1) =>
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"NTFY\".\"NTFY_CHNL\" SET \"CHNL_STS_ID\" = {0}, \"ATMP_CNT\" = {1}, \"NXT_ATMP_DTE\" = NULL WHERE \"ID\" = {2}", statusId, attempts, deliveryId);

    private async Task<long> SeedAsync(short statusId)
    {
        await using var seed = postgres.NewContext();
        var id = (await DeliveryTestKit.SeedAsync(seed, 1, Now))[0];
        await SetStatusAsync(seed, id, statusId);
        return id;
    }

    private async Task<ResendResult> ResendAsync(long id, string key, bool acknowledge = false)
    {
        await using var db = StaffContext();
        return await new ResendNotification(db, new TestClock(Now)).ExecuteAsync(id, key, acknowledge, Staff, CancellationToken.None);
    }

    [Fact]
    [Trait("Spec", "DLG-2")]
    [Trait("Spec", "TC-NOTIF-06")]
    [Trait("Spec", "FR-63")]
    public async Task Resend_OfAFailedDelivery_QueuesANewOneThatTheWorkerSends()
    {
        var id = await SeedAsync(4); // Failed

        var result = await ResendAsync(id, "click-0001-aaaa");

        Assert.Equal(ResendOutcome.Queued, result.Outcome);
        await using (var check = postgres.NewContext())
        {
            var queued = await DeliveryTestKit.LoadAsync(check, result.NewDeliveryId!.Value);
            Assert.Equal(DeliveryStatus.Queued, queued.Status);
            Assert.Equal(id, queued.FallbackOfId);
            Assert.Equal(DeliveryStatus.Failed, (await DeliveryTestKit.LoadAsync(check, id)).Status);
        }

        var sender = new ScriptedSender();
        await using (var worker = postgres.NewContextWithOutbox())
        {
            await DeliveryTestKit.Processor(worker, new TestClock(Now), [sender]).RunOnceAsync(CancellationToken.None);
        }

        await using var after = postgres.NewContext();
        Assert.Equal(DeliveryStatus.Sent, (await DeliveryTestKit.LoadAsync(after, result.NewDeliveryId!.Value)).Status);
        Assert.Single(sender.Sent);
    }

    [Fact]
    [Trait("Spec", "DLG-3")]
    [Trait("Spec", "FR-59")]
    public async Task Resend_RecordsWhoAskedAndWhen_OnTheNewDeliveryAndTheDeadLetter()
    {
        var id = await SeedAsync(5); // Dead
        await using (var seed = postgres.NewContext())
        {
            seed.DeadLetters.Add(new DeadLetter(1, id, NotificationKind.OrderConfirmation, NotificationChannel.Email, ErrorCategory.Temporary, 6, Now));
            await seed.SaveChangesAsync();
        }

        var result = await ResendAsync(id, "click-0002-bbbb");

        await using var check = postgres.NewContext();
        var queued = await DeliveryTestKit.LoadAsync(check, result.NewDeliveryId!.Value);
        Assert.Equal(Staff, queued.InsrBy);
        Assert.Equal(Now, queued.InsrDte);
        var dead = await check.DeadLetters.AsNoTracking().SingleAsync();
        Assert.Equal(Staff, dead.ResentBy);
        Assert.Equal(Now, dead.ResentAt);
    }

    [Theory]
    [Trait("Spec", "DLG-7")]
    [Trait("Spec", "NFR-FT-05")]
    [InlineData((short)2)] // Sent
    [InlineData((short)3)] // Delivered
    public async Task Resend_OfAMessageThatProbablyArrived_NeedsConfirmation_ThenQueuesOnceConfirmed(short statusId)
    {
        var id = await SeedAsync(statusId);

        var warned = await ResendAsync(id, "click-0003-cccc");
        Assert.Equal(ResendOutcome.NeedsConfirmation, warned.Outcome);
        await using (var check = postgres.NewContext())
        {
            Assert.Equal(1, await check.Deliveries.CountAsync());
        }

        var confirmed = await ResendAsync(id, "click-0003-cccc", acknowledge: true);
        Assert.Equal(ResendOutcome.Queued, confirmed.Outcome);
    }

    [Fact]
    [Trait("Spec", "DLG-8")]
    [Trait("Spec", "NFR-FT-05")]
    public async Task Resend_WithTheSameKeyTwice_QueuesOnce_AndAnswersTheSameDelivery()
    {
        var id = await SeedAsync(4);

        var first = await ResendAsync(id, "click-0004-dddd");
        var second = await ResendAsync(id, "click-0004-dddd");

        Assert.Equal(ResendOutcome.Queued, first.Outcome);
        Assert.Equal(ResendOutcome.Replayed, second.Outcome);
        Assert.Equal(first.NewDeliveryId, second.NewDeliveryId);
        await using var check = postgres.NewContext();
        Assert.Equal(2, await check.Deliveries.CountAsync());
    }

    [Fact]
    [Trait("Spec", "DLG-8")]
    public async Task Resend_WithTwoRequestsRacingOnOneKey_QueuesExactlyOne()
    {
        var id = await SeedAsync(4);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => ResendAsync(id, "click-0005-eeee")));

        Assert.Equal(1, results.Count(r => r.Outcome == ResendOutcome.Queued));
        Assert.All(results.Where(r => r.Outcome != ResendOutcome.Queued), r => Assert.Equal(ResendOutcome.Replayed, r.Outcome));
        await using var check = postgres.NewContext();
        Assert.Equal(2, await check.Deliveries.CountAsync());
    }

    [Fact]
    [Trait("Spec", "DLG-8")]
    public async Task Resend_WhileAnEarlierResendIsStillWaiting_IsRefusedEvenWithAFreshKey()
    {
        var id = await SeedAsync(4);
        await ResendAsync(id, "click-0006-ffff");

        var again = await ResendAsync(id, "click-0006-gggg");

        Assert.Equal(ResendOutcome.AlreadyInProgress, again.Outcome);
    }

    [Fact]
    [Trait("Spec", "DLG-8")]
    public async Task Resend_ReusingAKeyForAnotherDelivery_IsRefused()
    {
        var first = await SeedAsync(4);
        await using (var seed = postgres.NewContext())
        {
            await DeliveryTestKit.SeedAsync(seed, 1, Now);
        }

        await ResendAsync(first, "click-0007-hhhh");
        await using var other = postgres.NewContext();
        var otherId = await other.Deliveries.Where(d => d.Id != first && d.FallbackOfId == null).Select(d => d.Id).FirstAsync();

        var result = await ResendAsync(otherId, "click-0007-hhhh");

        Assert.Equal(ResendOutcome.KeyReused, result.Outcome);
    }

    [Theory]
    [Trait("Spec", "DLG-2")]
    [InlineData((short)1)] // Queued: already on its way
    [InlineData((short)7)] // Skipped: no opt-in
    public async Task Resend_OfAQueuedOrSkippedDelivery_IsRefused(short statusId)
    {
        var id = await SeedAsync(statusId);

        Assert.Equal(ResendOutcome.NotResendable, (await ResendAsync(id, "click-0008-iiii")).Outcome);
    }

    [Fact]
    [Trait("Spec", "DLG-2")]
    public async Task Resend_OfAnUnknownDelivery_IsNotFound() =>
        Assert.Equal(ResendOutcome.NotFound, (await ResendAsync(987654, "click-0009-jjjj")).Outcome);

    [Fact]
    [Trait("Spec", "DLG-2")]
    public async Task Resend_OfAnExpiredNotification_GivesItAFreshLifeSoTheWorkerDoesNotExpireItAgain()
    {
        var id = await SeedAsync(6); // Expired
        await using (var seed = postgres.NewContext())
        {
            await seed.Database.ExecuteSqlRawAsync("UPDATE \"NTFY\".\"NTFY_MAIN\" SET \"EXPY_DTE\" = {0}", Now.AddDays(-1));
        }

        var result = await ResendAsync(id, "click-0010-kkkk");
        var sender = new ScriptedSender();
        await using (var worker = postgres.NewContextWithOutbox())
        {
            await DeliveryTestKit.Processor(worker, new TestClock(Now), [sender]).RunOnceAsync(CancellationToken.None);
        }

        await using var check = postgres.NewContext();
        Assert.Equal(DeliveryStatus.Sent, (await DeliveryTestKit.LoadAsync(check, result.NewDeliveryId!.Value)).Status);
    }

    [Theory]
    [Trait("Spec", "DLG-8")]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("has spaces in it!")]
    public async Task Resend_WithAMissingOrMalformedKey_IsRejectedBeforeAnythingChanges(string key)
    {
        var id = await SeedAsync(4);

        await Assert.ThrowsAsync<ValidationException>(() => ResendAsync(id, key));
    }
}
