using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Notifications.Application.DeliveryLog;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Channels;
using Romp.Modules.Notifications.Infrastructure.Persistence;
using Romp.Modules.Notifications.Tests.Support;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Tests.DeliveryLog;

/// <summary>Orders' masked summary, standing in: the real masking has its own tests in the Orders suite.</summary>
internal sealed class StubContacts : IOrderContactSummaries
{
    public int FindManyCalls { get; private set; }

    public Task<IReadOnlyDictionary<long, OrderContactSummary>> FindManyAsync(IReadOnlyCollection<long> orderIds, CancellationToken cancellationToken)
    {
        FindManyCalls++;
        return Task.FromResult<IReadOnlyDictionary<long, OrderContactSummary>>(
            orderIds.ToDictionary(id => id, id => new OrderContactSummary(id, $"RMP-{id}", "a***@mail.com", "03XX-XXX4567")));
    }

    public Task<IReadOnlyList<long>> FindOrderIdsAsync(string term, int max, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<long>>(term.StartsWith("RMP-", StringComparison.Ordinal) && long.TryParse(term[4..], out var id) ? [id] : []);
}

public sealed class DeliveryLogQueryTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DeliveryLogQueries Queries(NtfyDbContext db, StubContacts? contacts = null) =>
        new(db, contacts ?? new StubContacts(), new QueueHealth(db, new TestClock(Now)), new SqlDailySendCounter(db, new TestClock(Now)), new TestClock(Now));

    private static async Task SetStatusAsync(NtfyDbContext db, long deliveryId, short statusId, int attempts = 0) =>
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"NTFY\".\"NTFY_CHNL\" SET \"CHNL_STS_ID\" = {0}, \"ATMP_CNT\" = {1} WHERE \"ID\" = {2}", statusId, attempts, deliveryId);

    [Fact]
    [Trait("Spec", "DLG-1")]
    [Trait("Spec", "DLG-6")]
    [Trait("Spec", "NFR-FT-07")]
    public async Task List_ReturnsKindChannelStatusAttemptsAndAMaskedRecipient()
    {
        await using var seed = postgres.NewContext();
        var ids = await DeliveryTestKit.SeedAsync(seed, 1, Now);
        await SetStatusAsync(seed, ids[0], 4, 2); // Failed after two attempts

        await using var db = postgres.NewContext();
        var page = await Queries(db).ListAsync(new DeliveryLogFilter(), new PageRequest(), CancellationToken.None);

        var row = Assert.Single(page.Items);
        Assert.Equal("ORCF", row.Kind);
        Assert.Equal("EMAL", row.Channel);
        Assert.Equal("FAIL", row.Status);
        Assert.Equal(2, row.Attempts);
        Assert.Equal("RMP-5000", row.OrderNumber);
        Assert.Equal("a***@mail.com", row.Recipient);
        Assert.True(row.CanResend);
        Assert.DoesNotContain("@example.com", System.Text.Json.JsonSerializer.Serialize(page), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "DLG-6")]
    public async Task List_ForAWhatsAppDelivery_ShowsTheMaskedMobileNotTheEmail()
    {
        await using var seed = postgres.NewContext();
        var notification = new Notification(NotificationKind.OrderConfirmation, ReferenceKinds.Order, 7000, Guid.NewGuid(), Now, TimeSpan.FromDays(7), 0);
        notification.Plan(new ChannelPlanResult(false, true, null), Now);
        seed.Notifications.Add(notification);
        await seed.SaveChangesAsync();

        await using var db = postgres.NewContext();
        var row = Assert.Single((await Queries(db).ListAsync(new DeliveryLogFilter(), new PageRequest(), CancellationToken.None)).Items);

        Assert.Equal("WHTS", row.Channel);
        Assert.Equal("03XX-XXX4567", row.Recipient);
    }

    [Fact]
    [Trait("Spec", "DLG-1")]
    public async Task List_PagesNewestFirstWithoutOverlap_AndClampsThePageSize()
    {
        await using var seed = postgres.NewContext();
        var ids = await DeliveryTestKit.SeedAsync(seed, 7, Now);

        await using var db = postgres.NewContext();
        var first = await Queries(db).ListAsync(new DeliveryLogFilter(), new PageRequest(1, 3), CancellationToken.None);
        var second = await Queries(db).ListAsync(new DeliveryLogFilter(), new PageRequest(2, 3), CancellationToken.None);
        var third = await Queries(db).ListAsync(new DeliveryLogFilter(), new PageRequest(3, 3), CancellationToken.None);
        var huge = await Queries(db).ListAsync(new DeliveryLogFilter(), new PageRequest(1, 5000), CancellationToken.None);

        var seen = first.Items.Concat(second.Items).Concat(third.Items).Select(r => r.Id).ToList();
        Assert.Equal(ids.OrderByDescending(i => i), seen);
        Assert.Equal(7, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        Assert.Single(third.Items);
        Assert.Equal(PageRequest.MaxPageSize, huge.PageSize);
    }

    [Fact]
    [Trait("Spec", "DLG-1")]
    public async Task List_FiltersByStatusChannelKindAndOrder()
    {
        await using var seed = postgres.NewContext();
        var confirmations = await DeliveryTestKit.SeedAsync(seed, 2, Now);
        var cancels = await DeliveryTestKit.SeedAsync(seed, 1, Now, NotificationKind.OrderCancelled);
        await SetStatusAsync(seed, confirmations[0], 2); // Sent

        await using var db = postgres.NewContext();
        var queries = Queries(db);

        var sent = await queries.ListAsync(new DeliveryLogFilter(Status: "sent"), new PageRequest(), CancellationToken.None);
        Assert.Equal([confirmations[0]], sent.Items.Select(r => r.Id));

        var cancelled = await queries.ListAsync(new DeliveryLogFilter(Kind: "ORCN"), new PageRequest(), CancellationToken.None);
        Assert.Equal([cancels[0]], cancelled.Items.Select(r => r.Id));

        var whatsApp = await queries.ListAsync(new DeliveryLogFilter(Channel: "WHTS"), new PageRequest(), CancellationToken.None);
        Assert.Empty(whatsApp.Items);

        var order = await queries.ListAsync(new DeliveryLogFilter(OrderId: 5001), new PageRequest(), CancellationToken.None);
        Assert.Single(order.Items);

        var unknownCode = await queries.ListAsync(new DeliveryLogFilter(Status: "nonsense"), new PageRequest(), CancellationToken.None);
        Assert.Equal(3, unknownCode.TotalCount);
    }

    [Fact]
    [Trait("Spec", "DLG-1")]
    public async Task List_SearchByOrderNumber_FindsThatOrdersNotificationsOnly()
    {
        await using var seed = postgres.NewContext();
        await DeliveryTestKit.SeedAsync(seed, 3, Now);

        await using var db = postgres.NewContext();
        var found = await Queries(db).ListAsync(new DeliveryLogFilter(Search: "RMP-5001"), new PageRequest(), CancellationToken.None);
        var none = await Queries(db).ListAsync(new DeliveryLogFilter(Search: "someone@nowhere"), new PageRequest(), CancellationToken.None);

        Assert.Equal(5001, Assert.Single(found.Items).OrderId);
        Assert.Empty(none.Items);
        Assert.Equal(0, none.TotalCount);
    }

    [Fact]
    [Trait("Spec", "DLG-5")]
    [Trait("Spec", "FR-62")]
    public async Task List_DeadLettersOnly_ShowsDeadOnesStillWaitingForAResend_WithTheirLastErrorCategory()
    {
        await using var seed = postgres.NewContext();
        var ids = await DeliveryTestKit.SeedAsync(seed, 3, Now);
        await SetStatusAsync(seed, ids[0], 5, 6); // Dead
        await SetStatusAsync(seed, ids[1], 5, 6); // Dead, already resent
        await seed.Database.ExecuteSqlRawAsync("UPDATE \"NTFY\".\"NTFY_CHNL\" SET \"ERR_CATG_ID\" = 1 WHERE \"ID\" = {0}", ids[0]);
        seed.DeadLetters.Add(new DeadLetter(1, ids[0], NotificationKind.OrderConfirmation, NotificationChannel.Email, ErrorCategory.Temporary, 6, Now));
        var resent = new DeadLetter(2, ids[1], NotificationKind.OrderConfirmation, NotificationChannel.Email, ErrorCategory.Temporary, 6, Now);
        resent.MarkResent("support@romp.pk", Now);
        seed.DeadLetters.Add(resent);
        await seed.SaveChangesAsync();

        await using var db = postgres.NewContext();
        var dead = await Queries(db).ListAsync(new DeliveryLogFilter(DeadLettersOnly: true), new PageRequest(), CancellationToken.None);

        var row = Assert.Single(dead.Items);
        Assert.Equal(ids[0], row.Id);
        Assert.Equal("DEAD", row.Status);
        Assert.Equal("TMPY", row.LastError);
    }

    [Fact]
    [Trait("Spec", "DLG-1")]
    [Trait("Spec", "NFR-FT-07")]
    public async Task List_CostsTheSameNumberOfQueriesForOneRowOrForAFullPage()
    {
        await using var seed = postgres.NewContext();
        await DeliveryTestKit.SeedAsync(seed, 1, Now);
        var small = await CountQueriesAsync();
        await DeliveryTestKit.SeedAsync(seed, 30, Now);
        var large = await CountQueriesAsync();

        Assert.Equal(small, large);
        Assert.True(large <= 2, $"A page should cost a count and a page, it cost {large}.");

        async Task<int> CountQueriesAsync()
        {
            var counter = new CommandCounter();
            var contacts = new StubContacts();
            await using var db = postgres.NewContext(counter);
            await Queries(db, contacts).ListAsync(new DeliveryLogFilter(), new PageRequest(1, 100), CancellationToken.None);
            Assert.Equal(1, contacts.FindManyCalls);
            return counter.Count;
        }
    }

    [Fact]
    [Trait("Spec", "DLV-12")]
    [Trait("Spec", "NFR-FT-07")]
    public async Task Get_ReturnsTheAttemptHistoryAndWarnsOfADuplicateForASentMessage()
    {
        await using var seed = postgres.NewContext();
        var id = (await DeliveryTestKit.SeedAsync(seed, 1, Now))[0];
        await SetStatusAsync(seed, id, 2, 1); // Sent
        seed.Attempts.Add(new DeliveryAttempt(id, 1, DeliveryStatus.Sent, null, Now));
        await seed.SaveChangesAsync();

        await using var db = postgres.NewContext();
        var detail = await Queries(db).GetAsync(id, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("SENT", detail.Row.Status);
        Assert.True(detail.WarnsOfDuplicate);
        var attempt = Assert.Single(detail.Attempts);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal("SENT", attempt.Outcome);
        Assert.Null(await Queries(db).GetAsync(id + 999, CancellationToken.None));
    }

    [Fact]
    [Trait("Spec", "DLV-11")]
    [Trait("Spec", "CHN-15")]
    public async Task Summary_CountsByStatus_QueueAge_DeadLettersAwaitingResend_AndTodaysWhatsAppSends()
    {
        await using var seed = postgres.NewContext();
        var ids = await DeliveryTestKit.SeedAsync(seed, 4, Now.AddMinutes(-10));
        await SetStatusAsync(seed, ids[0], 2, 1);
        await SetStatusAsync(seed, ids[1], 5, 6);
        await new SqlDailySendCounter(seed, new TestClock(Now)).IncrementAsync(DateOnly.FromDateTime(Now.UtcDateTime), NotificationChannel.WhatsApp, CancellationToken.None);
        await new SqlDailySendCounter(seed, new TestClock(Now)).IncrementAsync(DateOnly.FromDateTime(Now.UtcDateTime), NotificationChannel.WhatsApp, CancellationToken.None);

        await using var db = postgres.NewContext();
        var summary = await Queries(db).SummaryAsync(CancellationToken.None);

        Assert.Equal(1, summary.CountsByStatus["SENT"]);
        Assert.Equal(2, summary.CountsByStatus["QUED"]);
        Assert.Equal(0, summary.CountsByStatus["DLVD"]);
        Assert.Equal(2, summary.QueuedCount);
        Assert.Equal(1, summary.DeadLettersAwaitingResend);
        Assert.Equal(600, summary.OldestDueSeconds);
        Assert.Equal(2, summary.WhatsAppSentToday);
    }
}
